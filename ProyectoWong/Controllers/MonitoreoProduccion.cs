using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Models.Monitoreo;

namespace ProyectoWong.Controllers
{
    // IMPORTANTE: el nombre de la clase debe terminar en "Controller" para
    // que ASP.NET Core la reconozca automáticamente como un controlador.
    // El archivo puede seguir llamándose MonitoreoProduccion.cs, pero la
    // clase debe ser MonitoreoProduccionController.
    public class MonitoreoProduccionController : Controller
    {
        private readonly ApplicationDbContext _context;
        public MonitoreoProduccionController(ApplicationDbContext context) => _context = context;

        // Vista de Monitoreo de Producción, con la tabla "Proceso de orden"
        // Ruta resultante: /MonitoreoProduccion  o  /MonitoreoProduccion/Index
        public async Task<IActionResult> Index()
        {
            // Valores de EJEMPLO para línea de producción (no existe en el modelo aún)
            var lineasEjemplo = new[] { "Línea 1", "Línea 2", "Línea 3" };

            var ordenesEnProceso = await _context.OrdenProduccion
                .Include(o => o.Producto)
                .Where(o => o.Estado == "Pendiente")
                .OrderByDescending(o => o.FechaCreacion)
                .ToListAsync();

            var model = ordenesEnProceso.Select(o => new ProcesoOrdenViewModel
            {
                NumeroOrden = o.NumeroOP,
                Producto = o.Producto != null ? o.Producto.Nombre : "-",
                Cantidad = o.CantidadAProducir,
                EstadoProduccion = "En Producción",

                // ── VALORES DE EJEMPLO ─────────────────────────────────
                // TiempoProduccion, PorcentajeAvance y LineaProduccion son
                // datos simulados (deterministas según el Id de la orden)
                // porque el modelo actual no los guarda todavía.
                // Reemplazar esta lógica cuando existan los campos reales
                // (ej. OrdenProduccion.PorcentajeAvance, .LineaProduccion,
                // o calculado desde FechaInicio/FechaFin).
                TiempoProduccion = $"{2 + (o.Id % 4)}h {(o.Id * 7) % 60}m",
                PorcentajeAvance = 20 + (o.Id * 13) % 70,
                LineaProduccion = lineasEjemplo[o.Id % lineasEjemplo.Length]
            }).ToList();

            return View(model);
        }
    }
}