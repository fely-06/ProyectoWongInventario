using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Models.Produccion;

namespace ProyectoWong.Controllers
{
    public class MonitoreoVisualController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MonitoreoVisualController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Vista principal del tablero visual
        public IActionResult Index()
        {
            ViewBag.Title = "Inspección Visual - Mini Abanicos";
            return View();
        }

        // 2. Endpoint JSON para el polling del tablero
        [HttpGet("MonitoreoVisual/ObtenerDatos")]
        public async Task<IActionResult> ObtenerDatos(string filtro = "EnInspeccion")
        {
            try
            {
                var query = _context.OrdenProduccion
                    .Include(o => o.Producto)
                    .Where(o => o.Estado == "EnInspeccion" || o.Estado == "Aprobada" || o.Estado == "Rechazada")
                    .OrderByDescending(o => o.FechaFin ?? o.FechaCreacion)
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
                    PorcentajeCalidad = o.CantidadAProducir > 0
                        ? (int)Math.Round((double)o.CantidadAprobada / o.CantidadAProducir * 100)
                        : 0,
                    EstadoInspeccion = o.Estado,
                    DefectoPrincipal = string.IsNullOrEmpty(o.ObservacionesInspeccion)
                        ? "Sin novedad"
                        : o.ObservacionesInspeccion,
                    LineaProduccion = $"Línea {(o.Id % 3) + 1}",
                    TieneFoto = o.FotoInspeccion != null
                }).ToList();

                return Json(new { success = true, data = resultado });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message, data = new List<object>() });
            }
        }

        // 3. Mover orden de "Completada" a "EnInspeccion" (llamado desde la vista de Producción)
        [HttpPost("MonitoreoVisual/EnviarAInspeccion/{id}")]
        public async Task<IActionResult> EnviarAInspeccion(int id)
        {
            var orden = await _context.OrdenProduccion.FindAsync(id);
            if (orden != null && orden.Estado == "Completada")
            {
                orden.Estado = "EnInspeccion";
                await _context.SaveChangesAsync();
                return Json(new { success = true, mensaje = "Orden enviada a inspección visual." });
            }
            return Json(new { success = false, mensaje = "La orden no está completada o no existe." });
        }

        // 4. Registrar el resultado final de la inspección
        // Recibe multipart/form-data porque ahora exige la foto tomada por cámara
        // como evidencia del monitoreo visual (no se acepta sin foto).
        [HttpPost("MonitoreoVisual/RegistrarResultado")]
        [RequestFormLimits(MultipartBodyLengthLimit = 10_000_000)] // 10 MB
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> RegistrarResultado(
            [FromForm] int id,
            [FromForm] int cantidadAprobada,
            [FromForm] int cantidadRechazada,
            [FromForm] string? observaciones,
            IFormFile? foto)
        {
            if (foto == null || foto.Length == 0)
            {
                return Json(new { success = false, mensaje = "Debes capturar una foto con la cámara antes de registrar la inspección." });
            }

            var orden = await _context.OrdenProduccion.FindAsync(id);
            if (orden == null)
            {
                return Json(new { success = false, mensaje = "Orden no encontrada." });
            }

            using (var memoryStream = new MemoryStream())
            {
                await foto.CopyToAsync(memoryStream);
                orden.FotoInspeccion = memoryStream.ToArray();
            }
            orden.FechaFotoInspeccion = DateTime.Now;

            orden.CantidadAprobada = cantidadAprobada;
            orden.CantidadRechazada = cantidadRechazada;
            orden.ObservacionesInspeccion = observaciones;
            orden.Estado = cantidadRechazada > 0 ? "Rechazada" : "Aprobada";
            orden.FechaFin = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, mensaje = "Inspección registrada correctamente." });
        }

        // 5. Sirve la foto de evidencia capturada por cámara para una orden
        [HttpGet("MonitoreoVisual/Foto/{id}")]
        public async Task<IActionResult> Foto(int id)
        {
            var orden = await _context.OrdenProduccion.FindAsync(id);
            if (orden?.FotoInspeccion == null)
            {
                return NotFound();
            }
            return File(orden.FotoInspeccion, "image/jpeg");
        }
    }
}