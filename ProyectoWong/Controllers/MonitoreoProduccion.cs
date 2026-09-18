using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Models.Monitoreo;

namespace ProyectoWong.Controllers
{
    // IMPORTANTE: el nombre de la clase debe terminar en "Controller" para
    // que ASP.NET Core la reconozca automáticamente como un controlador.
    public class MonitoreoProduccionController : Controller
    {
        private readonly ApplicationDbContext _context;
        public MonitoreoProduccionController(ApplicationDbContext context) => _context = context;

        // Valor de EJEMPLO: cuántos segundos reales tarda producir 1 unidad.
        // Ej: 1 abanico = 1 minuto = 60 segundos.
        private const int SEGUNDOS_POR_UNIDAD = 2;

        // Vista de Monitoreo de Producción, con la tabla "Proceso de orden"
        public async Task<IActionResult> Index()
        {
            // Valor de EJEMPLO para línea de producción (no existe en el modelo aún)
            var lineasEjemplo = new[] { "Línea 1", "Línea 2", "Línea 3" };

            var ordenesEnProceso = await _context.OrdenProduccion
                .Include(o => o.Producto)
                .Where(o => o.Estado == "EnProceso")
                .OrderByDescending(o => o.FechaCreacion)
                .ToListAsync();

            var ahora = DateTime.Now;

            var model = ordenesEnProceso.Select(o =>
            {
                int tiempoTotalSegundos = o.CantidadAProducir * SEGUNDOS_POR_UNIDAD;

                // Si por alguna razón la orden no tiene FechaInicio (no debería
                // pasar si ya está "EnProceso"), asumimos que empieza ahora (0%).
                var fechaInicio = o.FechaInicio ?? ahora;

                double transcurridoSegundos = (ahora - fechaInicio).TotalSeconds;
                if (transcurridoSegundos < 0) transcurridoSegundos = 0;
                if (transcurridoSegundos > tiempoTotalSegundos) transcurridoSegundos = tiempoTotalSegundos;

                int porcentaje = tiempoTotalSegundos > 0
                    ? (int)Math.Round(transcurridoSegundos / tiempoTotalSegundos * 100)
                    : 0;

                return new ProcesoOrdenViewModel
                {
                    NumeroOrden = o.NumeroOP,
                    Producto = o.Producto != null ? o.Producto.Nombre : "-",
                    Cantidad = o.CantidadAProducir,
                    EstadoProduccion = "Pendiente",
                    TiempoProduccion = $"{FormatearTiempo(transcurridoSegundos)} / {FormatearTiempo(tiempoTotalSegundos)}",
                    PorcentajeAvance = porcentaje,

                    // La línea de producción sigue siendo un valor de EJEMPLO,
                    // no existe todavía como campo real en el modelo.
                    LineaProduccion = lineasEjemplo[o.Id % lineasEjemplo.Length]
                };
            }).ToList();

            return View(model);
        }

        // Formatea segundos como mm:ss, ej: 185 segundos -> "03:05"
        private static string FormatearTiempo(double segundosTotales)
        {
            var ts = TimeSpan.FromSeconds(segundosTotales);
            return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
        }
    }
}