using Microsoft.AspNetCore.Mvc;
using ProyectoWong.Models;

namespace ProyectoWong.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

       
        [HttpPost]
        public IActionResult Login(Login model)
        {
            if (ModelState.IsValid)
            {
                if (model.Username == "admin" && model.Password == "1234")
                {
                    // Redirigir a la página principal si es exitoso
                    return RedirectToAction("Index", "Home");
                }

                // Si falla, agregamos un error al modelo
                ModelState.AddModelError("", "Usuario o contraseña incorrectos");
            }

            // Si hay errores de validación, volvemos a mostrar la vista con los datos
            return View(model);
        }

    }
}
