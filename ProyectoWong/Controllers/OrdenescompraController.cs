using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models;
using ProyectoWong.Models.Recepcion;

namespace ProyectoWong.Controllers
{
    [Route("OrdenCompra")]
    public class OrdenCompraController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdenCompraController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "OrdenesCompra";
            return View();
        }

        [HttpGet("consultar-ordenes")]
        public async Task<IActionResult> Consultar()
        {
            try
            {
                var ordenes = await _context.OrdenesCompra
                    .Include(o => o.Proveedor)
                    .Select(o => new
                    {
                        id = o.Id,
                        numeroOC = o.NumeroOC,
                        proveedorId = o.ProveedorId,
                        proveedorNombre = o.Proveedor != null ? o.Proveedor : null,
                        estado = o.Estado,
                        fechaEsperada = o.FechaEsperada,
                        fechaCreacion = o.FechaCreacion
                    })
                    .ToListAsync();

                return Json(Respuesta.OK("Consulta exitosa", ordenes));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpGet("obtener-orden/{id}")]
        public async Task<IActionResult> ObtenerOrden(int id)
        {
            try
            {
                var orden = await _context.OrdenesCompra
                    .Include(o => o.Detalles)
                    .ThenInclude(d => d.Componente)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orden == null)
                    return Json(Respuesta.Error("Orden de compra no encontrada"));

                var dto = new
                {
                    id = orden.Id,
                    numeroOC = orden.NumeroOC,
                    proveedorId = orden.ProveedorId,
                    estado = orden.Estado,
                    fechaEsperada = orden.FechaEsperada?.ToString("yyyy-MM-dd"),
                    detalles = orden.Detalles?.Select(d => new
                    {
                        id = d.Id,
                        componenteId = d.ComponenteId,
                        componenteNombre = d.Componente != null ? d.Componente.Nombre : null,
                        cantidadEsperada = d.CantidadEsperada
                    })
                };

                return Json(Respuesta.OK("Orden encontrada", dto));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpPost("guardar-orden")]
        public async Task<IActionResult> GuardarOrden([FromBody] OrdenCompraViewModel model)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));

            if (model.Detalles == null || !model.Detalles.Any())
                return Json(Respuesta.Error("Debe agregar al menos un componente a la orden"));

            try
            {
                if (model.Id > 0)
                {
                    // EDICIÓN de cabecera
                    var existente = await _context.OrdenesCompra
                        .Include(o => o.Detalles)
                        .FirstOrDefaultAsync(o => o.Id == model.Id);

                    if (existente == null)
                        return Json(Respuesta.Error("No se encontró la orden de compra"));

                    existente.NumeroOC = model.NumeroOC;
                    existente.ProveedorId = model.ProveedorId;
                    existente.Estado = model.Estado;
                    existente.FechaEsperada = model.FechaEsperada;

                    // Reemplaza el detalle completo (simple, para este avance)
                    _context.OrdenCompraDetalles.RemoveRange(existente.Detalles ?? new List<OrdenCompraDetalle>());
                    foreach (var d in model.Detalles)
                    {
                        existente.Detalles!.Add(new OrdenCompraDetalle
                        {
                            ComponenteId = d.ComponenteId,
                            CantidadEsperada = d.CantidadEsperada
                        });
                    }
                }
                else
                {
                    // CREACIÓN
                    var nueva = new OrdenCompra
                    {
                        NumeroOC = model.NumeroOC,
                        ProveedorId = model.ProveedorId,
                        Estado = model.Estado,
                        FechaEsperada = model.FechaEsperada,
                        FechaCreacion = DateTime.Now,
                        Detalles = model.Detalles.Select(d => new OrdenCompraDetalle
                        {
                            ComponenteId = d.ComponenteId,
                            CantidadEsperada = d.CantidadEsperada
                        }).ToList()
                    };
                    _context.OrdenesCompra.Add(nueva);
                }

                await _context.SaveChangesAsync();
                return Json(Respuesta.OK(model.Id > 0 ? "Orden actualizada" : "Orden registrada"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpGet("buscar-por-numero/{numeroOC}")]
        public async Task<IActionResult> BuscarPorNumero(string numeroOC)
        {
            // Usado en el flujo de Recepción -> paso "SCAN PO"
            try
            {
                var orden = await _context.OrdenesCompra
                    .Include(o => o.Detalles)
                    .ThenInclude(d => d.Componente)
                    .FirstOrDefaultAsync(o => o.NumeroOC == numeroOC);

                if (orden == null)
                    return Json(Respuesta.Error("No se encontró una orden de compra con ese número"));

                if (orden.Estado == "Cerrada" || orden.Estado == "Cancelada")
                    return Json(Respuesta.Error($"La orden de compra está {orden.Estado.ToLower()} y no puede recibirse"));

                var dto = new
                {
                    id = orden.Id,
                    numeroOC = orden.NumeroOC,
                    estado = orden.Estado,
                    detalles = orden.Detalles?.Select(d => new
                    {
                        componenteId = d.ComponenteId,
                        componenteNombre = d.Componente != null ? d.Componente.Nombre : null,
                        numeroPieza = d.Componente != null ? d.Componente.NumeroPieza : null,
                        cantidadEsperada = d.CantidadEsperada
                    })
                };

                return Json(Respuesta.OK("Orden de compra encontrada", dto));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
    }
}