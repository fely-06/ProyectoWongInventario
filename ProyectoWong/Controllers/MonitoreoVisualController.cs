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
                    LineaProduccion = $"Línea {(o.Id % 3) + 1}"
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
        [HttpPost("MonitoreoVisual/RegistrarResultado")]
        public async Task<IActionResult> RegistrarResultado([FromBody] InspeccionRequest request)
        {
            var orden = await _context.OrdenProduccion.FindAsync(request.Id);
            if (orden != null)
            {
                orden.CantidadAprobada = request.CantidadAprobada;
                orden.CantidadRechazada = request.CantidadRechazada;
                orden.ObservacionesInspeccion = request.Observaciones;
                orden.Estado = request.CantidadRechazada > 0 ? "Rechazada" : "Aprobada";
                orden.FechaFin = DateTime.Now;

                await _context.SaveChangesAsync();
                return Json(new { success = true, mensaje = "Inspección registrada correctamente." });
            }
            return Json(new { success = false, mensaje = "Orden no encontrada." });
        }
    }

    // DTO para recibir datos del modal
    public class InspeccionRequest
    {
        public int Id { get; set; }
        public int CantidadAprobada { get; set; }
        public int CantidadRechazada { get; set; }
        public string? Observaciones { get; set; }
    }
}