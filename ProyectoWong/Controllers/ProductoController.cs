using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models;

namespace ProyectoWong.Controllers
{
    [Route("Producto")]
    public class ProductoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductoController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Productos";
            return View();
        }

        [HttpGet("consultar")]
        public async Task<IActionResult> Consultar()
        {
            try
            {
                var productos = await _context.Productos
                    .Where(p => p.Activo)
                    .Select(p => new
                    {
                        id = p.Id,
                        nombre = p.Nombre,
                        descripcion = p.Descripcion,
                        precioBase = p.PrecioBase,
                        totalComponentesReceta = p.Componentes.Count,
                        activo = p.Activo
                    })
                    .ToListAsync();

                return Json(Respuesta.OK("Consulta exitosa", productos));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpGet("obtener/{id}")]
        public async Task<IActionResult> ObtenerProducto(int id)
        {
            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Componentes)
                        .ThenInclude(c => c.Componente)
                    .Include(p => p.EscalasDescuento)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (producto == null)
                    return Json(Respuesta.Error("Producto no encontrado"));

                // Con el stock actual, cuántas unidades del producto se alcanzan a armar
                int? unidadesProducibles = null;
                if (producto.Componentes.Any())
                {
                    unidadesProducibles = producto.Componentes
                        .Select(c => c.CantidadRequerida > 0 ? c.Componente.Cantidad / c.CantidadRequerida : 0)
                        .Min();
                }

                var dto = new
                {
                    id = producto.Id,
                    nombre = producto.Nombre,
                    descripcion = producto.Descripcion,
                    precioBase = producto.PrecioBase,
                    activo = producto.Activo,
                    unidadesProducibles = unidadesProducibles,
                    componentes = producto.Componentes.Select(c => new
                    {
                        id = c.Id,
                        componenteId = c.ComponenteId,
                        componenteNombre = c.Componente.Nombre,
                        stockDisponible = c.Componente.Cantidad,
                        cantidadRequerida = c.CantidadRequerida
                    }),
                    descuentos = producto.EscalasDescuento
                        .OrderBy(e => e.CantidadMinima)
                        .Select(e => new
                        {
                            id = e.Id,
                            cantidadMinima = e.CantidadMinima,
                            porcentajeDescuento = e.PorcentajeDescuento
                        })
                };

                return Json(Respuesta.OK("Producto encontrado", dto));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpPost("guardar")]
        public async Task<IActionResult> GuardarProducto([FromBody] ProductoViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Nombre))
                return Json(Respuesta.Error("El nombre del producto es obligatorio"));

            if (model.Componentes == null || !model.Componentes.Any())
                return Json(Respuesta.Error("Debe agregar al menos un componente a la receta (BOM)"));

            try
            {
                if (model.Id > 0)
                {
                    var existente = await _context.Productos
                        .Include(p => p.Componentes)
                        .Include(p => p.EscalasDescuento)
                        .FirstOrDefaultAsync(p => p.Id == model.Id);

                    if (existente == null)
                        return Json(Respuesta.Error("No se encontró el producto"));

                    existente.Nombre = model.Nombre;
                    existente.Descripcion = model.Descripcion;
                    existente.PrecioBase = model.PrecioBase;
                    existente.Activo = model.Activo;

                    // Reemplaza receta y descuentos por completo (simple, para este avance)
                    _context.ProductoComponentes.RemoveRange(existente.Componentes);
                    _context.EscalasDescuento.RemoveRange(existente.EscalasDescuento);

                    foreach (var c in model.Componentes)
                    {
                        existente.Componentes.Add(new ProductoComponente
                        {
                            ComponenteId = c.ComponenteId,
                            CantidadRequerida = c.CantidadRequerida
                        });
                    }

                    foreach (var d in model.Descuentos ?? new List<EscalaDescuentoInput>())
                    {
                        existente.EscalasDescuento.Add(new EscalaDescuento
                        {
                            CantidadMinima = d.CantidadMinima,
                            PorcentajeDescuento = d.PorcentajeDescuento
                        });
                    }
                }
                else
                {
                    var nuevo = new Producto
                    {
                        Nombre = model.Nombre,
                        Descripcion = model.Descripcion,
                        PrecioBase = model.PrecioBase,
                        Activo = true,
                        FechaAlta = DateTime.Now,
                        Componentes = model.Componentes.Select(c => new ProductoComponente
                        {
                            ComponenteId = c.ComponenteId,
                            CantidadRequerida = c.CantidadRequerida
                        }).ToList(),
                        EscalasDescuento = (model.Descuentos ?? new List<EscalaDescuentoInput>()).Select(d => new EscalaDescuento
                        {
                            CantidadMinima = d.CantidadMinima,
                            PorcentajeDescuento = d.PorcentajeDescuento
                        }).ToList()
                    };
                    _context.Productos.Add(nuevo);
                }

                await _context.SaveChangesAsync();
                return Json(Respuesta.OK(model.Id > 0 ? "Producto actualizado" : "Producto registrado"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpDelete("eliminar/{id}")]
        public async Task<IActionResult> EliminarProducto(int id)
        {
            try
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto == null)
                    return Json(Respuesta.Error("No se encontró el producto"));

                producto.Activo = false;
                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Producto desactivado"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ── Recibe un pedido del producto terminado: valida stock según la receta (BOM),
        // descuenta los componentes usados y calcula el precio con el descuento por volumen ──
        [HttpPost("registrar-pedido")]
        public async Task<IActionResult> RegistrarPedido([FromBody] PedidoProductoInput model)
        {
            if (model.Cantidad <= 0)
                return Json(Respuesta.Error("La cantidad pedida debe ser mayor a 0"));

            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Componentes)
                        .ThenInclude(c => c.Componente)
                    .Include(p => p.EscalasDescuento)
                    .FirstOrDefaultAsync(p => p.Id == model.ProductoId && p.Activo);

                if (producto == null)
                    return Json(Respuesta.Error("Producto no encontrado"));

                if (!producto.Componentes.Any())
                    return Json(Respuesta.Error("El producto no tiene una receta (BOM) configurada"));

                // 1) Validar que haya stock suficiente de TODOS los componentes antes de descontar nada
                var faltantes = new List<string>();
                foreach (var linea in producto.Componentes)
                {
                    var requerido = linea.CantidadRequerida * model.Cantidad;
                    if (linea.Componente.Cantidad < requerido)
                        faltantes.Add($"{linea.Componente.Nombre} (disponible: {linea.Componente.Cantidad}, requerido: {requerido})");
                }

                if (faltantes.Any())
                    return Json(Respuesta.Error("Stock insuficiente para: " + string.Join(", ", faltantes)));

                // 2) Descontar el inventario de cada componente según la receta
                foreach (var linea in producto.Componentes)
                {
                    linea.Componente.Cantidad -= linea.CantidadRequerida * model.Cantidad;
                }

                // 3) Calcular el precio aplicando la mejor escala de descuento por volumen alcanzada
                var escalaAplicable = producto.EscalasDescuento
                    .Where(e => model.Cantidad >= e.CantidadMinima)
                    .OrderByDescending(e => e.CantidadMinima)
                    .FirstOrDefault();

                var subtotal = producto.PrecioBase * model.Cantidad;
                var porcentajeDescuento = escalaAplicable?.PorcentajeDescuento ?? 0;
                var montoDescuento = subtotal * (porcentajeDescuento / 100m);
                var total = subtotal - montoDescuento;

                // Guarda en una sola operación: el descuento de inventario de todos los componentes
                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Pedido registrado, inventario actualizado", new
                {
                    productoNombre = producto.Nombre,
                    cantidad = model.Cantidad,
                    subtotal,
                    porcentajeDescuento,
                    montoDescuento,
                    total
                }));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
    }

    public class ProductoComponenteInput
    {
        public int ComponenteId { get; set; }
        public int CantidadRequerida { get; set; }
    }

    public class EscalaDescuentoInput
    {
        public int CantidadMinima { get; set; }
        public decimal PorcentajeDescuento { get; set; }
    }

    public class ProductoViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal PrecioBase { get; set; }
        public bool Activo { get; set; } = true;
        public List<ProductoComponenteInput> Componentes { get; set; } = new();
        public List<EscalaDescuentoInput>? Descuentos { get; set; }
    }

    public class PedidoProductoInput
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
    }
}
