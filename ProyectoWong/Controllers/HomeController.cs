using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Models;
using System.Diagnostics;

namespace ProyectoWong.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new DashboardViewModel
            {
                TotalComponentes = await _context.Componentes.CountAsync(c => c.Activo),

                ComponentesBajoMinimo = await _context.Componentes
                    .CountAsync(c => c.Activo && c.Cantidad <= c.MinimoInventario),

                OrdenesCompraAbiertas = await _context.OrdenesCompra
                    .CountAsync(o => o.Estado == "Abierta"),

                RecepcionesEnProceso = await _context.Recepciones
                    .CountAsync(r => r.Estado == "EnProceso"),

                UbicacionesActivas = await _context.Ubicaciones
                    .CountAsync(u => u.Activo),

                // Pallets que ya pasaron QA pero todavía no tienen ningún
                // movimiento de inventario (es decir, nunca se les asignó ubicación).
                PalletsSinUbicacion = await _context.Pallets
                    .CountAsync(p => p.RecepcionDetalle != null
                                   && p.RecepcionDetalle.Estado == "Aprobado"
                                   && (p.Movimientos == null || !p.Movimientos.Any()))
            };

            // ── Componentes en estado crítico (para actuar rápido) ──────
            vm.ComponentesCriticos = await _context.Componentes
                .Where(c => c.Activo && c.Cantidad <= c.MinimoInventario)
                .OrderBy(c => c.Cantidad)
                .Take(5)
                .Select(c => new ComponenteBajoStockItem
                {
                    Id = c.Id,
                    NumeroPieza = c.NumeroPieza,
                    Nombre = c.Nombre,
                    Cantidad = c.Cantidad,
                    MinimoInventario = c.MinimoInventario
                })
                .ToListAsync();

            // ── Últimos movimientos de inventario registrados ───────────
            vm.UltimosMovimientos = await _context.MovimientosInventario
                .Include(m => m.Pallet!)
                    .ThenInclude(p => p.RecepcionDetalle!)
                        .ThenInclude(rd => rd.Componente)
                .Include(m => m.Ubicacion)
                .Include(m => m.Usuario)
                .OrderByDescending(m => m.FechaMovimiento)
                .Take(6)
                .Select(m => new MovimientoRecienteItem
                {
                    CodigoPallet = m.Pallet != null ? m.Pallet.CodigoPallet : "-",
                    Componente = m.Pallet != null && m.Pallet.RecepcionDetalle != null && m.Pallet.RecepcionDetalle.Componente != null
                        ? m.Pallet.RecepcionDetalle.Componente.Nombre
                        : null,
                    Ubicacion = m.Ubicacion != null ? m.Ubicacion.Codigo : null,
                    TipoMovimiento = m.TipoMovimiento,
                    FechaMovimiento = m.FechaMovimiento,
                    RealizadoPor = m.Usuario != null ? m.Usuario.NombreCompleto : null
                })
                .ToListAsync();

            // ── Recepciones que todavía requieren trabajo ───────────────
            vm.RecepcionesPendientes = await _context.Recepciones
                .Include(r => r.OrdenCompra)
                .Include(r => r.Detalles)
                .Where(r => r.Estado == "EnProceso")
                .OrderByDescending(r => r.FechaRecepcion)
                .Take(5)
                .Select(r => new RecepcionPendienteItem
                {
                    Id = r.Id,
                    NumeroOC = r.OrdenCompra != null ? r.OrdenCompra.NumeroOC : "-",
                    Estado = r.Estado,
                    FechaRecepcion = r.FechaRecepcion,
                    CantidadLotes = r.Detalles != null ? r.Detalles.Count : 0
                })
                .ToListAsync();

            return View("Index", vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
