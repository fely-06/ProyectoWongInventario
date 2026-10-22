using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoWong.Data;
using ProyectoWong.Models;

namespace ProyectoWong.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoginController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? returnUrl = null)
        {
            // Si ya hay sesión, no tiene caso volver a pedir credenciales
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(Login model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (ModelState.IsValid)
            {
                var usuario = _context.Usuarios.FirstOrDefault(u => u.Email == model.Username && u.Activo);

                if (usuario != null && usuario.PasswordHash != null && BCrypt.Net.BCrypt.Verify(model.Password, usuario.PasswordHash))
                {
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, usuario.ExpUsuarioId.ToString()),
                        new(ClaimTypes.Name, usuario.NombreCompleto),
                        new(ClaimTypes.Email, usuario.Email)
                    };
                    var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));

                    // Solo se regresa a URLs del propio sitio (evita redirecciones abiertas)
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return LocalRedirect(returnUrl);

                    // Redirigir a la página principal si es exitoso
                    return RedirectToAction("Index", "Home");
                }
                // Si falla, agregamos un error al modelo
                ModelState.AddModelError("", "Usuario o contraseña incorrectos");
            }

            // Si hay errores de validación, volvemos a mostrar la vista con los datos
            return View("Index", model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Login");
        }
    }
}
