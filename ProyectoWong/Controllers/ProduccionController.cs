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
        // 1. INICIAR (Reinicia contadores de pausa)
        [HttpPost("iniciar/{id}")]
        public async Task<IActionResult> Iniciar(int id)
        {
            var orden = await _context.OrdenProduccion.FirstOrDefaultAsync(o => o.Id == id);
            if (orden == null || orden.Estado != "Pendiente") return Json(Respuesta.Error("No se puede iniciar esta orden"));

            orden.Estado = "EnProceso";
            orden.FechaInicio = DateTime.Now;
            orden.FechaPausa = null;
            orden.TiempoPausadoMinutos = 0;
            orden.DuracionEstimadaMinutos = orden.CantidadAProducir * 0.05; // 0.5 min por unidad

            await _context.SaveChangesAsync();
            return Json(Respuesta.OK($"Orden iniciada. Tiempo estimado: {orden.DuracionEstimadaMinutos} min"));
        }

        // 2. PAUSAR
        [HttpPost("pausar/{id}")]
        public async Task<IActionResult> Pausar(int id)
        {
            var orden = await _context.OrdenProduccion.FirstOrDefaultAsync(o => o.Id == id);
            if (orden == null || orden.Estado != "EnProceso") return Json(Respuesta.Error("No se puede pausar esta orden"));

            orden.Estado = "Pausada";
            orden.FechaPausa = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(Respuesta.OK("Producción pausada"));
        }

        // 3. REANUDAR
        [HttpPost("reanudar/{id}")]
        public async Task<IActionResult> Reanudar(int id)
        {
            var orden = await _context.OrdenProduccion.FirstOrDefaultAsync(o => o.Id == id);
            if (orden == null || orden.Estado != "Pausada" || !orden.FechaPausa.HasValue)
                return Json(Respuesta.Error("No se puede reanudar esta orden"));

            // Calcular cuánto tiempo estuvo pausada y sumarlo al acumulado
            var minutosPausa = (DateTime.Now - orden.FechaPausa.Value).TotalMinutes;
            orden.TiempoPausadoMinutos += minutosPausa;

            orden.Estado = "EnProceso";
            orden.FechaPausa = null;
            await _context.SaveChangesAsync();
            return Json(Respuesta.OK("Producción reanudada"));
        }

        // 4. CONSULTAR PROGRESO (Matemática corregida para soportar pausas)
        [HttpGet("progreso/{id}")]
        public async Task<IActionResult> ConsultarProgreso(int id)
        {
            var orden = await _context.OrdenProduccion
                .Include(o => o.Producto)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (orden == null) return Json(Respuesta.Error("Orden no encontrada"));

            double minutosTrabajados = 0;
            double tiempoRestanteMin = 0;

            if (orden.Estado == "EnProceso" && orden.FechaInicio.HasValue)
            {
                minutosTrabajados = (DateTime.Now - orden.FechaInicio.Value).TotalMinutes - orden.TiempoPausadoMinutos;
            }
            else if (orden.Estado == "Pausada" && orden.FechaInicio.HasValue && orden.FechaPausa.HasValue)
            {
                minutosTrabajados = (orden.FechaPausa.Value - orden.FechaInicio.Value).TotalMinutes - orden.TiempoPausadoMinutos;
            }
            else if (orden.Estado == "Completada")
            {
                minutosTrabajados = orden.DuracionEstimadaMinutos;
            }

            double progreso = Math.Min(100, Math.Max(0, (minutosTrabajados / orden.DuracionEstimadaMinutos) * 100));
            tiempoRestanteMin = Math.Max(0, orden.DuracionEstimadaMinutos - minutosTrabajados);

            int totalPasos = 7;
            int pasoActual = (int)Math.Ceiling((progreso / 100) * totalPasos);
            pasoActual = Math.Clamp(pasoActual, 1, totalPasos);

            return Json(Respuesta.OK("OK", new
            {
                ordenId = orden.Id,
                numeroOP = orden.NumeroOP,
                productoNombre = orden.Producto.Nombre,
                productoImagen = orden.Producto.ImagenUrl ?? "https://share.google/IBaGOU8Jg6V5NDVzE",
                progreso = Math.Round(progreso, 1),
                pasoActual,
                estadoSimulacion = orden.Estado,
                tiempoRestanteTexto = orden.Estado == "Completada" ? "Finalizado" : $"{(int)tiempoRestanteMin}m {Math.Round((tiempoRestanteMin % 1) * 60)}s"
            }));
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

                // Validar que haya stock suficiente de cada componente.
                // Revisamos TODOS los componentes (no solo el primero que falle) para
                // poder devolver la lista completa de faltantes y así el frontend pueda
                // ofrecer generar automáticamente la orden de compra correspondiente.
                var componentesFaltantes = new List<object>();
                foreach (var pc in producto.Componentes)
                {
                    int requerido = pc.CantidadRequerida * request.Cantidad;
                    int disponible = pc.Componente.Cantidad;
                    if (disponible < requerido)
                    {
                        componentesFaltantes.Add(new
                        {
                            componenteId = pc.ComponenteId,
                            nombre = pc.Componente.Nombre,
                            disponible,
                            requerido,
                            faltante = requerido - disponible
                        });
                    }
                }

                if (componentesFaltantes.Any())
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "Stock insuficiente para fabricar este producto.",
                        stockInsuficiente = true,
                        productoId = producto.Id,
                        productoNombre = producto.Nombre,
                        cantidad = request.Cantidad,
                        componentesFaltantes
                    });
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
                if (orden.Estado != "Pendiente" && orden.Estado != "EnProceso" && orden.Estado != "Pausada")
                    return Json(Respuesta.Error("La orden no puede ser completada en su estado actual"));

                // 1. Descontar componentes del inventario
                foreach (var detalle in orden.Detalles)
                {
                    detalle.Componente.Cantidad -= detalle.CantidadRequerida;
                    detalle.CantidadConsumida = detalle.CantidadRequerida;
                }

                // 2. Aumentar stock del producto terminado
                orden.Producto.Cantidad += orden.CantidadAProducir;

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