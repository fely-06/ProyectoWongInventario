using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models.Produccion;

namespace ProyectoWong.Controllers
{
    [Route("Produccion")]
    public class ProduccionController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ProduccionController(ApplicationDbContext context) => _context = context;

        // 1. VISTA PRINCIPAL
        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Produccion";
            return View("Index", "Produccion");
        }

        // 2. OBTENER PRODUCTOS (Para el select del modal)
        [HttpGet("obtener-productos")]
        public async Task<IActionResult> ObtenerProductos()
        {
            var productos = await _context.Productos
                .Where(p => p.Activo)
                .Select(p => new { id = p.Id, nombre = p.Nombre })
                .ToListAsync();
            return Json(Respuesta.OK("OK", productos));
        }

        // 3. CONSULTAR ÓRDENES DE PRODUCCIÓN
        [HttpGet("consultar-ordenes")]
        public async Task<IActionResult> ConsultarOrdenes()
        {
            var ordenes = await _context.OrdenProduccion
                .Include(o => o.Producto)
                .Include(o => o.OrdenCompra)
                .OrderByDescending(o => o.FechaCreacion)
                .Select(o => new
                {
                    id = o.Id,
                    numeroOP = o.NumeroOP,
                    producto = o.Producto.Nombre,
                    cantidad = o.CantidadAProducir,
                    estado = o.Estado,
                    fechaCreacion = o.FechaCreacion,
                    numeroOC = o.OrdenCompra != null ? o.OrdenCompra.NumeroOC : "Manual"
                })
                .ToListAsync();

            return Json(Respuesta.OK("Consulta exitosa", ordenes));
        }

        // 4. GENERAR NUEVA ORDEN DE PRODUCCIÓN
        [HttpPost("generar-orden")]
        public async Task<IActionResult> GenerarOrden([FromBody] GenerarOPRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Componentes).ThenInclude(pc => pc.Componente)
                    .FirstOrDefaultAsync(p => p.Id == request.ProductoId);

                if (producto == null) return Json(Respuesta.Error("Producto no encontrado"));

                // Validar que haya stock suficiente de cada componente
                foreach (var pc in producto.Componentes)
                {
                    int requerido = pc.CantidadRequerida * request.Cantidad;
                    if (pc.Componente.Cantidad < requerido)
                        return Json(Respuesta.Error($"Stock insuficiente de '{pc.Componente.Nombre}'. Disponible: {pc.Componente.Cantidad}, Requerido: {requerido}"));
                }

                // Crear la cabecera
                var orden = new OrdenProduccion
                {
                    NumeroOP = $"OP-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}",
                    ProductoId = request.ProductoId,
                    CantidadAProducir = request.Cantidad,
                    Estado = "Pendiente",
                    FechaCreacion = DateTime.Now,
                    OrdenCompraId = request.OrdenCompraId // Opcional: para trazabilidad
                };
                _context.OrdenProduccion.Add(orden);
                await _context.SaveChangesAsync();

                // Crear los detalles (la receta)
                foreach (var pc in producto.Componentes)
                {
                    _context.OrdenProduccionDetalle.Add(new OrdenProduccionDetalle
                    {
                        OrdenProduccionId = orden.Id,
                        ComponenteId = pc.ComponenteId,
                        CantidadRequerida = pc.CantidadRequerida * request.Cantidad
                    });
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(Respuesta.OK($"Orden {orden.NumeroOP} creada exitosamente"));
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                return Json(Respuesta.Error(e.Message));
            }
        }

        // 5. COMPLETAR LA PRODUCCIÓN (Descuenta componentes, suma producto final)
        [HttpPost("completar/{id}")]
        public async Task<IActionResult> Completar(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var orden = await _context.OrdenProduccion
                    .Include(o => o.Producto)
                    .Include(o => o.Detalles).ThenInclude(d => d.Componente)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orden == null) return Json(Respuesta.Error("Orden no encontrada"));
                if (orden.Estado != "Pendiente" && orden.Estado != "EnProceso")
                    return Json(Respuesta.Error("La orden no puede ser completada en su estado actual"));

                // 1. Descontar componentes del inventario
                foreach (var detalle in orden.Detalles)
                {
                    detalle.Componente.Cantidad -= detalle.CantidadRequerida;
                    detalle.CantidadConsumida = detalle.CantidadRequerida;
                }

                // 2. Aumentar stock del producto terminado
                //orden.Producto.Cantidad += orden.CantidadAProducir;

                // 3. Actualizar estado y fechas
                orden.Estado = "Completada";
                orden.FechaFin = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(Respuesta.OK("Producción completada e inventario actualizado"));
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                return Json(Respuesta.Error(e.Message));
            }
        }
    }

    // DTO para recibir los datos del frontend
    public class GenerarOPRequest
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public int? OrdenCompraId { get; set; } // Opcional, por si quieres vincularla manualmente
    }
}