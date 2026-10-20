using System.IO.Ports;
using Microsoft.AspNetCore.Mvc;
using ProyectoWong.Helpers;

namespace ProyectoWong.Controllers
{
    public class PruebasSensoresController : Controller
    {
        private readonly SensorDataStore _store;
        private readonly SensorSerialService _serialService; // 1. Agregado

        // 2. Constructor actualizado para recibir AMBOS servicios
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

        // 3. NUEVO: Envía la orden al Arduino para empezar a medir
        [HttpPost("PruebasSensores/Iniciar")]
        public IActionResult Iniciar()
        {
            bool enviado = _serialService.EnviarComando("START");
            return Json(new { success = enviado });
        }

        // 4. NUEVO: Envía la orden al Arduino para detenerse y evaluar
        [HttpPost("PruebasSensores/Detener")]
        public IActionResult Detener()
        {
            bool enviado = _serialService.EnviarComando("STOP");
            return Json(new { success = enviado });
        }

        // Resultado de la prueba: enciende el LED verde (aprobado) o el rojo (rechazado) y lo deja fijo.
        [HttpPost("PruebasSensores/Resultado")]
        public IActionResult Resultado(bool aprobado)
        {
            bool enviado = _serialService.EnviarComando(aprobado ? "OK" : "FAIL");
            return Json(new { success = enviado });
        }

        // Apaga los LEDs y deja el Arduino en espera.
        [HttpPost("PruebasSensores/Reiniciar")]
        public IActionResult Reiniciar()
        {
            bool enviado = _serialService.EnviarComando("RESET");
            return Json(new { success = enviado });
        }
    }
}