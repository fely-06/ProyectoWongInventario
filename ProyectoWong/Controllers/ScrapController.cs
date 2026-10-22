using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models.Planta;

namespace ProyectoWong.Controllers
{
    [Route("Scrap")]
    public class ScrapController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ScrapController(ApplicationDbContext context) => _context = context;

        // 1. VISTA PRINCIPAL (Register Scrap)
        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Scrap";
            return View();
        }

        // 2. DATOS PARA LOS SELECTS: órdenes, máquinas y motivos de rechazo
        [HttpGet("catalogos")]
        public async Task<IActionResult> Catalogos()
        {
            try
            {
                var ordenes = await _context.OrdenProduccion
                    .AsNoTracking()
                    .Where(o => o.Estado != "Pendiente")
                    .OrderByDescending(o => o.FechaCreacion)
                    .Select(o => new
                    {
                        id = o.Id,
                        numeroOP = o.NumeroOP,
                        producto = o.Producto != null ? o.Producto.Nombre : "—",
                        cantidadAProducir = o.CantidadAProducir,
                        rechazadas = o.CantidadRechazada
                    })
                    .ToListAsync();

                // Scrap ya registrado por orden (para sugerir cuánto falta por registrar)
                var scrapPorOrden = await _context.RegistrosScrap
                    .AsNoTracking()
                    .GroupBy(r => r.OrdenProduccionId)
                    .Select(g => new { ordenId = g.Key, total = g.Sum(r => r.Cantidad) })
                    .ToListAsync();
                var mapa = scrapPorOrden.ToDictionary(x => x.ordenId, x => x.total);

                var maquinas = await _context.Maquinas
                    .AsNoTracking()
                    .Where(m => m.Activa)
                    .OrderBy(m => m.Codigo)
                    .Select(m => new { id = m.Id, nombre = m.Codigo + " · " + m.Nombre })
                    .ToListAsync();

                return Json(Respuesta.OK("OK", new
                {
                    ordenes = ordenes.Select(o => new
                    {
                        o.id,
                        o.numeroOP,
                        o.producto,
                        o.cantidadAProducir,
                        o.rechazadas,
                        scrapRegistrado = mapa.TryGetValue(o.id, out var t) ? t : 0
                    }),
                    maquinas,
                    motivos = CatalogosPlanta.MotivosRechazo
                }));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // 3. CONSULTAR TODOS LOS REGISTROS + RESUMEN (% scrap y defectos más frecuentes)
        [HttpGet("consultar")]
        public async Task<IActionResult> Consultar()
        {
            try
            {
                var lista = await _context.RegistrosScrap
                    .AsNoTracking()
                    .OrderByDescending(r => r.Fecha)
                    .Select(r => new
                    {
                        r.Id,
                        r.OrdenProduccionId,
                        NumeroOP = r.OrdenProduccion.NumeroOP,
                        Producto = r.OrdenProduccion.Producto != null ? r.OrdenProduccion.Producto.Nombre : "—",
                        CantidadOrden = r.OrdenProduccion.CantidadAProducir,
                        r.MaquinaId,
                        Maquina = r.Maquina.Nombre,
                        r.MotivoRechazo,
                        r.Cantidad,
                        r.Fecha,
                        r.Observaciones
                    })
                    .ToListAsync();

                // Scrap acumulado por orden y piezas totales de las órdenes que tienen scrap
                var acumuladoPorOrden = lista
                    .GroupBy(x => x.OrdenProduccionId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));
                int piezasOrdenes = lista
                    .GroupBy(x => x.OrdenProduccionId)
                    .Sum(g => g.First().CantidadOrden);

                int totalPiezas = lista.Sum(x => x.Cantidad);
                double pctGlobal = piezasOrdenes > 0 ? Math.Round(totalPiezas * 100.0 / piezasOrdenes, 2) : 0;

                var registros = lista.Select(x => new
                {
                    id = x.Id,
                    ordenProduccionId = x.OrdenProduccionId,
                    numeroOP = x.NumeroOP,
                    producto = x.Producto,
                    maquinaId = x.MaquinaId,
                    maquina = x.Maquina,
                    motivoRechazo = x.MotivoRechazo,
                    cantidad = x.Cantidad,
                    cantidadOrden = x.CantidadOrden,
                    // % de este evento respecto a lo que pedía la orden
                    porcentajeEvento = x.CantidadOrden > 0 ? Math.Round(x.Cantidad * 100.0 / x.CantidadOrden, 2) : 0,
                    // % de scrap acumulado de toda la orden
                    porcentajeOrden = x.CantidadOrden > 0
                        ? Math.Round(acumuladoPorOrden[x.OrdenProduccionId] * 100.0 / x.CantidadOrden, 2) : 0,
                    fecha = x.Fecha,
                    observaciones = x.Observaciones
                }).ToList();

                var defectos = lista
                    .GroupBy(x => x.MotivoRechazo)
                    .Select(g => new
                    {
                        motivo = g.Key,
                        eventos = g.Count(),
                        piezas = g.Sum(x => x.Cantidad),
                        porcentaje = totalPiezas > 0 ? Math.Round(g.Sum(x => x.Cantidad) * 100.0 / totalPiezas, 1) : 0
                    })
                    .OrderByDescending(d => d.eventos)
                    .ThenByDescending(d => d.piezas)
                    .ToList();

                return Json(Respuesta.OK("OK", new
                {
                    registros,
                    defectos,
                    resumen = new
                    {
                        totalPiezas,
                        totalEventos = lista.Count,
                        porcentajeGlobal = pctGlobal,
                        defectoPrincipal = defectos.FirstOrDefault()?.motivo ?? "—"
                    }
                }));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // 4. GUARDAR (Id = 0 crea, Id > 0 edita)
        [HttpPost("guardar")]
        public async Task<IActionResult> Guardar([FromBody] ScrapRequest request)
        {
            try
            {
                if (request == null) return Json(Respuesta.Error("Datos inválidos."));
                if (request.Cantidad <= 0) return Json(Respuesta.Error("La cantidad debe ser mayor que cero."));
                if (!CatalogosPlanta.MotivosRechazo.Contains(request.MotivoRechazo))
                    return Json(Respuesta.Error("Selecciona un motivo de rechazo válido."));

                var orden = await _context.OrdenProduccion.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == request.OrdenProduccionId);
                if (orden == null) return Json(Respuesta.Error("Selecciona una orden de producción válida."));

                var maquinaValida = await _context.Maquinas.AnyAsync(m => m.Id == request.MaquinaId && m.Activa);
                if (!maquinaValida) return Json(Respuesta.Error("Selecciona una máquina válida."));

                // El scrap acumulado de una orden no puede superar las piezas de la orden
                int otros = await _context.RegistrosScrap
                    .Where(r => r.OrdenProduccionId == orden.Id && r.Id != request.Id)
                    .SumAsync(r => (int?)r.Cantidad) ?? 0;
                if (otros + request.Cantidad > orden.CantidadAProducir)
                {
                    int disponible = Math.Max(0, orden.CantidadAProducir - otros);
                    return Json(Respuesta.Error(
                        $"El scrap acumulado superaría las {orden.CantidadAProducir} piezas de la orden {orden.NumeroOP}. Máximo que puedes registrar: {disponible}."));
                }

                var observaciones = string.IsNullOrWhiteSpace(request.Observaciones)
                    ? null
                    : request.Observaciones.Trim();
                if (observaciones != null && observaciones.Length > 300) observaciones = observaciones[..300];

                if (request.Id > 0)
                {
                    var existente = await _context.RegistrosScrap.FirstOrDefaultAsync(r => r.Id == request.Id);
                    if (existente == null) return Json(Respuesta.Error("El registro ya no existe."));

                    existente.OrdenProduccionId = request.OrdenProduccionId;
                    existente.MaquinaId = request.MaquinaId;
                    existente.MotivoRechazo = request.MotivoRechazo;
                    existente.Cantidad = request.Cantidad;
                    existente.Observaciones = observaciones;
                    await _context.SaveChangesAsync();
                    return Json(Respuesta.OK("Registro de scrap actualizado."));
                }

                _context.RegistrosScrap.Add(new RegistroScrap
                {
                    OrdenProduccionId = request.OrdenProduccionId,
                    MaquinaId = request.MaquinaId,
                    MotivoRechazo = request.MotivoRechazo,
                    Cantidad = request.Cantidad,
                    Fecha = DateTime.Now,
                    Observaciones = observaciones
                });
                await _context.SaveChangesAsync();
                return Json(Respuesta.OK("Scrap registrado correctamente."));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // 5. ELIMINAR
        [HttpPost("eliminar/{id}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            try
            {
                var registro = await _context.RegistrosScrap.FirstOrDefaultAsync(r => r.Id == id);
                if (registro == null) return Json(Respuesta.Error("El registro ya no existe."));

                _context.RegistrosScrap.Remove(registro);
                await _context.SaveChangesAsync();
                return Json(Respuesta.OK("Registro eliminado."));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        private static string MensajeBD(Exception ex) =>
            "No se pudo completar la operación. ¿Ya se aplicaron las migraciones (dotnet ef database update)? Detalle: "
            + (ex.InnerException?.Message ?? ex.Message);
    }

    public class ScrapRequest
    {
        public int Id { get; set; }
        public int OrdenProduccionId { get; set; }
        public int MaquinaId { get; set; }
        public string MotivoRechazo { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string? Observaciones { get; set; }
    }
}
