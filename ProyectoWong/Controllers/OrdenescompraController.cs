using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models;
using ProyectoWong.Models.Recepcion;

namespace ProyectoWong.Controllers
{
    [Route("OrdenesCompra")]
    public class OrdenesCompraController : Controller
    {
        private readonly ApplicationDbContext _context;
        public OrdenesCompraController(ApplicationDbContext context) => _context = context;

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "OrdenesCompra";
            return View();
        }

        // ============ CONSULTAR ÓRDENES ============
        [HttpGet("consultar-ordenes")]
        public async Task<IActionResult> Consultar()
        {
            try
            {
                var ordenes = await _context.OrdenesCompra
                    .Include(o => o.Detalles)
                        .ThenInclude(d => d.Componente)
                    .Include(o => o.ProveedorNavigation)
                    .OrderByDescending(o => o.FechaCreacion)
                    .Select(o => new
                    {
                        id = o.Id,
                        numeroOC = o.NumeroOC,
                        proveedor = o.ProveedorNavigation != null ? o.ProveedorNavigation.Nombre : o.Proveedor,
                        estado = o.Estado,
                        fechaEsperada = o.FechaEsperada,
                        fechaCreacion = o.FechaCreacion,
                        totalComponentes = o.Detalles.Sum(d => d.CantidadEsperada),
                        totalDetalles = o.Detalles.Count
                    })
                    .ToListAsync();

                return Json(Respuesta.OK("Consulta exitosa", ordenes));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ============ OBTENER PRODUCTOS PARA EL MODAL ============
        [HttpGet("obtener-productos")]
        public async Task<IActionResult> ObtenerProductos()
        {
            try
            {
                var productos = await _context.Productos
                    .Where(p => p.Activo)
                    .Select(p => new
                    {
                        id = p.Id,
                        nombre = p.Nombre,
                        precioBase = p.PrecioBase
                    })
                    .ToListAsync();
                return Json(Respuesta.OK("OK", productos));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ============ CALCULAR DESGLOSE (LA MAGIA) ============
        // Este endpoint se llama vía AJAX cuando el usuario cambia la cantidad
        [HttpGet("calcular-desglose")]
        public async Task<IActionResult> CalcularDesglose(int productoId, int cantidad)
        {
            try
            {
                if (cantidad <= 0)
                    return Json(Respuesta.Error("La cantidad debe ser mayor a 0"));

                var producto = await _context.Productos
                    .Include(p => p.Componentes)
                        .ThenInclude(pc => pc.Componente)
                    .FirstOrDefaultAsync(p => p.Id == productoId);

                if (producto == null)
                    return Json(Respuesta.Error("Producto no encontrado"));

                // 1. Calcular descuento según la cantidad
                var escala = await _context.EscalasDescuento
                    .Where(e => e.ProductoId == productoId && e.CantidadMinima <= cantidad)
                    .OrderByDescending(e => e.CantidadMinima)
                    .FirstOrDefaultAsync();

                decimal porcentajeDescuento = escala?.PorcentajeDescuento ?? 0;
                decimal precioOriginal = producto.PrecioBase * cantidad;
                decimal descuento = precioOriginal * (porcentajeDescuento / 100);
                decimal totalFinal = precioOriginal - descuento;

                // 2. Calcular componentes necesarios
                var componentesNecesarios = producto.Componentes.Select(pc => new
                {
                    componenteId = pc.ComponenteId,
                    numeroPieza = pc.Componente.NumeroPieza,
                    nombre = pc.Componente.Nombre,
                    cantidadPorUnidad = pc.CantidadRequerida,
                    cantidadTotal = pc.CantidadRequerida * cantidad,
                    stockDisponible = pc.Componente.Cantidad,
                    stockSuficiente = pc.Componente.Cantidad >= (pc.CantidadRequerida * cantidad)
                }).ToList();

                return Json(Respuesta.OK("OK", new
                {
                    productoNombre = producto.Nombre,
                    precioUnitario = producto.PrecioBase,
                    cantidad,
                    porcentajeDescuento,
                    precioOriginal,
                    descuento,
                    totalFinal,
                    componentes = componentesNecesarios
                }));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ============ GUARDAR ORDEN DE COMPRA ============
        [HttpPost("guardar-orden")]
        public async Task<IActionResult> GuardarOrden([FromBody] OrdenCompraRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (request == null || request.ProductoId <= 0 || request.Cantidad <= 0)
                    return Json(Respuesta.Error("Datos inválidos"));

                // 1. Crear la orden de compra
                var orden = new OrdenCompra
                {
                    NumeroOC = $"OC-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}",
                    ProveedorId = request.ProveedorId,
                    Estado = "Pendiente",
                    FechaEsperada = request.FechaEsperada,
                    FechaCreacion = DateTime.Now
                };
                _context.OrdenesCompra.Add(orden);
                await _context.SaveChangesAsync();

                // 2. Obtener los componentes del producto
                var componentesProducto = await _context.ProductoComponentes
                    .Where(pc => pc.ProductoId == request.ProductoId)
                    .ToListAsync();

                // 3. Crear un detalle por cada componente necesario
                foreach (var pc in componentesProducto)
                {
                    var detalle = new OrdenCompraDetalle
                    {
                        OrdenCmpraId = orden.Id,
                        ComponenteId = pc.ComponenteId,
                        CantidadEsperada = pc.CantidadRequerida * request.Cantidad
                    };
                    _context.OrdenCompraDetalles.Add(detalle);
                }
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return Json(Respuesta.OK($"Orden {orden.NumeroOC} creada exitosamente"));
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ============ ELIMINAR ORDEN ============
        [HttpDelete("eliminar-orden/{id}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            try
            {
                var orden = await _context.OrdenesCompra.FindAsync(id);
                if (orden == null) return Json(Respuesta.Error("Orden no encontrada"));
                _context.OrdenesCompra.Remove(orden);
                await _context.SaveChangesAsync();
                return Json(Respuesta.OK("Orden eliminada"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
    }

    // DTO para recibir el JSON del frontend
    public class OrdenCompraRequest
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public int ProveedorId { get; set; }
        public DateTime? FechaEsperada { get; set; }
    }
}