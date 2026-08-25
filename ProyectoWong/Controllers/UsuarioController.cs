using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models;

namespace ProyectoWong.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsuarioController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Usuarios";
            return View();
        }

        [HttpGet("consultar")]
        public async Task<IActionResult> Consultar()
        {
            try
            {
                var usuarios = await _context.Usuarios
                    .Select(u => new
                    {
                        expUsuarioId = u.ExpUsuarioId,
                        nombreCompleto = u.NombreCompleto,
                        email = u.Email,
                        telefono = u.Telefono,
                        activo = u.Activo
                    })
                    .ToListAsync();

                return Json(Respuesta.OK("Consulta exitosa", usuarios));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpGet("obtener/{id}")] // Nombre cambiado para coincidir con el JS
        public async Task<IActionResult> ObtenerUsuario(int id)
        {
            try
            {
                var usuario = await _context.Usuarios
                    .Select(u => new
                    {
                        expUsuarioId = u.ExpUsuarioId,
                        nombreCompleto = u.NombreCompleto,
                        email = u.Email,
                        telefono = u.Telefono,
                        activo = u.Activo
                    })
                    .FirstOrDefaultAsync(u => u.expUsuarioId == id);

                if (usuario == null)
                    return Json(Respuesta.Error("Usuario no encontrado"));

                return Json(Respuesta.OK("Usuario encontrado", usuario));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpPost("guardar")]
        public async Task<IActionResult> GuardarUsuario([FromForm] UsuarioViewModel model)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));

            try
            {
                // Si tiene ID, es EDICIÓN
                if (model.ExpUsuarioId > 0)
                {
                    var existente = await _context.Usuarios.FindAsync(model.ExpUsuarioId);
                    if (existente == null)
                        return Json(Respuesta.Error("No se encontró el registro"));

                    existente.NombreCompleto = model.NombreCompleto;
                    existente.Email = model.Email;
                    existente.Telefono = model.Telefono;
                    existente.Activo = model.Activo;

                    if (!string.IsNullOrWhiteSpace(model.Password))
                    {
                        existente.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                    }
                }
                // Si NO tiene ID, es CREACIÓN
                else
                {
                    if (string.IsNullOrWhiteSpace(model.Password))
                        return Json(Respuesta.Error("La contraseña es obligatoria para nuevos usuarios"));

                    var nuevo = new Usuarios
                    {
                        NombreCompleto = model.NombreCompleto,
                        Email = model.Email,
                        Telefono = model.Telefono,
                        Activo = model.Activo,
                        FechaAlta = DateTime.Now,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
                    };
                    _context.Usuarios.Add(nuevo);
                }

                await _context.SaveChangesAsync();
                return Json(Respuesta.OK(model.ExpUsuarioId > 0 ? "Usuario actualizado correctamente" : "Usuario registrado correctamente"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpDelete("eliminar/{id}")]
        public async Task<IActionResult> EliminarUsuario(int id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                    return Json(Respuesta.Error("No se encontró el registro"));

                // Borrado lógico (Soft Delete)
                usuario.Activo = false;
                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Usuario desactivado correctamente"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
    }
}