using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;

namespace ProyectoWong.Controllers
{
    // ── Inventario consolidado ───────────────────────────────────────────
    // Esta pantalla existía como vista vacía y sin controlador. Aquí se
    // arma la vista "real" del inventario: cantidad actual por componente
    // (que ahora sí se actualiza desde RecepcionController.AsignarUbicacion)
    // y, para cada componente, en qué ubicación(es) física(s) se encuentra
    // el material, derivado de los MovimientosInventario más recientes de
    // cada pallet.
    [Route("Inventario")]
    public class InventarioController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventarioController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Inventario";
            return View();
        }

        // ── Devuelve, por componente: cantidad actual y ubicaciones vigentes ──
        [HttpGet("obtener")]
        public async Task<IActionResult> Obtener()
        {
            try
            {
                var componentes = await _context.Componentes
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Nombre)
                    .ToListAsync();

                // Todos los movimientos de inventario, con lo necesario para
                // saber a qué componente pertenece cada pallet.
                var movimientos = await _context.MovimientosInventario
                    .Include(m => m.Ubicacion)
                    .Include(m => m.Pallet!)
                        .ThenInclude(p => p.RecepcionDetalle)
                    .ToListAsync();

                // Última ubicación conocida por pallet (un pallet puede
                // haberse reubicado varias veces; solo interesa la vigente).
                var ultimaUbicacionPorPallet = movimientos
                    .GroupBy(m => m.PalletId)
                    .Select(g => g.OrderByDescending(m => m.FechaMovimiento).First())
                    .ToList();

                // Agrupa esas ubicaciones vigentes por componente.
                var ubicacionesPorComponente = ultimaUbicacionPorPallet
                    .Where(m => m.Pallet?.RecepcionDetalle != null)
                    .GroupBy(m => m.Pallet!.RecepcionDetalle!.ComponenteId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(m => m.Ubicacion?.Codigo)
                              .Where(c => c != null)
                              .Distinct()
                              .ToList()
                    );

                var dto = componentes.Select(c => new
                {
                    id = c.Id,
                    numeroPieza = c.NumeroPieza,
                    nombre = c.Nombre,
                    cantidad = c.Cantidad,
                    minimoInventario = c.MinimoInventario,
                    maximoInventario = c.MaximoInventario,
                    unidadMedida = c.UnidadMedida,
                    // "BajoMinimo" / "SobreMaximo" / "Normal" para colorear en la vista
                    estadoStock = c.Cantidad <= c.MinimoInventario ? "BajoMinimo"
                                : (c.MaximoInventario > 0 && c.Cantidad > c.MaximoInventario) ? "SobreMaximo"
                                : "Normal",
                    ubicaciones = ubicacionesPorComponente.TryGetValue(c.Id, out var ubs) ? ubs : new List<string?>()
                });

                return Json(Respuesta.OK("Inventario cargado", dto));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
    }
}
