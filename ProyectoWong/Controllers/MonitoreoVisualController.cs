using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
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
            ViewBag.ActiveMenu = "Monitoreo"; // Para que el sidebar se marque como activo
            return View();
        }

        // 2. Endpoint JSON para el polling del tablero
        [HttpGet("MonitoreoVisual/ObtenerDatos")]
        public async Task<IActionResult> ObtenerDatos(string filtro = "EnInspeccion")
        {
            try
            {
                // CAMBIO: Ahora buscamos órdenes en Inspección, en Pruebas Técnicas o Completadas
                var query = _context.OrdenProduccion
                    .Include(o => o.Producto)
                    .Where(o => o.Estado == "EnInspeccion" || o.Estado == "EnPruebasTecnicas" || o.Estado == "Completada")
                    .OrderByDescending(o => o.FechaCreacion)
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

        // 3. Mover orden a "EnInspeccion" (Idempotente: si ya está ahí, no falla)
        [HttpPost("MonitoreoVisual/EnviarAInspeccion/{id}")]
        public async Task<IActionResult> EnviarAInspeccion(int id)
        {
            var orden = await _context.OrdenProduccion.FindAsync(id);

            // Si ya está en inspección (el auto-trigger ya actuó), no hay nada que hacer
            if (orden?.Estado == "EnInspeccion")
                return Json(new { success = true, mensaje = "Orden lista para inspección visual." });

            // Si aún se está fabricando, se cierra la fabricación primero para que el inventario se mueva
            if (orden != null && (orden.Estado == "EnProceso" || orden.Estado == "Pausada"))
            {
                await ProduccionFlujo.FinalizarFabricacionAsync(_context, id);
                return Json(new { success = true, mensaje = "Fabricación finalizada e inventario actualizado. Orden lista para inspección visual." });
            }

            return Json(new { success = false, mensaje = $"La orden no puede ser enviada. Estado actual: {orden?.Estado}" });
        }

        // 4. Registrar el resultado de la inspección visual
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

            if (orden.Estado != "EnInspeccion")
            {
                return Json(new { success = false, mensaje = $"La orden no está en inspección visual. Estado actual: {orden.Estado}" });
            }

            // Validación de seguridad: la suma no puede superar el total producido
            if (cantidadAprobada + cantidadRechazada > orden.CantidadAProducir)
            {
                return Json(new { success = false, mensaje = "La suma de aprobados y rechazados no puede superar la cantidad total producida." });
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

            // 🔥 CAMBIO CLAVE: No se marca como "Aprobada" o "Rechazada" final aún.
            // Pasa a la siguiente etapa: Pruebas Técnicas (Sensores).
            orden.Estado = "EnPruebasTecnicas";
            orden.PruebasAprobadas = 0;
            orden.PruebasRechazadas = 0;

            // Si ninguna pieza pasó la inspección visual, no hay nada que probar con sensores
            if (cantidadAprobada == 0)
            {
                orden.Estado = "Completada";
                orden.FechaFin = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new { success = true, mensaje = "Inspección visual registrada. Ninguna pieza fue aprobada, la orden se cierra." });
            }

            // NOTA: No establecemos orden.FechaFin aquí. 
            // La FechaFin real se establecerá en el módulo de Sensores cuando se pruebe la última unidad.

            await _context.SaveChangesAsync();
            return Json(new { success = true, mensaje = "Inspección visual registrada. La orden pasa a Pruebas Técnicas." });
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