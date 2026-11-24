using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Models.Indicadores;

namespace ProyectoWong.Controllers
{
    [Route("Indicadores")]
    public class IndicadoresController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly CultureInfo Es = new("es-MX");

        public IndicadoresController(ApplicationDbContext context) => _context = context;

        // VISTA PRINCIPAL
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Indicadores";
            return View();
        }

        // CATÁLOGOS PARA LOS FILTROS
        [HttpGet("catalogos")]
        public async Task<IActionResult> Catalogos()
        {
            var productos = await _context.Productos.AsNoTracking()
                .OrderBy(p => p.Nombre)
                .Select(p => new { p.Id, p.Nombre })
                .ToListAsync();

            var maquinas = await _context.Maquinas.AsNoTracking()
                .OrderBy(m => m.Codigo)
                .Select(m => new { m.Id, Nombre = m.Codigo + " · " + m.Nombre })
                .ToListAsync();

            return Json(new { productos, maquinas });
        }

        // TODOS LOS INDICADORES (JSON)
        [HttpGet("datos")]
        public async Task<IActionResult> Datos([FromQuery] IndicadoresFiltro filtro)
        {
            try
            {
                return Json(await CalcularAsync(filtro));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "No se pudieron calcular los indicadores: " + ex.Message });
            }
        }

        //  CÁLCULO PRINCIPAL
        private async Task<IndicadoresResultado> CalcularAsync(IndicadoresFiltro f)
        {
            // Rango de fechas: por defecto los últimos 30 días
            var desde = (f.Desde ?? DateTime.Today.AddDays(-29)).Date;
            var hasta = (f.Hasta ?? DateTime.Today).Date;
            if (hasta < desde) (desde, hasta) = (hasta, desde);
            var p = new Periodo(desde, hasta);

            var resultado = new IndicadoresResultado
            {
                Desde = desde,
                Hasta = hasta,
                Agrupacion = p.Mensual ? "mes" : "día"
            };

            // Cada módulo se calcula en su propio método para que sea fácil de leer.
            var produccion = await ModuloProduccionAsync(f, p);
            var scrap = await ModuloScrapAsync(f, p);
            var downtime = await ModuloDowntimeAsync(f, p);
            var inventario = await ModuloInventarioAsync();
            var compras = await ModuloComprasAsync(p);
            var recepcion = await ModuloRecepcionAsync(p);
            var pruebas = await ModuloPruebasAsync(f, p);
            var terminado = await ModuloProductoTerminadoAsync(f, p);
            var personal = await ModuloPersonalAsync(p);

            // El resumen general toma los KPIs más importantes de cada módulo
            resultado.Modulos.Add(ModuloResumen(p, produccion, scrap, downtime, inventario, recepcion, pruebas, personal));
            resultado.Modulos.AddRange(new[]
            {
                inventario.Modulo, compras, recepcion.Modulo, produccion.Modulo, pruebas.Modulo,
                scrap.Modulo, downtime.Modulo, terminado, personal.Modulo
            });

            return resultado;
        }

        //  RESUMEN GENERAL 
        private ModuloIndicadores ModuloResumen(Periodo p, ResProduccion prod, ResScrap scrap, ResDowntime dt,
            ResInventario inv, ResRecepcion rec, ResPruebas pru, ResPersonal per)
        {
            var m = new ModuloIndicadores
            {
                Clave = "general",
                Titulo = "Resumen general",
                Icono = "bi-speedometer2",
                Descripcion = "Los indicadores más importantes de toda la planta en un solo vistazo."
            };

            m.Kpis.Add(KpiPct("Calidad a la primera (FPY)", prod.Fpy, 95, 85, true, "Meta ≥ 95 %",
                "Piezas aprobadas ÷ (aprobadas + rechazadas) de las órdenes de producción del periodo."));
            m.Kpis.Add(KpiPct("Cumplimiento del plan", prod.Cumplimiento, 90, 75, true, "Meta ≥ 90 %",
                "Piezas aprobadas ÷ piezas planeadas en las órdenes de producción del periodo."));
            m.Kpis.Add(KpiPct("Tasa de scrap", scrap.Tasa, 5, 10, false, "Meta ≤ 5 %",
                "Piezas en scrap ÷ (piezas aprobadas + piezas en scrap) del periodo."));
            m.Kpis.Add(Kpi("Tiempo de paro", FmtMin(dt.Minutos), dt.Minutos,
                dt.Minutos == 0 ? "ok" : dt.Minutos <= 120 ? "warn" : "danger", "Meta: 0 min",
                "Suma de la duración de todos los paros (Downtime) del periodo."));
            m.Kpis.Add(KpiPct("Aprobación QA de proveedores", rec.AprobacionQa, 95, 85, true, "Meta ≥ 95 %",
                "Lotes aprobados ÷ lotes inspeccionados en recepción durante el periodo."));
            m.Kpis.Add(KpiPct("Pruebas técnicas aprobadas", pru.Aprobacion, 95, 85, true, "Meta ≥ 95 %",
                "Pruebas de sensores con resultado APROBADO ÷ total de pruebas del periodo."));
            m.Kpis.Add(Kpi("Componentes bajo mínimo", N(inv.BajoMinimo), inv.BajoMinimo,
                inv.BajoMinimo == 0 ? "ok" : inv.BajoMinimo <= 3 ? "warn" : "danger", "Meta: 0",
                "Componentes activos cuya cantidad es menor o igual a su inventario mínimo (dato actual)."));
            m.Kpis.Add(KpiPct("Ausentismo", per.Ausentismo, 3, 8, false, "Meta ≤ 3 %",
                "Faltas, permisos e incapacidades ÷ registros de asistencia del periodo (sin contar vacaciones)."));

            // indicadores porcentuales contra su meta
            var nombres = new List<string>();
            var reales = new List<double>();
            var metas = new List<double>();
            void Agregar(string n, double? v, double meta) { if (v.HasValue) { nombres.Add(n); reales.Add(Math.Round(v.Value, 1)); metas.Add(meta); } }
            Agregar("FPY", prod.Fpy, 95);
            Agregar("Cumplimiento plan", prod.Cumplimiento, 90);
            Agregar("Aprobación QA", rec.AprobacionQa, 95);
            Agregar("Pruebas aprobadas", pru.Aprobacion, 95);
            Agregar("Asistencia", per.Ausentismo.HasValue ? 100 - per.Ausentismo : null, 97);

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Indicadores clave vs. meta",
                Tipo = "bar",
                Unidad = "%",
                Etiquetas = nombres,
                Series =
                {
                    new SerieGrafica { Nombre = "Real", Valores = reales },
                    new SerieGrafica { Nombre = "Meta", Valores = metas, Color = "#cbd5e1" }
                }
            });

            // actividad de la planta en el tiempo
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"Actividad de la planta por {p.NombreAgrupacion}",
                Tipo = "line",
                Etiquetas = p.Etiquetas,
                Series =
                {
                    new SerieGrafica { Nombre = "Piezas aprobadas", Valores = prod.AprobadasPorPeriodo },
                    new SerieGrafica { Nombre = "Piezas en scrap", Valores = scrap.PiezasPorPeriodo, Color = "#dc2626" },
                    new SerieGrafica { Nombre = "Minutos de paro", Valores = dt.MinutosPorPeriodo, Color = "#f59e0b" }
                }
            });

            return m;
        }

        //  INVENTARIO DE COMPONENTES
        private async Task<ResInventario> ModuloInventarioAsync()
        {
            var comps = await _context.Componentes.AsNoTracking()
                .Where(c => c.Activo)
                .Select(c => new { c.NumeroPieza, c.Nombre, c.Cantidad, c.MinimoInventario, c.MaximoInventario, c.Precio, c.Categoria })
                .ToListAsync();

            int bajo = comps.Count(c => c.Cantidad <= c.MinimoInventario);
            int sobre = comps.Count(c => c.MaximoInventario > 0 && c.Cantidad > c.MaximoInventario);
            int enRango = comps.Count - bajo - sobre;
            decimal valor = comps.Sum(c => c.Cantidad * c.Precio);
            double? pctRango = Ratio(enRango, comps.Count);

            var m = new ModuloIndicadores
            {
                Clave = "inventario",
                Titulo = "Inventario de componentes",
                Icono = "bi-cpu-fill",
                Descripcion = "Estado actual del almacén de materia prima (no depende del rango de fechas)."
            };

            m.Kpis.Add(Kpi("Componentes activos", N(comps.Count), comps.Count, "neutral", null, "Componentes dados de alta y activos."));
            m.Kpis.Add(Kpi("Valor del inventario", valor.ToString("C2", Es), (double)valor, "neutral", null, "Σ (cantidad × precio) de los componentes activos."));
            m.Kpis.Add(Kpi("Bajo mínimo", N(bajo), bajo, bajo == 0 ? "ok" : bajo <= 3 ? "warn" : "danger", "Meta: 0",
                "Componentes con cantidad ≤ inventario mínimo: hay riesgo de detener producción."));
            m.Kpis.Add(Kpi("Sobre máximo", N(sobre), sobre, sobre == 0 ? "ok" : "warn", "Meta: 0",
                "Componentes con cantidad mayor a su inventario máximo (dinero detenido)."));
            m.Kpis.Add(KpiPct("Componentes en rango", pctRango, 90, 75, true, "Meta ≥ 90 %",
                "Componentes entre su mínimo y su máximo ÷ componentes activos."));

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Estado del stock",
                Tipo = "doughnut",
                Etiquetas = { "Bajo mínimo", "En rango", "Sobre máximo" },
                Series = { new SerieGrafica { Nombre = "Componentes", Valores = { bajo, enRango, sobre } } }
            });

            var porValor = comps.OrderByDescending(c => c.Cantidad * c.Precio).Take(10).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Top 10 componentes por valor en inventario",
                Tipo = "bar",
                Horizontal = true,
                Unidad = "$",
                Etiquetas = porValor.Select(c => c.Nombre).ToList(),
                Series = { new SerieGrafica { Nombre = "Valor", Valores = porValor.Select(c => (double)(c.Cantidad * c.Precio)).ToList() } }
            });

            var criticos = comps.OrderBy(c => c.MinimoInventario == 0 ? double.MaxValue : (double)c.Cantidad / c.MinimoInventario).Take(10).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Stock actual vs. mínimo (10 más críticos)",
                Tipo = "bar",
                Ancha = true,
                Unidad = "pzas",
                Etiquetas = criticos.Select(c => c.Nombre).ToList(),
                Series =
                {
                    new SerieGrafica { Nombre = "Stock actual", Valores = criticos.Select(c => (double)c.Cantidad).ToList() },
                    new SerieGrafica { Nombre = "Mínimo", Valores = criticos.Select(c => (double)c.MinimoInventario).ToList(), Color = "#dc2626" },
                    new SerieGrafica { Nombre = "Máximo", Valores = criticos.Select(c => (double)c.MaximoInventario).ToList(), Color = "#cbd5e1" }
                }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Componentes bajo mínimo",
                Columnas = { "No. pieza", "Componente", "Categoría", "Cantidad", "Mínimo", "Faltante" },
                Filas = comps.Where(c => c.Cantidad <= c.MinimoInventario).OrderBy(c => c.Cantidad - c.MinimoInventario)
                    .Select(c => new List<string> { c.NumeroPieza, c.Nombre, c.Categoria ?? "—", N(c.Cantidad), N(c.MinimoInventario), N(c.MinimoInventario - c.Cantidad) })
                    .ToList()
            };

            return new ResInventario { Modulo = m, BajoMinimo = bajo };
        }

        //  PROVEEDORES Y ÓRDENES DE COMPRA
        private async Task<ModuloIndicadores> ModuloComprasAsync(Periodo p)
        {
            var ocs = await _context.OrdenesCompra.AsNoTracking()
                .Where(o => o.FechaCreacion >= p.Desde && o.FechaCreacion < p.HastaExcl)
                .Select(o => new
                {
                    o.NumeroOC,
                    o.Estado,
                    o.FechaCreacion,
                    o.FechaEsperada,
                    Proveedor = o.ProveedorNavigation != null ? o.ProveedorNavigation.Nombre : "Sin proveedor",
                    Piezas = o.Detalles!.Sum(d => (int?)d.CantidadEsperada) ?? 0,
                    PrimeraRecepcion = o.Recepciones!.Min(r => (DateTime?)r.FechaRecepcion)
                })
                .ToListAsync();

            int abiertasTotal = await _context.OrdenesCompra.CountAsync(o => o.Estado == "Abierta");
            int proveedoresActivos = await _context.Proveedores.CountAsync(x => x.Activo);

            // Entrega a tiempo: OCs con fecha esperada que ya tuvieron al menos una recepción
            var conEntrega = ocs.Where(o => o.FechaEsperada.HasValue && o.PrimeraRecepcion.HasValue).ToList();
            int aTiempo = conEntrega.Count(o => o.PrimeraRecepcion!.Value.Date <= o.FechaEsperada!.Value.Date);
            double? pctATiempo = Ratio(aTiempo, conEntrega.Count);

            var m = new ModuloIndicadores
            {
                Clave = "compras",
                Titulo = "Proveedores y órdenes de compra",
                Icono = "bi-truck",
                Descripcion = "Órdenes de compra creadas en el periodo y desempeño de entrega de los proveedores."
            };

            m.Kpis.Add(Kpi("OC creadas", N(ocs.Count), ocs.Count, "neutral", null, "Órdenes de compra creadas en el periodo."));
            m.Kpis.Add(Kpi("Piezas ordenadas", N(ocs.Sum(o => o.Piezas)), ocs.Sum(o => o.Piezas), "neutral", null, "Σ cantidad esperada en los detalles de las OC del periodo."));
            m.Kpis.Add(Kpi("OC abiertas (hoy)", N(abiertasTotal), abiertasTotal, "neutral", null, "Órdenes de compra en estado \"Abierta\" sin importar la fecha."));
            m.Kpis.Add(KpiPct("Entregas a tiempo", pctATiempo, 90, 75, true, "Meta ≥ 90 %",
                "OC cuya primera recepción llegó en o antes de la fecha esperada ÷ OC recibidas que tenían fecha esperada."));
            m.Kpis.Add(Kpi("Proveedores activos", N(proveedoresActivos), proveedoresActivos, "neutral", null, "Proveedores marcados como activos."));

            var porEstado = ocs.GroupBy(o => o.Estado).OrderByDescending(g => g.Count()).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "OC por estado",
                Tipo = "doughnut",
                Etiquetas = porEstado.Select(g => g.Key).ToList(),
                Series = { new SerieGrafica { Nombre = "OC", Valores = porEstado.Select(g => (double)g.Count()).ToList() } }
            });

            var porProv = ocs.GroupBy(o => o.Proveedor).OrderByDescending(g => g.Count()).Take(8).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "OC por proveedor",
                Tipo = "bar",
                Horizontal = true,
                Etiquetas = porProv.Select(g => g.Key).ToList(),
                Series =
                {
                    new SerieGrafica { Nombre = "Órdenes", Valores = porProv.Select(g => (double)g.Count()).ToList() }
                }
            });

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"OC creadas por {p.NombreAgrupacion}",
                Tipo = "bar",
                Ancha = true,
                Etiquetas = p.Etiquetas,
                Series = { new SerieGrafica { Nombre = "OC creadas", Valores = p.Serie(ocs, o => o.FechaCreacion, _ => 1) } }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Órdenes de compra del periodo",
                Columnas = { "OC", "Proveedor", "Estado", "Creada", "Esperada", "Primera recepción", "Piezas" },
                Filas = ocs.OrderByDescending(o => o.FechaCreacion).Select(o => new List<string>
                {
                    o.NumeroOC, o.Proveedor, o.Estado, Fecha(o.FechaCreacion), Fecha(o.FechaEsperada), Fecha(o.PrimeraRecepcion), N(o.Piezas)
                }).ToList()
            };

            return m;
        }

        //  RECEPCIÓN, INSPECCIÓN QA Y ALMACÉN
        private async Task<ResRecepcion> ModuloRecepcionAsync(Periodo p)
        {
            var recepciones = await _context.Recepciones.AsNoTracking()
                .Where(r => r.FechaRecepcion >= p.Desde && r.FechaRecepcion < p.HastaExcl)
                .Select(r => new
                {
                    r.FechaRecepcion,
                    r.Estado,
                    OC = r.OrdenCompra != null ? r.OrdenCompra.NumeroOC : "-",
                    Proveedor = r.OrdenCompra != null && r.OrdenCompra.ProveedorNavigation != null ? r.OrdenCompra.ProveedorNavigation.Nombre : "-",
                    Lotes = r.Detalles!.Count(),
                    Piezas = r.Detalles!.Sum(d => (int?)d.CantidadRecibida) ?? 0
                })
                .ToListAsync();

            var inspecciones = await _context.InspeccionesQA.AsNoTracking()
                .Where(i => i.FechaInspeccion >= p.Desde && i.FechaInspeccion < p.HastaExcl)
                .Select(i => new { i.Resultado, i.EmpaqueOk, i.EtiquetaOk, i.MaterialOk, i.CertificadoDisponible })
                .ToListAsync();

            int movimientos = await _context.MovimientosInventario
                .CountAsync(mv => mv.FechaMovimiento >= p.Desde && mv.FechaMovimiento < p.HastaExcl);

            int palletsSinUbicacion = await _context.Pallets
                .CountAsync(x => x.RecepcionDetalle != null && x.RecepcionDetalle.Estado == "Aprobado"
                                 && (x.Movimientos == null || !x.Movimientos.Any()));

            int aprobadas = inspecciones.Count(i => i.Resultado == "Aprobado");
            int rechazadas = inspecciones.Count - aprobadas;
            double? pctQa = Ratio(aprobadas, inspecciones.Count);

            var m = new ModuloIndicadores
            {
                Clave = "recepcion",
                Titulo = "Recepción y calidad de entrada",
                Icono = "bi-box-seam-fill",
                Descripcion = "Material recibido de proveedores, resultado de la inspección QA y movimientos de almacén."
            };

            m.Kpis.Add(Kpi("Recepciones", N(recepciones.Count), recepciones.Count, "neutral", null, "Recepciones registradas en el periodo."));
            m.Kpis.Add(Kpi("Piezas recibidas", N(recepciones.Sum(r => r.Piezas)), recepciones.Sum(r => r.Piezas), "neutral", null, "Σ cantidad recibida en los lotes del periodo."));
            m.Kpis.Add(KpiPct("Aprobación QA", pctQa, 95, 85, true, "Meta ≥ 95 %", "Lotes aprobados ÷ lotes inspeccionados."));
            m.Kpis.Add(Kpi("Lotes rechazados", N(rechazadas), rechazadas, rechazadas == 0 ? "ok" : "warn", "Meta: 0", "Lotes cuya inspección QA no fue aprobada."));
            m.Kpis.Add(Kpi("Pallets sin ubicar (hoy)", N(palletsSinUbicacion), palletsSinUbicacion,
                palletsSinUbicacion == 0 ? "ok" : palletsSinUbicacion <= 5 ? "warn" : "danger", "Meta: 0",
                "Pallets de lotes aprobados que todavía no tienen ningún movimiento a una ubicación."));
            m.Kpis.Add(Kpi("Movimientos de almacén", N(movimientos), movimientos, "neutral", null, "Movimientos de inventario registrados en el periodo."));

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Resultado de inspección QA",
                Tipo = "doughnut",
                Etiquetas = { "Aprobado", "Rechazado" },
                Series = { new SerieGrafica { Nombre = "Lotes", Valores = { aprobadas, rechazadas } } }
            });

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Fallas detectadas por criterio de inspección",
                Tipo = "bar",
                Etiquetas = { "Empaque", "Etiqueta", "Material", "Sin certificado" },
                Series =
                {
                    new SerieGrafica
                    {
                        Nombre = "Lotes con falla",
                        Color = "#f59e0b",
                        Valores =
                        {
                            inspecciones.Count(i => !i.EmpaqueOk), inspecciones.Count(i => !i.EtiquetaOk),
                            inspecciones.Count(i => !i.MaterialOk), inspecciones.Count(i => !i.CertificadoDisponible)
                        }
                    }
                }
            });

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"Piezas recibidas por {p.NombreAgrupacion}",
                Tipo = "line",
                Ancha = true,
                Unidad = "pzas",
                Etiquetas = p.Etiquetas,
                Series = { new SerieGrafica { Nombre = "Piezas recibidas", Valores = p.Serie(recepciones, r => r.FechaRecepcion, r => r.Piezas) } }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Recepciones del periodo",
                Columnas = { "Fecha", "OC", "Proveedor", "Lotes", "Piezas", "Estado" },
                Filas = recepciones.OrderByDescending(r => r.FechaRecepcion).Select(r => new List<string>
                {
                    Fecha(r.FechaRecepcion), r.OC, r.Proveedor, N(r.Lotes), N(r.Piezas), r.Estado
                }).ToList()
            };

            return new ResRecepcion { Modulo = m, AprobacionQa = pctQa };
        }

        //  PRODUCCIÓN
        private async Task<ResProduccion> ModuloProduccionAsync(IndicadoresFiltro f, Periodo p)
        {
            var q = _context.OrdenProduccion.AsNoTracking()
                .Where(o => o.FechaCreacion >= p.Desde && o.FechaCreacion < p.HastaExcl);
            if (f.ProductoId.HasValue) q = q.Where(o => o.ProductoId == f.ProductoId);

            var ops = await q.Select(o => new
            {
                o.NumeroOP,
                o.Estado,
                o.FechaCreacion,
                o.CantidadAProducir,
                o.CantidadAprobada,
                o.CantidadRechazada,
                Producto = o.Producto != null ? o.Producto.Nombre : "-"
            }).ToListAsync();

            var validas = ops.Where(o => o.Estado != "Cancelada").ToList();
            int planeadas = validas.Sum(o => o.CantidadAProducir);
            int aprob = validas.Sum(o => o.CantidadAprobada);
            int rech = validas.Sum(o => o.CantidadRechazada);
            int completadas = validas.Count(o => o.Estado == "Completada");
            int enCurso = validas.Count(o => o.Estado is "Pendiente" or "EnProceso" or "Pausada");
            double? fpy = Ratio(aprob, aprob + rech);
            double? cumplimiento = Ratio(aprob, planeadas);

            var m = new ModuloIndicadores
            {
                Clave = "produccion",
                Titulo = "Producción",
                Icono = "bi-gear-wide-connected",
                Descripcion = "Órdenes de producción creadas en el periodo: avance, calidad y cumplimiento."
            };

            m.Kpis.Add(Kpi("Órdenes de producción", N(validas.Count), validas.Count, "neutral", null, "Órdenes creadas en el periodo (sin canceladas)."));
            m.Kpis.Add(Kpi("Completadas", N(completadas), completadas, "neutral", null, "Órdenes en estado Completada."));
            m.Kpis.Add(Kpi("En fabricación", N(enCurso), enCurso, "neutral", null, "Órdenes Pendientes, En proceso o Pausadas."));
            m.Kpis.Add(Kpi("Piezas planeadas", N(planeadas), planeadas, "neutral", null, "Σ cantidad a producir."));
            m.Kpis.Add(Kpi("Piezas aprobadas", N(aprob), aprob, "neutral", null, "Σ cantidad aprobada en inspección."));
            m.Kpis.Add(KpiPct("FPY", fpy, 95, 85, true, "Meta ≥ 95 %", "Aprobadas ÷ (aprobadas + rechazadas)."));
            m.Kpis.Add(KpiPct("Cumplimiento del plan", cumplimiento, 90, 75, true, "Meta ≥ 90 %", "Aprobadas ÷ planeadas."));

            var porEstado = ops.GroupBy(o => o.Estado).OrderByDescending(g => g.Count()).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Órdenes por estado",
                Tipo = "doughnut",
                Etiquetas = porEstado.Select(g => NombreEstado(g.Key)).ToList(),
                Series = { new SerieGrafica { Nombre = "Órdenes", Valores = porEstado.Select(g => (double)g.Count()).ToList() } }
            });

            var porProducto = validas.GroupBy(o => o.Producto).OrderByDescending(g => g.Sum(o => o.CantidadAProducir)).Take(8).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Producción por producto",
                Tipo = "bar",
                Apilada = true,
                Unidad = "pzas",
                Etiquetas = porProducto.Select(g => g.Key).ToList(),
                Series =
                {
                    new SerieGrafica { Nombre = "Aprobadas", Valores = porProducto.Select(g => (double)g.Sum(o => o.CantidadAprobada)).ToList() },
                    new SerieGrafica { Nombre = "Rechazadas", Valores = porProducto.Select(g => (double)g.Sum(o => o.CantidadRechazada)).ToList(), Color = "#dc2626" },
                    new SerieGrafica { Nombre = "Pendientes", Color = "#cbd5e1",
                        Valores = porProducto.Select(g => (double)Math.Max(0, g.Sum(o => o.CantidadAProducir - o.CantidadAprobada - o.CantidadRechazada))).ToList() }
                }
            });

            var aprobadasPorPeriodo = p.Serie(validas, o => o.FechaCreacion, o => o.CantidadAprobada);
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"Piezas planeadas vs. aprobadas por {p.NombreAgrupacion}",
                Tipo = "line",
                Ancha = true,
                Unidad = "pzas",
                Etiquetas = p.Etiquetas,
                Series =
                {
                    new SerieGrafica { Nombre = "Planeadas", Valores = p.Serie(validas, o => o.FechaCreacion, o => o.CantidadAProducir), Color = "#94a3b8" },
                    new SerieGrafica { Nombre = "Aprobadas", Valores = aprobadasPorPeriodo }
                }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Órdenes de producción del periodo",
                Columnas = { "OP", "Producto", "Estado", "Creada", "Planeadas", "Aprobadas", "Rechazadas", "FPY" },
                Filas = ops.OrderByDescending(o => o.FechaCreacion).Select(o => new List<string>
                {
                    o.NumeroOP, o.Producto, NombreEstado(o.Estado), Fecha(o.FechaCreacion), N(o.CantidadAProducir),
                    N(o.CantidadAprobada), N(o.CantidadRechazada), FmtPct(Ratio(o.CantidadAprobada, o.CantidadAprobada + o.CantidadRechazada))
                }).ToList()
            };

            return new ResProduccion { Modulo = m, Fpy = fpy, Cumplimiento = cumplimiento, Aprobadas = aprob, AprobadasPorPeriodo = aprobadasPorPeriodo };
        }

        //  PRUEBAS TÉCNICAS (SENSORES)
        private async Task<ResPruebas> ModuloPruebasAsync(IndicadoresFiltro f, Periodo p)
        {
            var q = _context.HistorialPruebasSensores.AsNoTracking()
                .Where(h => h.FechaInicio >= p.Desde && h.FechaInicio < p.HastaExcl);
            if (f.ProductoId.HasValue) q = q.Where(h => h.OrdenProduccion.ProductoId == f.ProductoId);

            var pruebas = await q.Select(h => new
            {
                h.FechaInicio,
                h.ResultadoFinal,
                h.DuracionSegundos,
                OP = h.OrdenProduccion.NumeroOP,
                Producto = h.OrdenProduccion.Producto != null ? h.OrdenProduccion.Producto.Nombre : "-"
            }).ToListAsync();

            // Las lecturas se resumen directo en SQL (pueden ser muchas filas)
            var lecturasQ = _context.DetalleLecturasSensores.AsNoTracking()
                .Where(l => q.Select(h => h.Id).Contains(l.HistorialId));
            var lect = await lecturasQ
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Fuera = g.Count(l => l.Estado != "OK"),
                    Rpm = g.Average(l => l.Rpm),
                    Temp = g.Average(l => l.Temperatura),
                    Vib = g.Average(l => l.Vibracion),
                    Ruido = g.Average(l => l.Ruido)
                })
                .FirstOrDefaultAsync();

            int aprob = pruebas.Count(x => x.ResultadoFinal == "APROBADO");
            int rech = pruebas.Count - aprob;
            double? pctAprob = Ratio(aprob, pruebas.Count);
            double? pctFuera = lect == null ? null : Ratio(lect.Fuera, lect.Total);

            var m = new ModuloIndicadores
            {
                Clave = "pruebas",
                Titulo = "Pruebas técnicas (sensores)",
                Icono = "bi-activity",
                Descripcion = "Resultados de las pruebas con Arduino: RPM, temperatura, humedad, vibración y ruido."
            };

            m.Kpis.Add(Kpi("Pruebas realizadas", N(pruebas.Count), pruebas.Count, "neutral", null, "Pruebas guardadas en el historial durante el periodo."));
            m.Kpis.Add(KpiPct("Pruebas aprobadas", pctAprob, 95, 85, true, "Meta ≥ 95 %", "Pruebas APROBADAS ÷ pruebas realizadas."));
            m.Kpis.Add(Kpi("Duración promedio", pruebas.Count > 0 ? N(pruebas.Average(x => x.DuracionSegundos), 0) + " s" : "—",
                pruebas.Count > 0 ? pruebas.Average(x => x.DuracionSegundos) : null, "neutral", null, "Promedio de duración de cada prueba en segundos."));
            m.Kpis.Add(KpiPct("Lecturas fuera de rango", pctFuera, 5, 15, false, "Meta ≤ 5 %", "Lecturas con estado distinto de OK ÷ total de lecturas."));
            m.Kpis.Add(Kpi("RPM promedio", lect?.Rpm is double r ? N(r, 0) : "—", lect?.Rpm, "neutral", null, "Promedio de las lecturas de RPM."));
            m.Kpis.Add(Kpi("Temperatura promedio", lect?.Temp is double t ? N(t, 1) + " °C" : "—", lect?.Temp, "neutral", null, "Promedio de las lecturas de temperatura."));
            m.Kpis.Add(Kpi("Vibración promedio", lect?.Vib is double v ? N(v, 2) + " g" : "—", lect?.Vib, "neutral", null, "Promedio de las lecturas de vibración."));
            m.Kpis.Add(Kpi("Ruido promedio", lect?.Ruido is double n ? N(n, 1) + " dB" : "—", lect?.Ruido, "neutral", null, "Promedio de las lecturas de ruido."));

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Resultado de las pruebas",
                Tipo = "doughnut",
                Etiquetas = { "Aprobado", "Rechazado" },
                Series = { new SerieGrafica { Nombre = "Pruebas", Valores = { aprob, rech } } }
            });

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"Pruebas por {p.NombreAgrupacion}",
                Tipo = "bar",
                Apilada = true,
                Etiquetas = p.Etiquetas,
                Series =
                {
                    new SerieGrafica { Nombre = "Aprobadas", Valores = p.Serie(pruebas.Where(x => x.ResultadoFinal == "APROBADO"), x => x.FechaInicio, _ => 1) },
                    new SerieGrafica { Nombre = "Rechazadas", Valores = p.Serie(pruebas.Where(x => x.ResultadoFinal != "APROBADO"), x => x.FechaInicio, _ => 1), Color = "#dc2626" }
                }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Pruebas del periodo",
                Columnas = { "Fecha", "OP", "Producto", "Resultado", "Duración (s)" },
                Filas = pruebas.OrderByDescending(x => x.FechaInicio).Take(50).Select(x => new List<string>
                {
                    FechaHora(x.FechaInicio), x.OP, x.Producto, x.ResultadoFinal, N(x.DuracionSegundos)
                }).ToList()
            };

            return new ResPruebas { Modulo = m, Aprobacion = pctAprob };
        }

        //  SCRAP
        private async Task<ResScrap> ModuloScrapAsync(IndicadoresFiltro f, Periodo p)
        {
            var q = _context.RegistrosScrap.AsNoTracking()
                .Where(r => r.Fecha >= p.Desde && r.Fecha < p.HastaExcl);
            if (f.ProductoId.HasValue) q = q.Where(r => r.OrdenProduccion.ProductoId == f.ProductoId);
            if (f.MaquinaId.HasValue) q = q.Where(r => r.MaquinaId == f.MaquinaId);

            var regs = await q.Select(r => new
            {
                r.Fecha,
                r.Cantidad,
                r.MotivoRechazo,
                r.Automatico,
                Maquina = r.Maquina.Codigo + " · " + r.Maquina.Nombre,
                OP = r.OrdenProduccion.NumeroOP,
                Producto = r.OrdenProduccion.Producto != null ? r.OrdenProduccion.Producto.Nombre : "-"
            }).ToListAsync();

            // Piezas aprobadas del mismo periodo (para sacar la tasa de scrap)
            var opQ = _context.OrdenProduccion.Where(o => o.FechaCreacion >= p.Desde && o.FechaCreacion < p.HastaExcl && o.Estado != "Cancelada");
            if (f.ProductoId.HasValue) opQ = opQ.Where(o => o.ProductoId == f.ProductoId);
            int aprobadas = await opQ.SumAsync(o => (int?)o.CantidadAprobada) ?? 0;

            int piezas = regs.Sum(r => r.Cantidad);
            double? tasa = Ratio(piezas, aprobadas + piezas);
            var motivos = regs.GroupBy(r => r.MotivoRechazo).Select(g => (g.Key, (double)g.Sum(r => r.Cantidad))).ToList();
            var maquinas = regs.GroupBy(r => r.Maquina).Select(g => (g.Key, (double)g.Sum(r => r.Cantidad))).OrderByDescending(x => x.Item2).ToList();

            var m = new ModuloIndicadores
            {
                Clave = "scrap",
                Titulo = "Scrap (piezas rechazadas)",
                Icono = "bi-recycle",
                Descripcion = "Piezas desechadas: cuántas, por qué motivo y en qué máquina."
            };

            m.Kpis.Add(Kpi("Piezas en scrap", N(piezas), piezas, piezas == 0 ? "ok" : "warn", null, "Σ cantidad de los registros de scrap del periodo."));
            m.Kpis.Add(KpiPct("Tasa de scrap", tasa, 5, 10, false, "Meta ≤ 5 %", "Piezas en scrap ÷ (aprobadas + scrap)."));
            m.Kpis.Add(Kpi("Registros", N(regs.Count), regs.Count, "neutral", null, "Número de capturas de scrap."));
            m.Kpis.Add(KpiPct("Detectado automático", Ratio(regs.Where(r => r.Automatico).Sum(r => r.Cantidad), piezas), 0, 0, true, null,
                "Piezas registradas automáticamente por las pruebas de sensores ÷ total de piezas en scrap.", semaforo: false));
            m.Kpis.Add(Kpi("Motivo principal", motivos.Count > 0 ? motivos.OrderByDescending(x => x.Item2).First().Key : "—", null, "neutral", null, "Motivo con más piezas rechazadas."));
            m.Kpis.Add(Kpi("Máquina con más scrap", maquinas.Count > 0 ? maquinas.First().Key : "—", null, "neutral", null, "Máquina con más piezas rechazadas."));

            m.Graficas.Add(Pareto("Pareto de motivos de rechazo", motivos, "Piezas", "pzas"));

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Scrap por máquina",
                Tipo = "bar",
                Horizontal = true,
                Unidad = "pzas",
                Etiquetas = maquinas.Select(x => x.Key).ToList(),
                Series = { new SerieGrafica { Nombre = "Piezas", Valores = maquinas.Select(x => x.Item2).ToList(), Color = "#f59e0b" } }
            });

            var piezasPorPeriodo = p.Serie(regs, r => r.Fecha, r => r.Cantidad);
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"Scrap por {p.NombreAgrupacion}",
                Tipo = "line",
                Ancha = true,
                Unidad = "pzas",
                Etiquetas = p.Etiquetas,
                Series = { new SerieGrafica { Nombre = "Piezas en scrap", Valores = piezasPorPeriodo, Color = "#dc2626" } }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Registros de scrap (últimos 50)",
                Columnas = { "Fecha", "OP", "Producto", "Máquina", "Motivo", "Piezas", "Origen" },
                Filas = regs.OrderByDescending(r => r.Fecha).Take(50).Select(r => new List<string>
                {
                    FechaHora(r.Fecha), r.OP, r.Producto, r.Maquina, r.MotivoRechazo, N(r.Cantidad), r.Automatico ? "Automático" : "Manual"
                }).ToList()
            };

            return new ResScrap { Modulo = m, Tasa = tasa, PiezasPorPeriodo = piezasPorPeriodo };
        }

        //  DOWNTIME (PAROS)
        private async Task<ResDowntime> ModuloDowntimeAsync(IndicadoresFiltro f, Periodo p)
        {
            var q = _context.RegistrosDowntime.AsNoTracking()
                .Where(r => r.HoraInicio >= p.Desde && r.HoraInicio < p.HastaExcl);
            if (f.MaquinaId.HasValue) q = q.Where(r => r.MaquinaId == f.MaquinaId);
            if (f.ProductoId.HasValue) q = q.Where(r => r.OrdenProduccion != null && r.OrdenProduccion.ProductoId == f.ProductoId);

            var ahora = DateTime.Now;
            var paros = (await q.Select(r => new
            {
                r.HoraInicio,
                r.Causa,
                r.Estado,
                r.DuracionMinutos,
                r.Simulado,
                Maquina = r.Maquina.Codigo + " · " + r.Maquina.Nombre
            }).ToListAsync())
            // Un paro en curso todavía no tiene duración guardada: se calcula al momento
            .Select(r => new
            {
                r.HoraInicio,
                r.Causa,
                r.Estado,
                r.Simulado,
                r.Maquina,
                Minutos = r.Estado == "EnCurso" ? Math.Max(0, (ahora - r.HoraInicio).TotalMinutes) : r.DuracionMinutos
            }).ToList();

            double minutos = paros.Sum(x => x.Minutos);
            var finalizados = paros.Where(x => x.Estado != "EnCurso").ToList();
            double? mttr = finalizados.Count > 0 ? finalizados.Average(x => x.Minutos) : null;
            int enCurso = paros.Count(x => x.Estado == "EnCurso");
            var causas = paros.GroupBy(x => x.Causa).Select(g => (g.Key, Math.Round(g.Sum(x => x.Minutos), 1))).ToList();
            var maquinas = paros.GroupBy(x => x.Maquina).Select(g => (g.Key, Math.Round(g.Sum(x => x.Minutos), 1))).OrderByDescending(x => x.Item2).ToList();

            var m = new ModuloIndicadores
            {
                Clave = "downtime",
                Titulo = "Downtime (paros de máquina)",
                Icono = "bi-hourglass-split",
                Descripcion = "Tiempo perdido por paros: cuánto, por qué causa y en qué máquina."
            };

            m.Kpis.Add(Kpi("Tiempo total de paro", FmtMin(minutos), minutos, minutos == 0 ? "ok" : minutos <= 120 ? "warn" : "danger", "Meta: 0 min", "Σ duración de los paros del periodo."));
            m.Kpis.Add(Kpi("Paros", N(paros.Count), paros.Count, "neutral", null, "Número de paros registrados."));
            m.Kpis.Add(Kpi("MTTR", mttr.HasValue ? FmtMin(mttr.Value) : "—", mttr,
                mttr == null ? "neutral" : mttr <= 15 ? "ok" : mttr <= 30 ? "warn" : "danger", "Meta ≤ 15 min",
                "Tiempo medio de reparación: duración promedio de los paros ya finalizados."));
            m.Kpis.Add(Kpi("Máquinas detenidas ahora", N(enCurso), enCurso, enCurso == 0 ? "ok" : "danger", null, "Paros en curso en este momento."));
            m.Kpis.Add(Kpi("Causa principal", causas.Count > 0 ? causas.OrderByDescending(x => x.Item2).First().Key : "—", null, "neutral", null, "Causa con más minutos perdidos."));

            m.Graficas.Add(Pareto("Pareto de causas de paro", causas, "Minutos", "min"));

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Minutos de paro por máquina",
                Tipo = "bar",
                Horizontal = true,
                Unidad = "min",
                Etiquetas = maquinas.Select(x => x.Key).ToList(),
                Series = { new SerieGrafica { Nombre = "Minutos", Valores = maquinas.Select(x => x.Item2).ToList(), Color = "#f59e0b" } }
            });

            var minutosPorPeriodo = p.Serie(paros, x => x.HoraInicio, x => x.Minutos).Select(v => Math.Round(v, 1)).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"Minutos de paro por {p.NombreAgrupacion}",
                Tipo = "bar",
                Ancha = true,
                Unidad = "min",
                Etiquetas = p.Etiquetas,
                Series = { new SerieGrafica { Nombre = "Minutos de paro", Valores = minutosPorPeriodo, Color = "#f59e0b" } }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Paros del periodo (últimos 50)",
                Columnas = { "Inicio", "Máquina", "Causa", "Duración", "Estado", "Tipo" },
                Filas = paros.OrderByDescending(x => x.HoraInicio).Take(50).Select(x => new List<string>
                {
                    FechaHora(x.HoraInicio), x.Maquina, x.Causa, FmtMin(x.Minutos), x.Estado == "EnCurso" ? "En curso" : "Finalizado", x.Simulado ? "Simulado" : "Real"
                }).ToList()
            };

            return new ResDowntime { Modulo = m, Minutos = Math.Round(minutos, 1), MinutosPorPeriodo = minutosPorPeriodo };
        }

        //  PRODUCTO TERMINADO
        private async Task<ModuloIndicadores> ModuloProductoTerminadoAsync(IndicadoresFiltro f, Periodo p)
        {
            var q = _context.LotesProductoTerminado.AsNoTracking()
                .Where(l => l.FechaRecepcion >= p.Desde && l.FechaRecepcion < p.HastaExcl);
            if (f.ProductoId.HasValue) q = q.Where(l => l.ProductoId == f.ProductoId);

            var lotes = await q.Select(l => new
            {
                l.NumeroLote,
                l.Cantidad,
                l.TipoEmpaque,
                l.FechaRecepcion,
                l.Estado,
                Producto = l.Producto != null ? l.Producto.Nombre : "-",
                Pallets = l.Pallets.Count()
            }).ToListAsync();

            // Foto actual de los pallets (no depende de fechas)
            var palletsQ = _context.PalletsProductoTerminado.AsNoTracking().AsQueryable();
            if (f.ProductoId.HasValue) palletsQ = palletsQ.Where(x => x.Lote != null && x.Lote.ProductoId == f.ProductoId);
            var pallets = await palletsQ.Select(x => new { x.Estado, x.FechaEtiqueta, x.FechaUbicacion }).ToListAsync();
            int porUbicar = pallets.Count(x => x.Estado == "PorUbicar");
            int almacenados = pallets.Count(x => x.Estado == "Almacenado");
            var ubicados = pallets.Where(x => x.FechaUbicacion.HasValue).ToList();
            double? horasUbicar = ubicados.Count > 0 ? ubicados.Average(x => (x.FechaUbicacion!.Value - x.FechaEtiqueta).TotalHours) : null;

            var prodQ = _context.Productos.AsNoTracking().Where(x => x.Activo);
            if (f.ProductoId.HasValue) prodQ = prodQ.Where(x => x.Id == f.ProductoId);
            var stock = await prodQ.OrderByDescending(x => x.Cantidad).Select(x => new { x.Nombre, x.Cantidad }).ToListAsync();

            var m = new ModuloIndicadores
            {
                Clave = "terminado",
                Titulo = "Almacén de producto terminado",
                Icono = "bi-archive-fill",
                Descripcion = "Lotes que salen de producción hacia el almacén y el stock disponible para venta."
            };

            m.Kpis.Add(Kpi("Lotes recibidos", N(lotes.Count), lotes.Count, "neutral", null, "Lotes de producto terminado recibidos en el periodo."));
            m.Kpis.Add(Kpi("Piezas recibidas", N(lotes.Sum(l => l.Cantidad)), lotes.Sum(l => l.Cantidad), "neutral", null, "Σ cantidad de los lotes del periodo."));
            m.Kpis.Add(Kpi("Stock disponible (hoy)", N(stock.Sum(x => x.Cantidad)), stock.Sum(x => x.Cantidad), "neutral", null, "Σ existencia de producto terminado de los productos activos."));
            m.Kpis.Add(Kpi("Pallets por ubicar (hoy)", N(porUbicar), porUbicar, porUbicar == 0 ? "ok" : porUbicar <= 5 ? "warn" : "danger", "Meta: 0", "Pallets etiquetados que aún no tienen ubicación."));
            m.Kpis.Add(Kpi("Tiempo promedio para ubicar", horasUbicar.HasValue ? N(horasUbicar.Value, 1) + " h" : "—", horasUbicar,
                horasUbicar == null ? "neutral" : horasUbicar <= 4 ? "ok" : horasUbicar <= 24 ? "warn" : "danger", "Meta ≤ 4 h",
                "Promedio de horas entre que se etiqueta un pallet y se ubica en el almacén."));

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Stock de producto terminado",
                Tipo = "bar",
                Horizontal = true,
                Unidad = "pzas",
                Etiquetas = stock.Take(10).Select(x => x.Nombre).ToList(),
                Series = { new SerieGrafica { Nombre = "Existencia", Valores = stock.Take(10).Select(x => (double)x.Cantidad).ToList() } }
            });

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Pallets por estado",
                Tipo = "doughnut",
                Etiquetas = { "Almacenado", "Por ubicar" },
                Series = { new SerieGrafica { Nombre = "Pallets", Valores = { almacenados, porUbicar } } }
            });

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"Piezas recibidas en almacén por {p.NombreAgrupacion}",
                Tipo = "line",
                Ancha = true,
                Unidad = "pzas",
                Etiquetas = p.Etiquetas,
                Series = { new SerieGrafica { Nombre = "Piezas", Valores = p.Serie(lotes, l => l.FechaRecepcion, l => l.Cantidad) } }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Lotes del periodo",
                Columnas = { "Lote", "Producto", "Piezas", "Empaque", "Pallets", "Recibido", "Estado" },
                Filas = lotes.OrderByDescending(l => l.FechaRecepcion).Select(l => new List<string>
                {
                    l.NumeroLote, l.Producto, N(l.Cantidad), l.TipoEmpaque, N(l.Pallets), FechaHora(l.FechaRecepcion), l.Estado == "PorUbicar" ? "Por ubicar" : l.Estado
                }).ToList()
            };

            return m;
        }

        //  PERSONAL
        private async Task<ResPersonal> ModuloPersonalAsync(Periodo p)
        {
            var empleados = await _context.Empleados.AsNoTracking()
                .Where(e => e.Activo).Select(e => new { e.Id, e.Nombre, e.Puesto }).ToListAsync();

            var registros = await _context.Asistencias.AsNoTracking()
                .Where(a => a.Fecha >= p.Desde && a.Fecha < p.HastaExcl)
                .Select(a => new { a.EmpleadoId, a.Fecha, a.Tipo, Empleado = a.Empleado != null ? a.Empleado.Nombre : "-" })
                .ToListAsync();

            var hoy = DateTime.Today;
            int asignacionesVigentes = await _context.AsignacionesTurno
                .CountAsync(a => a.FechaInicio <= hoy && (a.FechaFin == null || a.FechaFin >= hoy));
            int presentesHoy = await _context.Asistencias.CountAsync(a => a.Fecha == hoy && a.Tipo == "Asistencia");

            // Vacaciones son ausencias planeadas: no cuentan para el ausentismo
            var sinVacaciones = registros.Where(r => r.Tipo != "Vacaciones").ToList();
            int ausencias = sinVacaciones.Count(r => r.Tipo != "Asistencia");
            double? ausentismo = Ratio(ausencias, sinVacaciones.Count);

            var m = new ModuloIndicadores
            {
                Clave = "personal",
                Titulo = "Personal",
                Icono = "bi-person-badge-fill",
                Descripcion = "Plantilla, asignación de turnos y asistencia de los empleados."
            };

            m.Kpis.Add(Kpi("Empleados activos", N(empleados.Count), empleados.Count, "neutral", null, "Empleados marcados como activos."));
            m.Kpis.Add(Kpi("Asignaciones vigentes", N(asignacionesVigentes), asignacionesVigentes, "neutral", null, "Asignaciones de turno vigentes hoy."));
            m.Kpis.Add(Kpi("Presentes hoy", N(presentesHoy), presentesHoy, "neutral", null, "Empleados con asistencia registrada el día de hoy."));
            m.Kpis.Add(Kpi("Asistencias del periodo", N(registros.Count(r => r.Tipo == "Asistencia")), registros.Count(r => r.Tipo == "Asistencia"), "neutral", null, "Registros de tipo Asistencia."));
            m.Kpis.Add(KpiPct("Ausentismo", ausentismo, 3, 8, false, "Meta ≤ 3 %", "Faltas, permisos e incapacidades ÷ registros (sin vacaciones)."));

            var porTipo = registros.GroupBy(r => r.Tipo).OrderByDescending(g => g.Count()).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Registros por tipo",
                Tipo = "doughnut",
                Etiquetas = porTipo.Select(g => g.Key).ToList(),
                Series = { new SerieGrafica { Nombre = "Registros", Valores = porTipo.Select(g => (double)g.Count()).ToList() } }
            });

            var porPuesto = empleados.GroupBy(e => e.Puesto).OrderByDescending(g => g.Count()).ToList();
            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = "Empleados por puesto",
                Tipo = "bar",
                Etiquetas = porPuesto.Select(g => g.Key).ToList(),
                Series = { new SerieGrafica { Nombre = "Empleados", Valores = porPuesto.Select(g => (double)g.Count()).ToList() } }
            });

            m.Graficas.Add(new GraficaIndicador
            {
                Titulo = $"Asistencia vs. ausencias por {p.NombreAgrupacion}",
                Tipo = "bar",
                Apilada = true,
                Ancha = true,
                Etiquetas = p.Etiquetas,
                Series =
                {
                    new SerieGrafica { Nombre = "Asistencias", Valores = p.Serie(registros.Where(r => r.Tipo == "Asistencia"), r => r.Fecha, _ => 1) },
                    new SerieGrafica { Nombre = "Ausencias", Valores = p.Serie(registros.Where(r => r.Tipo != "Asistencia"), r => r.Fecha, _ => 1), Color = "#dc2626" }
                }
            });

            m.Tabla = new TablaReporte
            {
                Titulo = "Empleados con más ausencias en el periodo",
                Columnas = { "Empleado", "Asistencias", "Faltas", "Permisos", "Incapacidades", "Vacaciones" },
                Filas = registros.GroupBy(r => r.Empleado)
                    .Select(g => new
                    {
                        g.Key,
                        A = g.Count(r => r.Tipo == "Asistencia"),
                        F = g.Count(r => r.Tipo == "Falta"),
                        P = g.Count(r => r.Tipo == "Permiso"),
                        I = g.Count(r => r.Tipo == "Incapacidad"),
                        V = g.Count(r => r.Tipo == "Vacaciones")
                    })
                    .Where(x => x.F + x.P + x.I + x.V > 0)
                    .OrderByDescending(x => x.F).ThenByDescending(x => x.F + x.P + x.I)
                    .Take(15)
                    .Select(x => new List<string> { x.Key, N(x.A), N(x.F), N(x.P), N(x.I), N(x.V) })
                    .ToList()
            };

            return new ResPersonal { Modulo = m, Ausentismo = ausentismo };
        }

        //  metodos auxiliares

        private static GraficaIndicador Pareto(string titulo, List<(string Key, double Valor)> datos, string nombre, string unidad)
        {
            var orden = datos.OrderByDescending(x => x.Valor).ToList();
            double total = orden.Sum(x => x.Valor);
            double acum = 0;
            var acumulado = orden.Select(x => { acum += x.Valor; return total > 0 ? Math.Round(acum / total * 100, 1) : 0; }).ToList();

            return new GraficaIndicador
            {
                Titulo = titulo,
                Tipo = "pareto",
                Unidad = unidad,
                Etiquetas = orden.Select(x => x.Key).ToList(),
                Series =
                {
                    new SerieGrafica { Nombre = nombre, Valores = orden.Select(x => x.Valor).ToList() },
                    new SerieGrafica { Nombre = "% acumulado", Valores = acumulado, Color = "#dc2626" }
                }
            };
        }

        private static IndicadorKpi Kpi(string nombre, string valor, double? numero, string estado, string? meta, string descripcion) =>
            new() { Nombre = nombre, Valor = valor, Numero = numero, Estado = estado, Meta = meta, Descripcion = descripcion };

        // KPI en porcentaje con semáforo automático.
        // mayorEsMejor = true  -> verde si valor ≥ ok, amarillo si ≥ warn, rojo si no.
        // mayorEsMejor = false -> verde si valor ≤ ok, amarillo si ≤ warn, rojo si no.
        private static IndicadorKpi KpiPct(string nombre, double? valor, double ok, double warn, bool mayorEsMejor,
            string? meta, string descripcion, bool semaforo = true)
        {
            string estado = "neutral";
            if (semaforo && valor.HasValue)
            {
                estado = mayorEsMejor
                    ? (valor >= ok ? "ok" : valor >= warn ? "warn" : "danger")
                    : (valor <= ok ? "ok" : valor <= warn ? "warn" : "danger");
            }
            return Kpi(nombre, FmtPct(valor), valor.HasValue ? Math.Round(valor.Value, 1) : null, estado, meta, descripcion);
        }

        private static double? Ratio(double parte, double total) => total > 0 ? parte / total * 100 : null;
        private static string N(double v, int dec = 0) => v.ToString("N" + dec, Es);
        private static string FmtPct(double? v) => v.HasValue ? v.Value.ToString("0.0", Es) + " %" : "—";
        private static string Fecha(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy", Es) : "—";
        private static string FechaHora(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Es);
        private static string FmtMin(double m)
        {
            if (m < 60) return N(m, 1) + " min";
            int h = (int)(m / 60);
            return $"{h} h {N(m - h * 60, 0)} min";
        }

        private static string NombreEstado(string e) => e switch
        {
            "EnProceso" => "En proceso",
            "EnInspeccion" => "En inspección",
            "EnPruebasTecnicas" => "En pruebas técnicas",
            _ => e
        };

        private sealed class Periodo
        {
            public DateTime Desde { get; }
            public DateTime HastaExcl { get; }
            public bool Mensual { get; }
            public string NombreAgrupacion => Mensual ? "mes" : "día";
            public List<string> Etiquetas { get; }
            private readonly List<DateTime> _cubetas = new();

            public Periodo(DateTime desde, DateTime hasta)
            {
                Desde = desde;
                HastaExcl = hasta.AddDays(1);
                Mensual = (hasta - desde).TotalDays > 62;

                for (var d = Cubeta(desde); d <= hasta; d = Mensual ? d.AddMonths(1) : d.AddDays(1))
                    _cubetas.Add(d);

                Etiquetas = _cubetas.Select(d => Mensual ? d.ToString("MMM yyyy", Es) : d.ToString("dd MMM", Es)).ToList();
            }

            private DateTime Cubeta(DateTime d) => Mensual ? new DateTime(d.Year, d.Month, 1) : d.Date;

            public List<double> Serie<T>(IEnumerable<T> items, Func<T, DateTime> fecha, Func<T, double> valor)
            {
                var suma = items.GroupBy(x => Cubeta(fecha(x))).ToDictionary(g => g.Key, g => g.Sum(valor));
                return _cubetas.Select(c => suma.TryGetValue(c, out var v) ? Math.Round(v, 2) : 0).ToList();
            }
        }

        // Resultados intermedios: el módulo y los números que reutiliza el resumen general
        private sealed class ResProduccion { public ModuloIndicadores Modulo = null!; public double? Fpy, Cumplimiento; public int Aprobadas; public List<double> AprobadasPorPeriodo = new(); }
        private sealed class ResScrap { public ModuloIndicadores Modulo = null!; public double? Tasa; public List<double> PiezasPorPeriodo = new(); }
        private sealed class ResDowntime { public ModuloIndicadores Modulo = null!; public double Minutos; public List<double> MinutosPorPeriodo = new(); }
        private sealed class ResInventario { public ModuloIndicadores Modulo = null!; public int BajoMinimo; }
        private sealed class ResRecepcion { public ModuloIndicadores Modulo = null!; public double? AprobacionQa; }
        private sealed class ResPruebas { public ModuloIndicadores Modulo = null!; public double? Aprobacion; }
        private sealed class ResPersonal { public ModuloIndicadores Modulo = null!; public double? Ausentismo; }
    }
}
