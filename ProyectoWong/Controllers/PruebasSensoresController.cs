using System.IO.Ports;
using Microsoft.AspNetCore.Mvc;
using ProyectoWong.Helpers;

namespace ProyectoWong.Controllers
{
    public class PruebasSensoresController : Controller
    {
        private readonly SensorDataStore _store;
        private readonly SensorSerialService _serialService; // <-- Inyectado correctamente

        public PruebasSensoresController(SensorDataStore store, SensorSerialService serialService)
        {
            _store = store;
            _serialService = serialService;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "PruebasSensores";
            return View();
        }

        [HttpGet("PruebasSensores/Ultimos")]
        public IActionResult Ultimos()
        {
            var (ultima, historial, estado, conectado, puerto) = _store.Obtener();
            string[] puertos;
            try { puertos = SerialPort.GetPortNames(); }
            catch { puertos = Array.Empty<string>(); }

            return Json(new { conectado, puerto, estado, puertosDisponibles = puertos, ultima, historial });
        }

        [HttpPost("PruebasSensores/Limpiar")]
        public IActionResult Limpiar()
        {
            _store.Limpiar();
            return Json(new { success = true });
        }

        [HttpPost("PruebasSensores/Iniciar")]
        public IActionResult Iniciar()
        {
            _serialService.EnviarComando("START");
            return Json(new { success = true });
        }

        [HttpPost("PruebasSensores/Detener")]
        public IActionResult Detener()
        {
            _serialService.EnviarComando("STOP");
            return Json(new { success = true });
        }
    }
}