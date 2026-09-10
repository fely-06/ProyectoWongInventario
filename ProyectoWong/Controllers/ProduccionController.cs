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
                    NumeroOP = $"WO-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}",
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
                orden.Producto!.Cantidad += orden.CantidadAProducir;

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

        // 6. SIMULACIÓN RÁPIDA DE PRODUCCIÓN
        // Crea y completa una OP en un solo paso, y devuelve el detalle de
        // consumo de componentes y stock antes/después. Pensado para poder
        // correr una corrida de producción de prueba (ej. el ventilador
        // portátil: motor, aspas, case y batería) sin pasar por el flujo
        // manual de dos pasos (generar-orden + completar).
        [HttpPost("simular")]
        public async Task<IActionResult> Simular([FromBody] SimularProduccionRequest request)
        {
            if (request.Cantidad <= 0)
                return Json(Respuesta.Error("La cantidad a producir debe ser mayor a cero"));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Componentes).ThenInclude(pc => pc.Componente)
                    .FirstOrDefaultAsync(p => p.Id == request.ProductoId);

                if (producto == null) return Json(Respuesta.Error("Producto no encontrado"));
                if (producto.Componentes == null || !producto.Componentes.Any())
                    return Json(Respuesta.Error("El producto no tiene una receta de componentes definida"));

                // 1. Validar stock suficiente de cada componente antes de tocar nada
                var faltantes = new List<string>();
                foreach (var pc in producto.Componentes)
                {
                    int requerido = pc.CantidadRequerida * request.Cantidad;
                    if (pc.Componente.Cantidad < requerido)
                        faltantes.Add($"{pc.Componente.Nombre} (disponible: {pc.Componente.Cantidad}, requerido: {requerido})");
                }
                if (faltantes.Any())
                    return Json(Respuesta.Error($"Stock insuficiente para simular la producción: {string.Join("; ", faltantes)}"));

                // 2. Crear la OP (cabecera + receta)
                var orden = new OrdenProduccion
                {
                    NumeroOP = $"WO-SIM-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}",
                    ProductoId = producto.Id,
                    CantidadAProducir = request.Cantidad,
                    Estado = "Pendiente",
                    FechaCreacion = DateTime.Now,
                    FechaInicio = DateTime.Now
                };
                _context.OrdenProduccion.Add(orden);
                await _context.SaveChangesAsync();

                var detalleSimulacion = new List<object>();

                foreach (var pc in producto.Componentes)
                {
                    int requerido = pc.CantidadRequerida * request.Cantidad;
                    int stockAntes = pc.Componente.Cantidad;

                    _context.OrdenProduccionDetalle.Add(new OrdenProduccionDetalle
                    {
                        OrdenProduccionId = orden.Id,
                        ComponenteId = pc.ComponenteId,
                        CantidadRequerida = requerido,
                        CantidadConsumida = requerido
                    });

                    // 3. Descontar el componente del inventario
                    pc.Componente.Cantidad -= requerido;

                    detalleSimulacion.Add(new
                    {
                        componente = pc.Componente.Nombre,
                        cantidadPorUnidad = pc.CantidadRequerida,
                        consumidoTotal = requerido,
                        stockAntes,
                        stockDespues = pc.Componente.Cantidad
                    });
                }

                // 4. Sumar el producto terminado al inventario
                int stockProductoAntes = producto.Cantidad;
                producto.Cantidad += request.Cantidad;

                // 5. Cerrar la OP
                orden.Estado = "Completada";
                orden.FechaFin = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var resultado = new
                {
                    numeroOP = orden.NumeroOP,
                    producto = producto.Nombre,
                    unidadesProducidas = request.Cantidad,
                    stockProductoAntes,
                    stockProductoDespues = producto.Cantidad,
                    componentesConsumidos = detalleSimulacion
                };

                return Json(Respuesta.OK($"Simulación completada: {request.Cantidad} unidad(es) de '{producto.Nombre}' producidas", resultado));
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

    // DTO para la simulación rápida de producción
    public class SimularProduccionRequest
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
    }
}