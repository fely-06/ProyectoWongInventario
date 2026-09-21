using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Models.Monitoreo;

namespace ProyectoWong.Controllers
{
    public class MonitoreoProduccionController : Controller
    {
        private readonly ApplicationDbContext _context;
        public MonitoreoProduccionController(ApplicationDbContext context) => _context = context;

        private static readonly string[] LineasEjemplo = { "Línea 1", "Línea 2", "Línea 3" };

        // Vista principal
        public IActionResult Index(string filtro = "Todas")
        {
            ViewBag.FiltroActual = filtro;
            return View();
        }

        // Endpoint JSON para el polling en tiempo real (cada 5 seg)
        [HttpGet("ObtenerDatosMonitoreo")]
        public async Task<IActionResult> ObtenerDatosMonitoreo(string filtro="En Proceso")
        {
            try
            {
                Console.WriteLine($"📥 Solicitud recibida - Filtro: {filtro}");

                var query = _context.OrdenProduccion
                    .Include(o => o.Producto)
                    .OrderByDescending(o => o.FechaCreacion)
                    .AsQueryable();

                if (filtro != "Todas")
                {
                    query = query.Where(o => o.Estado == filtro);
                }

                var ordenes = await query.ToListAsync();
                Console.WriteLine($"📦 Órdenes encontradas: {ordenes.Count}");

                var ahora = DateTime.Now;

                var resultado = ordenes.Select(o =>
                {
                    double minutosTrabajados = 0;
                    double duracion = o.DuracionEstimadaMinutos > 0 ? o.DuracionEstimadaMinutos : (o.CantidadAProducir * 0.5);

                    if (o.Estado == "EnProceso" && o.FechaInicio.HasValue)
                    {
                        minutosTrabajados = (ahora - o.FechaInicio.Value).TotalMinutes - o.TiempoPausadoMinutos;
                    }
                    else if (o.Estado == "Pausada" && o.FechaInicio.HasValue && o.FechaPausa.HasValue)
                    {
                        minutosTrabajados = (o.FechaPausa.Value - o.FechaInicio.Value).TotalMinutes - o.TiempoPausadoMinutos;
                    }
                    else if (o.Estado == "Completada")
                    {
                        minutosTrabajados = duracion;
                    }

                    if (duracion <= 0) duracion = 1;
                    if (minutosTrabajados < 0) minutosTrabajados = 0;

                    double progreso = Math.Min(100, Math.Max(0, (minutosTrabajados / duracion) * 100));

                    string tiempoTexto = o.Estado == "Completada" ? "Finalizado" :
                                         $"{FormatearTiempo(minutosTrabajados)} / {FormatearTiempo(duracion)}";

                    return new ProcesoOrdenViewModel
                    {
                        Id = o.Id,
                        NumeroOrden = o.NumeroOP,
                        Producto = o.Producto?.Nombre ?? "Desconocido",
                        Cantidad = o.CantidadAProducir,
                        Estado = o.Estado,
                        TiempoProduccion = tiempoTexto,
                        PorcentajeAvance = (int)Math.Round(progreso),
                        LineaProduccion = $"Línea {(o.Id % 3) + 1}"
                    };
                }).ToList();

                Console.WriteLine($"✅ Retornando {resultado.Count} registros");
                return Json(new { success = true, data = resultado });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return Json(new { success = false, message = ex.Message, data = new List<object>() });
            }
        }

        private static string FormatearTiempo(double minutosTotales)
        {
            int mins = (int)minutosTotales;
            int secs = (int)((minutosTotales - mins) * 60);
            return $"{mins:D2}:{secs:D2}";
        }
        // 1. Endpoint para el Tablero Visual (Polling cada 5 seg)
        [HttpGet("ObtenerDatosInspeccionVisual")]
        public async Task<IActionResult> ObtenerDatosInspeccionVisual(string filtro = "EnInspeccion")
        {
            try
            {
                var query = _context.OrdenProduccion
                    .Include(o => o.Producto)
                    .Where(o => o.Estado == "EnInspeccion" || o.Estado == "Aprobada" || o.Estado == "Rechazada")
                    .OrderByDescending(o => o.FechaFin)
                    .AsQueryable();

                if (filtro != "Todas")
                {
                    query = query.Where(o => o.Estado == filtro);
                }

                var ordenes = await query.ToListAsync();

                var resultado = ordenes.Select(o => new
                {
                    Id = o.Id,
                    NumeroOrden = o.NumeroOP,
                    Producto = o.Producto?.Nombre ?? "Mini Abanico",
                    CantidadTotal = o.CantidadAProducir,
                    CantidadAprobada = o.CantidadAprobada,
                    CantidadRechazada = o.CantidadRechazada,
                    PorcentajeCalidad = o.CantidadAProducir > 0 ? (int)Math.Round((double)o.CantidadAprobada / o.CantidadAProducir * 100) : 0,
                    EstadoInspeccion = o.Estado,
                    DefectoPrincipal = string.IsNullOrEmpty(o.ObservacionesInspeccion) ? "Sin novedad" : o.ObservacionesInspeccion,
                    LineaProduccion = $"Línea {(o.Id % 3) + 1}"
                }).ToList();

                return Json(new { success = true, data = resultado });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message, data = new List<object>() });
            }
        }

        // 2. Mover orden de "Completada" a "EnInspeccion"
        [HttpPost("EnviarAInspeccion/{id}")]
        public async Task<IActionResult> EnviarAInspeccion(int id)
        {
            var orden = await _context.OrdenProduccion.FindAsync(id);
            if (orden != null && orden.Estado == "Completada")
            {
                orden.Estado = "EnInspeccion";
                await _context.SaveChangesAsync();
                return Json(new { success = true, mensaje = "Orden enviada a la zona de inspección visual." });
            }
            return Json(new { success = false, mensaje = "La orden no está completada o no existe." });
        }

        // 3. Registrar el resultado final de la inspección
        [HttpPost("RegistrarResultadoInspeccion")]
        public async Task<IActionResult> RegistrarResultadoInspeccion([FromBody] InspeccionRequest request)
        {
            var orden = await _context.OrdenProduccion.FindAsync(request.Id);
            if (orden != null)
            {
                orden.CantidadAprobada = request.CantidadAprobada;
                orden.CantidadRechazada = request.CantidadRechazada;
                orden.ObservacionesInspeccion = request.Observaciones;
                orden.Estado = request.CantidadRechazada > 0 ? "Rechazada" : "Aprobada";
                orden.FechaFin = DateTime.Now; // Asegura la fecha de cierre real

                await _context.SaveChangesAsync();
                return Json(new { success = true, mensaje = "Inspección registrada correctamente." });
            }
            return Json(new { success = false, mensaje = "Orden no encontrada." });
        }

        // Clase auxiliar para recibir los datos del formulario
        public class InspeccionRequest
        {
            public int Id { get; set; }
            public int CantidadAprobada { get; set; }
            public int CantidadRechazada { get; set; }
            public string? Observaciones { get; set; }
        }

    }
}