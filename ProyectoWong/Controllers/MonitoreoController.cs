using Microsoft.AspNetCore.Mvc;

namespace ProyectoWong.Controllers
{
    public class MonitoreoController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Monitoreo";
            return View();
        }

       
    }
}
