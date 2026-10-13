using System.IO.Ports;
using Microsoft.AspNetCore.Mvc;
using ProyectoWong.Helpers;

namespace ProyectoWong.Controllers
{
    public class PruebasSensoresController : Controller
    {
        private readonly SensorDataStore _store;

        public PruebasSensoresController(SensorDataStore store)
        {
            _store = store;
        }

        // Vista del apartado de pruebas
        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "PruebasSensores";
            return View();
        }

        // Endpoint JSON que consulta la vista cada pocos segundos
        [HttpGet("PruebasSensores/Ultimos")]
        public IActionResult Ultimos()
        {
            var (ultima, historial, estado, conectado, puerto) = _store.Obtener();

            string[] puertos;
            try { puertos = SerialPort.GetPortNames(); }
            catch { puertos = Array.Empty<string>(); }

            return Json(new
            {
                conectado,
                puerto,
                estado,
                puertosDisponibles = puertos,
                ultima,
                historial
            });
        }

        [HttpPost("PruebasSensores/Limpiar")]
        public IActionResult Limpiar()
        {
            _store.Limpiar();
            return Json(new { success = true });
        }
    }
}
