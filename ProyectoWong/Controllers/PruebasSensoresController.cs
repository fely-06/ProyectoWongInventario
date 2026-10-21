using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models;
using System.IO.Ports;
using System.Linq;

namespace ProyectoWong.Controllers
{
    public class PruebasSensoresController : Controller
    {
        private readonly SensorDataStore _store;
        private readonly SensorSerialService _serialService;
        private readonly ApplicationDbContext _db; // <-- Reemplaza con el nombre real de tu DbContext

        public PruebasSensoresController(SensorDataStore store, SensorSerialService serialService, ApplicationDbContext db)
        {
            _store = store;
            _serialService = serialService;
            _db = db;
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
        // ... (Mantén tus métodos Index, Ultimos, Limpiar, Iniciar, Detener, Resultado, Reiniciar) ...

        
        // NUEVO: Obtener Órdenes de Producción listas para pruebas
        [HttpGet("PruebasSensores/OrdenesActivas")]
        public IActionResult OrdenesActivas()
        {
            // Traemos OPs que ya terminaron de producirse y están pendientes de prueba
            var ordenes = _db.OrdenProduccion
                .Include(op => op.Producto)
                .Include(op => op.OrdenCompra)
                .Where(op => op.Estado == "Terminada" || op.Estado == "En Pruebas") // CAMBIO CLAVE
                .Select(op => new {
                    id = op.Id,
                    numeroOP = op.NumeroOP,
                    producto = op.Producto.Nombre,
                    cantidadTotal = op.CantidadAProducir,
                    aprobados = op.CantidadAprobada,
                    rechazados = op.CantidadRechazada,
                    ordenCompra = op.OrdenCompra != null ? op.OrdenCompra.NumeroOC : "N/A",
                    estado = op.Estado
                })
                .ToList();

            return Json(new { success = true, data = ordenes });
        }
        // NUEVO: Marcar OP como completada cuando termine todas las pruebas
        [HttpPost("PruebasSensores/CompletarOP")]
        public IActionResult CompletarOP([FromBody] CompletarOPRequest request)
        {
            try
            {
                var ordenProduccion = _db.OrdenProduccion.Find(request.OrdenProduccionId);
                if (ordenProduccion == null)
                {
                    return Json(new { success = false, error = "Orden de Producción no encontrada" });
                }

                // Verificar si ya se probaron todas las unidades
                int totalProbado = ordenProduccion.CantidadAprobada + ordenProduccion.CantidadRechazada;

                if (totalProbado >= ordenProduccion.CantidadAProducir)
                {
                    // Todas las unidades fueron probadas, marcar como completada
                    ordenProduccion.Estado = "Completada";
                    ordenProduccion.FechaFin = DateTime.Now;
                    _db.SaveChanges();

                    return Json(new
                    {
                        success = true,
                        mensaje = "Orden de Producción completada exitosamente",
                        totalAprobados = ordenProduccion.CantidadAprobada,
                        totalRechazados = ordenProduccion.CantidadRechazada
                    });
                }
                else
                {
                    // Aún faltan unidades por probar
                    return Json(new
                    {
                        success = true,
                        mensaje = $"Faltan {ordenProduccion.CantidadAProducir - totalProbado} unidades por probar",
                        totalAprobados = ordenProduccion.CantidadAprobada,
                        totalRechazados = ordenProduccion.CantidadRechazada
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        public class CompletarOPRequest
        {
            public int OrdenProduccionId { get; set; }
        }

        // MODIFICADO: Guardar prueba y actualizar contadores de la Orden de Producción
        [HttpPost("PruebasSensores/GuardarPrueba")]
        public IActionResult GuardarPrueba([FromBody] GuardarPruebaRequest request)
        {
            try
            {
                var (_, historialStore, _, _, _) = _store.Obtener();

                // 1. Crear el registro de la prueba
                var nuevoHistorial = new HistorialPruebaSensor
                {
                    OrdenProduccionId = request.OrdenProduccionId,
                    FechaInicio = DateTime.Now.AddSeconds(-request.DuracionSegundos),
                    FechaFin = DateTime.Now,
                    ResultadoFinal = request.EsAprobado ? "APROBADO" : "RECHAZADO",
                    Observaciones = request.Observaciones,
                    DuracionSegundos = request.DuracionSegundos
                };

                _db.HistorialPruebasSensores.Add(nuevoHistorial);
                _db.SaveChanges();

                // 2. Guardar lecturas detalladas
                foreach (var lectura in historialStore)
                {
                    string estadoLectura = "OK";
                    if (lectura.Rpm.HasValue && lectura.Rpm < 2000) estadoLectura = "FUERA_DE_RANGO";
                    if (lectura.Temperatura.HasValue && (lectura.Temperatura < 18 || lectura.Temperatura > 28)) estadoLectura = "FUERA_DE_RANGO";

                    _db.DetalleLecturasSensores.Add(new DetalleLecturaSensor
                    {
                        HistorialId = nuevoHistorial.Id,
                        FechaHora = lectura.FechaHora,
                        Rpm = lectura.Rpm,
                        Temperatura = lectura.Temperatura,
                        Humedad = lectura.Humedad,
                        Estado = estadoLectura
                    });
                }

                // 3. ACTUALIZAR CONTADORES DE LA ORDEN DE PRODUCCIÓN (¡Lo que quiere el profe!)
                var ordenProduccion = _db.OrdenProduccion.Find(request.OrdenProduccionId);
                if (ordenProduccion != null)
                {
                    if (request.EsAprobado)
                        ordenProduccion.CantidadAprobada++;
                    else
                        ordenProduccion.CantidadRechazada++;

                    _db.SaveChanges();
                }

                _store.Limpiar();

                // Devolvemos los nuevos contadores para actualizar la UI en tiempo real
                return Json(new
                {
                    success = true,
                    historialId = nuevoHistorial.Id,
                    nuevosContadores = new { aprobados = ordenProduccion?.CantidadAprobada ?? 0, rechazados = ordenProduccion?.CantidadRechazada ?? 0 }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    public class GuardarPruebaRequest
    {
        public int OrdenProduccionId { get; set; } // CAMBIO CLAVE
        public bool EsAprobado { get; set; }
        public string Observaciones { get; set; } = "";
        public int DuracionSegundos { get; set; }
    }
}