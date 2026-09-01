using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Models;

namespace ProyectoWong.Controllers
{
    public class LoginController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoginController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }



        [HttpPost]
        public IActionResult Login(Login model)
        {
            if (ModelState.IsValid)
            {
                
                    var usuario = _context.Usuarios.FirstOrDefault(u => u.Email == model.Username && u.Activo);

                if (usuario != null && usuario.PasswordHash != null && BCrypt.Net.BCrypt.Verify(model.Password, usuario.PasswordHash))
                {

                    // Redirigir a la página principal si es exitoso
                    return RedirectToAction("Index", "Home");

                    // Si falla, agregamos un error al modelo
                    ModelState.AddModelError("", "Usuario o contraseña incorrectos");
                }

                // Si hay errores de validación, volvemos a mostrar la vista con los datos
                return View(model);
            }
            return View(model);
        }

        public IActionResult Logout()
        {
            // TODO: cuando exista sesión/cookie de autenticación real, limpiarla aquí.
            return RedirectToAction("Index", "Login");
        }

    }
}
