using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models;

namespace ProyectoWong.Controllers
{
    [Route("Proveedor")]
    public class ProveedorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProveedorController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Proveedores";
            return View();
        }

        [HttpGet("consultar-proveedores")]
        public async Task<IActionResult> Consultar()
        {
            try
            {
                var proveedores = await _context.Proveedores
                    .Where(p => p.Activo)
                    .Select(p => new
                    {
                        id = p.Id,
                        nombre = p.Nombre,
                        telefono = p.Telefono,
                        correo = p.Correo,
                        activo = p.Activo
                    })
                    .ToListAsync();

                return Json(Respuesta.OK("Consulta exitosa", proveedores));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpGet("obtener-proveedor/{id}")]
        public async Task<IActionResult> ObtenerProveedor(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(id);

                if (proveedor == null)
                    return Json(Respuesta.Error("Proveedor no encontrado"));

                var dto = new
                {
                    id = proveedor.Id,
                    nombre = proveedor.Nombre,
                    telefono = proveedor.Telefono,
                    correo = proveedor.Correo,
                    activo = proveedor.Activo
                };

                return Json(Respuesta.OK("Proveedor encontrado", dto));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpPost("guardar-proveedor")]
        public async Task<IActionResult> GuardarProveedor([FromForm] ProveedorViewModel model)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));

            try
            {
                if (model.Id > 0)
                {
                    // EDICIÓN
                    var existente = await _context.Proveedores.FindAsync(model.Id);
                    if (existente == null)
                        return Json(Respuesta.Error("No se encontró el proveedor"));

                    existente.Nombre = model.Nombre;
                    existente.Telefono = model.Telefono;
                    existente.Correo = model.Correo;
                    existente.Activo = model.Activo;
                }
                else
                {
                    // CREACIÓN
                    var nuevo = new Proveedor
                    {
                        Nombre = model.Nombre,
                        Telefono = model.Telefono,
                        Correo = model.Correo,
                        Activo = true
                    };
                    _context.Proveedores.Add(nuevo);
                }

                await _context.SaveChangesAsync();
                return Json(Respuesta.OK(model.Id > 0 ? "Proveedor actualizado" : "Proveedor registrado"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpDelete("eliminar-proveedor/{id}")]
        public async Task<IActionResult> EliminarProveedor(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(id);
                if (proveedor == null)
                    return Json(Respuesta.Error("No se encontró el proveedor"));

                proveedor.Activo = false;
                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Proveedor desactivado"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
    }
}