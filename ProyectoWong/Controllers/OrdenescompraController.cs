using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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
        
        [HttpGet("buscar-por-numero/{numero}")]
        public async Task<IActionResult> BuscarPorNumero(string numero)
        {
            try
            {
                var oc = await _context.OrdenesCompra
                    .Include(o => o.Detalles).ThenInclude(d => d.Componente)
                    .Include(o => o.ProveedorNavigation)
                    .FirstOrDefaultAsync(o => o.NumeroOC == numero);

                if (oc == null)
                    return Json(Respuesta.Error("Orden de compra no encontrada"));

                // Obtener cantidades ya recibidas y APROBADAS históricamente para esta OC
                var recibidosAprobados = await _context.RecepcionDetalles
                    .Where(rd => rd.Recepcion.OrdenCompraId == oc.Id && rd.Estado == "Aprobado")
                    .GroupBy(rd => rd.ComponenteId)
                    .Select(g => new { ComponenteId = g.Key, Total = g.Sum(x => x.CantidadRecibida) })
                    .ToListAsync();

                var resultado = new
                {
                    id = oc.Id,
                    numeroOC = oc.NumeroOC,
                    estado = oc.Estado,
                    detalles = oc.Detalles.Select(d => new
                    {
                        componenteId = d.ComponenteId,
                        componenteNombre = d.Componente.Nombre,
                        numeroPieza = d.Componente.NumeroPieza,
                        cantidadEsperada = d.CantidadEsperada,
                        // NUEVO: Enviamos el histórico real al frontend
                        cantidadYaRecibida = recibidosAprobados.FirstOrDefault(r => r.ComponenteId == d.ComponenteId)?.Total ?? 0
                    }).ToList()
                };

                return Json(Respuesta.OK("Orden encontrada", resultado));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
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
                        proveedor = o.ProveedorNavigation != null ? o.ProveedorNavigation.Nombre : "Sin proveedor",
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
                decimal precioVentaOriginal = producto.PrecioBase * cantidad;
                decimal descuento = precioVentaOriginal * (porcentajeDescuento / 100);
                decimal totalFinal = precioVentaOriginal - descuento;

                // 2. Calcular componentes: Requerido vs Inventario vs Faltante
                var componentesNecesarios = producto.Componentes.Select(pc =>
                {
                    int cantidadRequerida = pc.CantidadRequerida * cantidad;
                    int stockDisponible = pc.Componente?.Cantidad ?? 0;
                    int cantidadFaltante = Math.Max(0, cantidadRequerida - stockDisponible);

                    return new
                    {
                        componenteId = pc.ComponenteId,
                        numeroPieza = pc.Componente.NumeroPieza,
                        nombre = pc.Componente.Nombre,
                        cantidadPorUnidad = pc.CantidadRequerida,
                        cantidadRequerida = cantidadRequerida,   // Total que pide la receta
                        stockDisponible = stockDisponible,       // Lo que hay en almacén
                        cantidadFaltante = cantidadFaltante,     // Lo que realmente hay que comprar
                        stockSuficiente = cantidadFaltante == 0,  // True si no hay que comprar nada
                        precio = pc.Componente.Precio

                    };
                }).ToList();
                // Dentro de CalcularDesglose, después del foreach de componentes:

                var totalComponentesFaltantes = componentesNecesarios
                    .Where(c => c.cantidadFaltante > 0)
                    .Sum(c => c.cantidadFaltante * c.precio); // <-- Necesitarás incluir el precio

                return Json(Respuesta.OK("OK", new
                {
                    productoNombre = producto.Nombre,
                    cantidadProduccion = cantidad,
                    componentes = componentesNecesarios,
                    // NUEVO: Costo real de lo que se va a comprar
                    costoTotalCompra = totalComponentesFaltantes,
                    // Opcional: mantener el precio del producto final como referencia
                    precioVentaUnitario = producto.PrecioBase,
                    totalVenta = totalFinal, 
                    precioVentaOriginal
                }));
                //return Json(Respuesta.OK("OK", new
                //{
                //    productoNombre = producto.Nombre,
                //    precioUnitario = producto.PrecioBase,
                //    cantidad,
                //    porcentajeDescuento,
                //    precioOriginal,
                //    descuento,
                //    totalFinal,
                //    componentes = componentesNecesarios
                //}));
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

                // 2. Obtener los componentes del producto INCLUYENDO los datos del componente para leer el stock
                var componentesProducto = await _context.ProductoComponentes
                    .Include(pc => pc.Componente) // <-- ESTO ES NUEVO Y NECESARIO
                    .Where(pc => pc.ProductoId == request.ProductoId)
                    .ToListAsync();

                bool hayAlMenosUnFaltante = false;

                // 3. Crear un detalle SOLO por los componentes que faltan
                foreach (var pc in componentesProducto)
                {
                    int cantidadRequerida = pc.CantidadRequerida * request.Cantidad;
                    int stockDisponible = pc.Componente?.Cantidad ?? 0;
                    int cantidadFaltante = Math.Max(0, cantidadRequerida - stockDisponible);

                    if (cantidadFaltante > 0)
                    {
                        hayAlMenosUnFaltante = true;
                        var detalle = new OrdenCompraDetalle
                        {
                            OrdenCmpraId = orden.Id,
                            ComponenteId = pc.ComponenteId,
                            CantidadEsperada = cantidadFaltante // <-- GUARDAMOS SOLO LO QUE FALTA
                        };
                        _context.OrdenCompraDetalle.Add(detalle);
                    }
                }

                if (!hayAlMenosUnFaltante)
                {
                    await transaction.RollbackAsync();
                    return Json(Respuesta.Error("No se requiere compra: el inventario es suficiente para esta producción."));
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(Respuesta.OK($"Orden {orden.NumeroOC} creada exitosamente solo con componentes faltantes."));
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                var errorMessage = e.Message;
                if (e.InnerException != null)
                {
                    errorMessage += $"\nInner: {e.InnerException.Message}";
                    if (e.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx)
                    {
                        errorMessage += $"\nSQL Error: {sqlEx.Number}";
                        foreach (SqlError err in sqlEx.Errors)
                        {
                            errorMessage += $"\n- {err.Message}";
                        }
                    }
                }
                return Json(Respuesta.Error(errorMessage));
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