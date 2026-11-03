using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models.ProductoTerminado;

namespace ProyectoWong.Controllers
{
    // Almacén de producto terminado (Finished Goods Inventory):
    // recibe las piezas buenas de las órdenes completadas, las identifica por lote,
    // genera una etiqueta por caja/pallet, confirma su ubicación y da reportes de
    // existencias y trazabilidad.
    [Route("ProductoTerminado")]
    public class ProductoTerminadoController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ProductoTerminadoController(ApplicationDbContext context) => _context = context;

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "ProductoTerminado";
            return View();
        }

        // Piezas buenas de una orden según Calidad: las que pasaron las pruebas de sensores.
        // Órdenes cerradas sin pruebas (flujo anterior) usan lo aprobado en la inspección visual.
        private static int PiezasBuenas(int pruebasAprobadas, int pruebasRechazadas, int aprobadasVisual) =>
            pruebasAprobadas + pruebasRechazadas > 0 ? pruebasAprobadas : aprobadasVisual;

        private int UsuarioActual() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        // 1. ÓRDENES COMPLETADAS QUE AÚN NO ENTRAN AL ALMACÉN
        [HttpGet("pendientes")]
        public async Task<IActionResult> Pendientes()
        {
            var recibidas = _context.LotesProductoTerminado.Select(l => l.OrdenProduccionId);
            var ordenes = await _context.OrdenProduccion
                .AsNoTracking()
                .Where(o => o.Estado == "Completada" && !recibidas.Contains(o.Id))
                .OrderByDescending(o => o.FechaFin)
                .Select(o => new
                {
                    o.Id,
                    o.NumeroOP,
                    Producto = o.Producto != null ? o.Producto.Nombre : "—",
                    o.CantidadAProducir,
                    o.CantidadAprobada,
                    o.CantidadRechazada,
                    o.PruebasAprobadas,
                    o.PruebasRechazadas,
                    o.FechaFin
                })
                .ToListAsync();

            var resultado = ordenes
                .Select(o => new
                {
                    id = o.Id,
                    numeroOP = o.NumeroOP,
                    producto = o.Producto,
                    producidas = o.CantidadAProducir,
                    rechazoVisual = o.CantidadRechazada,
                    rechazoPruebas = o.PruebasRechazadas,
                    buenas = PiezasBuenas(o.PruebasAprobadas, o.PruebasRechazadas, o.CantidadAprobada),
                    fechaFin = o.FechaFin
                })
                .Where(o => o.buenas > 0)
                .ToList();

            return Json(Respuesta.OK("OK", resultado));
        }

        // 2. REGISTRAR LA ENTRADA: crea el lote y una caja/pallet con código propio por cada N piezas
        [HttpPost("recibir")]
        public async Task<IActionResult> Recibir([FromBody] RecibirProductoRequest request)
        {
            if (request == null) return Json(Respuesta.Error("Datos inválidos."));
            if (request.TipoEmpaque != "Caja" && request.TipoEmpaque != "Pallet")
                return Json(Respuesta.Error("Elige si se empaca en cajas o en pallets."));
            if (request.PiezasPorUnidad <= 0)
                return Json(Respuesta.Error("Las piezas por caja/pallet deben ser mayores que cero."));

            var orden = await _context.OrdenProduccion.FirstOrDefaultAsync(o => o.Id == request.OrdenProduccionId);
            if (orden == null) return Json(Respuesta.Error("Orden de producción no encontrada."));
            if (orden.Estado != "Completada")
                return Json(Respuesta.Error("Solo se reciben órdenes completadas (después de Calidad)."));
            if (await _context.LotesProductoTerminado.AnyAsync(l => l.OrdenProduccionId == orden.Id))
                return Json(Respuesta.Error($"La orden {orden.NumeroOP} ya se recibió en el almacén."));

            int buenas = PiezasBuenas(orden.PruebasAprobadas, orden.PruebasRechazadas, orden.CantidadAprobada);
            if (buenas <= 0) return Json(Respuesta.Error("La orden no tiene piezas buenas que recibir."));

            int unidades = (int)Math.Ceiling(buenas / (double)request.PiezasPorUnidad);
            if (unidades > 200)
                return Json(Respuesta.Error($"Serían {unidades} cajas/pallets; aumenta las piezas por unidad."));

            int usuario = UsuarioActual();
            if (usuario == 0) return Json(Respuesta.Error("No se pudo identificar al usuario de la sesión."));

            var hoy = DateTime.Today;
            string prefijo = $"LPT-{hoy:yyyyMMdd}-";
            int consecutivo = await _context.LotesProductoTerminado.CountAsync(l => l.NumeroLote.StartsWith(prefijo)) + 1;
            string numeroLote = $"{prefijo}{consecutivo:D3}";

            var observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim();
            if (observaciones != null && observaciones.Length > 300) observaciones = observaciones[..300];

            var ahora = DateTime.Now;
            var lote = new LoteProductoTerminado
            {
                NumeroLote = numeroLote,
                OrdenProduccionId = orden.Id,
                ProductoId = orden.ProductoId,
                Cantidad = buenas,
                TipoEmpaque = request.TipoEmpaque,
                FechaRecepcion = ahora,
                RecibidoPor = usuario,
                Estado = "PorUbicar",
                Observaciones = observaciones
            };

            // Cada caja/pallet lleva su propio identificador, aunque todas sean del mismo lote
            string letra = request.TipoEmpaque == "Caja" ? "C" : "P";
            int restantes = buenas;
            for (int i = 1; i <= unidades; i++)
            {
                int cantidad = Math.Min(request.PiezasPorUnidad, restantes);
                restantes -= cantidad;
                lote.Pallets.Add(new PalletProductoTerminado
                {
                    CodigoPallet = $"{numeroLote}-{letra}{i:D2}",
                    Cantidad = cantidad,
                    FechaEtiqueta = ahora,
                    Estado = "PorUbicar"
                });
            }

            _context.LotesProductoTerminado.Add(lote);
            await _context.SaveChangesAsync();

            return Json(Respuesta.OK($"Entrada registrada: lote {numeroLote} con {unidades} {(letra == "C" ? "caja(s)" : "pallet(s)")}.",
                new { loteId = lote.Id, numeroLote, unidades }));
        }

        // 3. CAJAS/PALLETS ETIQUETADOS QUE ESPERAN UBICACIÓN
        [HttpGet("por-ubicar")]
        public async Task<IActionResult> PorUbicar()
        {
            var pallets = await _context.PalletsProductoTerminado
                .AsNoTracking()
                .Where(p => p.Estado == "PorUbicar")
                .OrderBy(p => p.Lote!.FechaRecepcion).ThenBy(p => p.CodigoPallet)
                .Select(p => new
                {
                    id = p.Id,
                    codigo = p.CodigoPallet,
                    cantidad = p.Cantidad,
                    loteId = p.LoteId,
                    numeroLote = p.Lote!.NumeroLote,
                    tipo = p.Lote.TipoEmpaque,
                    producto = p.Lote.Producto != null ? p.Lote.Producto.Nombre : "—",
                    numeroOP = p.Lote.OrdenProduccion != null ? p.Lote.OrdenProduccion.NumeroOP : "—",
                    fechaEtiqueta = p.FechaEtiqueta
                })
                .ToListAsync();
            return Json(Respuesta.OK("OK", pallets));
        }

        // 4. EL OPERADOR CONFIRMA LA UBICACIÓN (escaneando o escribiendo el código del pallet).
        //    Aquí el producto entra al stock disponible.
        [HttpPost("ubicar")]
        public async Task<IActionResult> Ubicar([FromBody] UbicarPalletRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CodigoPallet))
                return Json(Respuesta.Error("Escanea o escribe el código de la caja/pallet."));

            var codigo = request.CodigoPallet.Trim().ToUpperInvariant();
            var pallet = await _context.PalletsProductoTerminado
                .Include(p => p.Lote!).ThenInclude(l => l.Producto)
                .Include(p => p.Lote!).ThenInclude(l => l.Pallets)
                .FirstOrDefaultAsync(p => p.CodigoPallet == codigo);
            if (pallet == null) return Json(Respuesta.Error($"No existe la caja/pallet {codigo}."));
            if (pallet.Estado != "PorUbicar")
                return Json(Respuesta.Error($"{codigo} ya está almacenado."));

            var ubicacion = await _context.Ubicaciones.FirstOrDefaultAsync(u => u.Id == request.UbicacionId && u.Activo);
            if (ubicacion == null) return Json(Respuesta.Error("Selecciona una ubicación válida."));

            int usuario = UsuarioActual();
            var ahora = DateTime.Now;

            pallet.UbicacionId = ubicacion.Id;
            pallet.FechaUbicacion = ahora;
            pallet.UbicadoPor = usuario == 0 ? null : usuario;
            pallet.Estado = "Almacenado";

            // Las piezas buenas entran al inventario de producto terminado
            if (pallet.Lote?.Producto != null)
                pallet.Lote.Producto.Cantidad += pallet.Cantidad;

            // El lote queda almacenado cuando todas sus cajas/pallets tienen ubicación
            if (pallet.Lote != null && pallet.Lote.Pallets.All(p => p.Estado == "Almacenado"))
                pallet.Lote.Estado = "Almacenado";

            await _context.SaveChangesAsync();
            return Json(Respuesta.OK($"{codigo} almacenado en {ubicacion.Codigo}. +{pallet.Cantidad} pzas al inventario.",
                new { loteCompleto = pallet.Lote?.Estado == "Almacenado" }));
        }

        // 5a. REPORTE DE EXISTENCIAS: por producto, por lote y por ubicación
        [HttpGet("existencias")]
        public async Task<IActionResult> Existencias()
        {
            var pallets = await _context.PalletsProductoTerminado
                .AsNoTracking()
                .Select(p => new
                {
                    p.CodigoPallet,
                    p.Cantidad,
                    p.Estado,
                    p.FechaUbicacion,
                    Ubicacion = p.Ubicacion != null ? p.Ubicacion.Codigo : null,
                    Zona = p.Ubicacion != null ? p.Ubicacion.Zona : null,
                    p.Lote!.NumeroLote,
                    p.Lote.FechaRecepcion,
                    p.Lote.ProductoId,
                    Producto = p.Lote.Producto != null ? p.Lote.Producto.Nombre : "—",
                    NumeroOP = p.Lote.OrdenProduccion != null ? p.Lote.OrdenProduccion.NumeroOP : "—"
                })
                .ToListAsync();

            var porProducto = pallets
                .GroupBy(p => new { p.ProductoId, p.Producto })
                .Select(g => new
                {
                    producto = g.Key.Producto,
                    almacenado = g.Where(p => p.Estado == "Almacenado").Sum(p => p.Cantidad),
                    porUbicar = g.Where(p => p.Estado == "PorUbicar").Sum(p => p.Cantidad),
                    lotes = g.Select(p => p.NumeroLote).Distinct().Count(),
                    pallets = g.Count()
                })
                .OrderBy(p => p.producto)
                .ToList();

            var porLote = pallets
                .GroupBy(p => new { p.NumeroLote, p.Producto, p.NumeroOP, p.FechaRecepcion })
                .Select(g => new
                {
                    numeroLote = g.Key.NumeroLote,
                    producto = g.Key.Producto,
                    numeroOP = g.Key.NumeroOP,
                    fechaRecepcion = g.Key.FechaRecepcion,
                    cantidad = g.Sum(p => p.Cantidad),
                    pallets = g.Count(),
                    porUbicar = g.Count(p => p.Estado == "PorUbicar"),
                    ubicaciones = g.Where(p => p.Ubicacion != null).Select(p => p.Ubicacion!).Distinct().OrderBy(u => u).ToList()
                })
                .OrderByDescending(l => l.fechaRecepcion)
                .ToList();

            var porUbicacion = pallets
                .Where(p => p.Estado == "Almacenado")
                .GroupBy(p => new { p.Ubicacion, p.Zona })
                .Select(g => new
                {
                    ubicacion = g.Key.Ubicacion,
                    zona = g.Key.Zona,
                    cantidad = g.Sum(p => p.Cantidad),
                    pallets = g.OrderBy(p => p.CodigoPallet).Select(p => new { codigo = p.CodigoPallet, p.NumeroLote, p.Producto, cantidad = p.Cantidad }).ToList()
                })
                .OrderBy(u => u.ubicacion)
                .ToList();

            return Json(Respuesta.OK("OK", new
            {
                resumen = new
                {
                    piezasAlmacenadas = pallets.Where(p => p.Estado == "Almacenado").Sum(p => p.Cantidad),
                    piezasPorUbicar = pallets.Where(p => p.Estado == "PorUbicar").Sum(p => p.Cantidad),
                    lotes = porLote.Count,
                    pallets = pallets.Count
                },
                porProducto,
                porLote,
                porUbicacion
            }));
        }

        // 5b. REPORTE DE TRAZABILIDAD: desde un pallet, un lote o una orden de producción hacia atrás
        //     (orden, calidad, scrap, paros, componentes y la orden de compra con sus lotes de material)
        [HttpGet("trazabilidad")]
        public async Task<IActionResult> Trazabilidad(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return Json(Respuesta.Error("Escribe el código de un pallet, un lote o una orden de producción."));
            codigo = codigo.Trim().ToUpperInvariant();

            var lote = await _context.LotesProductoTerminado
                .AsNoTracking()
                .Include(l => l.Producto)
                .Include(l => l.Receptor)
                .Include(l => l.Pallets).ThenInclude(p => p.Ubicacion)
                .Include(l => l.Pallets).ThenInclude(p => p.Operador)
                .FirstOrDefaultAsync(l => l.NumeroLote == codigo
                    || l.Pallets.Any(p => p.CodigoPallet == codigo)
                    || l.OrdenProduccion!.NumeroOP == codigo);

            int? ordenId = lote?.OrdenProduccionId;
            if (ordenId == null)
                ordenId = await _context.OrdenProduccion.Where(o => o.NumeroOP == codigo).Select(o => (int?)o.Id).FirstOrDefaultAsync();
            if (ordenId == null)
                return Json(Respuesta.Error($"No se encontró ningún pallet, lote u orden con el código {codigo}."));

            var orden = await _context.OrdenProduccion
                .AsNoTracking()
                .Include(o => o.Producto)
                .Include(o => o.OrdenCompra!).ThenInclude(oc => oc.ProveedorNavigation)
                .Include(o => o.Detalles!).ThenInclude(d => d.Componente)
                .FirstAsync(o => o.Id == ordenId);

            var pruebas = await _context.HistorialPruebasSensores
                .AsNoTracking()
                .Where(h => h.OrdenProduccionId == orden.Id)
                .OrderBy(h => h.FechaInicio)
                .Select(h => new { fecha = h.FechaInicio, resultado = h.ResultadoFinal, lecturas = h.Lecturas.Count })
                .ToListAsync();

            var scrap = await _context.RegistrosScrap
                .AsNoTracking()
                .Where(r => r.OrdenProduccionId == orden.Id)
                .OrderBy(r => r.Fecha)
                .Select(r => new { fecha = r.Fecha, maquina = r.Maquina.Codigo, motivo = r.MotivoRechazo, cantidad = r.Cantidad, automatico = r.Automatico })
                .ToListAsync();

            var paros = await _context.RegistrosDowntime
                .AsNoTracking()
                .Where(r => r.OrdenProduccionId == orden.Id)
                .OrderBy(r => r.HoraInicio)
                .Select(r => new { inicio = r.HoraInicio, maquina = r.Maquina.Codigo, causa = r.Causa, minutos = r.DuracionMinutos, estado = r.Estado })
                .ToListAsync();

            // Lotes de material recibidos con la orden de compra ligada a la orden de producción
            var lotesMaterial = orden.OrdenCompraId == null ? new List<object>() : (await _context.RecepcionDetalles
                .AsNoTracking()
                .Where(d => d.Recepcion!.OrdenCompraId == orden.OrdenCompraId && d.Estado == "Aprobado")
                .Select(d => new { componente = d.Componente!.NumeroPieza, lote = d.NumeroLote, cantidad = d.CantidadRecibida, fecha = d.Recepcion!.FechaRecepcion })
                .ToListAsync()).Cast<object>().ToList();

            return Json(Respuesta.OK("OK", new
            {
                buscado = codigo,
                lote = lote == null ? null : new
                {
                    id = lote.Id,
                    numeroLote = lote.NumeroLote,
                    producto = lote.Producto?.Nombre,
                    cantidad = lote.Cantidad,
                    tipo = lote.TipoEmpaque,
                    fechaRecepcion = lote.FechaRecepcion,
                    recibidoPor = lote.Receptor?.NombreCompleto,
                    estado = lote.Estado,
                    observaciones = lote.Observaciones,
                    pallets = lote.Pallets.OrderBy(p => p.CodigoPallet).Select(p => new
                    {
                        codigo = p.CodigoPallet,
                        cantidad = p.Cantidad,
                        estado = p.Estado,
                        ubicacion = p.Ubicacion?.Codigo,
                        fechaUbicacion = p.FechaUbicacion,
                        ubicadoPor = p.Operador?.NombreCompleto,
                        buscado = p.CodigoPallet == codigo
                    })
                },
                orden = new
                {
                    numeroOP = orden.NumeroOP,
                    producto = orden.Producto?.Nombre,
                    estado = orden.Estado,
                    cantidad = orden.CantidadAProducir,
                    fechaCreacion = orden.FechaCreacion,
                    fechaInicio = orden.FechaInicio,
                    fechaFin = orden.FechaFin,
                    ordenCompra = orden.OrdenCompra?.NumeroOC,
                    proveedor = orden.OrdenCompra?.ProveedorNavigation?.Nombre,
                    componentes = (orden.Detalles ?? []).Select(d => new
                    {
                        numeroPieza = d.Componente?.NumeroPieza,
                        nombre = d.Componente?.Nombre,
                        cantidad = d.CantidadConsumida > 0 ? d.CantidadConsumida : d.CantidadRequerida
                    })
                },
                calidad = new
                {
                    aprobadasVisual = orden.CantidadAprobada,
                    rechazadasVisual = orden.CantidadRechazada,
                    defectoVisual = orden.ObservacionesInspeccion,
                    tieneFoto = orden.FotoInspeccion != null,
                    ordenId = orden.Id,
                    pruebasAprobadas = orden.PruebasAprobadas,
                    pruebasRechazadas = orden.PruebasRechazadas,
                    pruebas
                },
                scrap,
                paros,
                lotesMaterial
            }));
        }

        // 6. ETIQUETAS IMPRIMIBLES: una por caja/pallet del lote
        [HttpGet("etiquetas/{loteId}")]
        public async Task<IActionResult> Etiquetas(int loteId)
        {
            var lote = await _context.LotesProductoTerminado
                .AsNoTracking()
                .Include(l => l.Producto)
                .Include(l => l.OrdenProduccion)
                .Include(l => l.Pallets)
                .FirstOrDefaultAsync(l => l.Id == loteId);
            if (lote == null) return NotFound();
            return View("Etiquetas", lote);
        }
    }

    public class RecibirProductoRequest
    {
        public int OrdenProduccionId { get; set; }
        public string TipoEmpaque { get; set; } = "Pallet";
        public int PiezasPorUnidad { get; set; }
        public string? Observaciones { get; set; }
    }

    public class UbicarPalletRequest
    {
        public string CodigoPallet { get; set; } = string.Empty;
        public int UbicacionId { get; set; }
    }
}
