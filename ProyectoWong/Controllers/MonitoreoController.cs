using Microsoft.AspNetCore.Mvc;

namespace ProyectoWong.Controllers
{
    public class MonitoreoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Produccion()
        {
            return View("MonitoreoProduccion");
        }
    }
}
