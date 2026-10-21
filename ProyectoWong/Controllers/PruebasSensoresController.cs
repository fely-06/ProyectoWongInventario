using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data; // Ajusta si tu DbContext está en otro namespace
using ProyectoWong.Helpers;
using ProyectoWong.Models;
using ProyectoWong.Models.Produccion;
using ProyectoWong.Models.Sensores; // Asegúrate de tener aquí tus modelos de HistorialPruebaSensor y DetalleLecturaSensor
using System.IO.Ports;

namespace ProyectoWong.Controllers
{
    [Route("PruebasSensores")]
    public class PruebasSensoresController : Controller
    {
        private readonly SensorDataStore _store;
        private readonly SensorSerialService _serialService;
        private readonly ApplicationDbContext _db;

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

        [HttpGet("Ultimos")]
        public IActionResult Ultimos()
        {
            var (ultima, historial, estado, conectado, puerto) = _store.Obtener();
            string[] puertos;
            try { puertos = SerialPort.GetPortNames(); }
            catch { puertos = Array.Empty<string>(); }
            return Json(new { conectado, puerto, estado, puertosDisponibles = puertos, ultima, historial });
        }

        [HttpPost("Limpiar")]
        public IActionResult Limpiar() { _store.Limpiar(); return Json(new { success = true }); }

        [HttpPost("Iniciar")]
        public IActionResult Iniciar() { bool enviado = _serialService.EnviarComando("START"); return Json(new { success = enviado }); }

        [HttpPost("Detener")]
        public IActionResult Detener() { bool enviado = _serialService.EnviarComando("STOP"); return Json(new { success = enviado }); }

        [HttpPost("Resultado")]
        public IActionResult Resultado(bool aprobado) { bool enviado = _serialService.EnviarComando(aprobado ? "OK" : "FAIL"); return Json(new { success = enviado }); }

        [HttpPost("Reiniciar")]
        public IActionResult Reiniciar() { bool enviado = _serialService.EnviarComando("RESET"); return Json(new { success = enviado }); }

        // 1. OBTENER ÓRDENES LISTAS PARA PRUEBAS TÉCNICAS
        [HttpGet("OrdenesActivas")]
        public IActionResult OrdenesActivas()
        {
            var ordenes = _db.OrdenProduccion
                .Include(op => op.Producto)
                .Include(op => op.OrdenCompra)
                .Where(op => op.Estado == "EnPruebasTecnicas") // <-- Solo las que pasaron inspección visual
                .Select(op => new
                {
                    id = op.Id,
                    numeroOP = op.NumeroOP,
                    producto = op.Producto.Nombre,
                    cantidadTotal = op.CantidadAProducir,
                    aprobados = op.CantidadAprobada,
                    rechazados = op.CantidadRechazada,
                    ordenCompra = op.OrdenCompra != null ? op.OrdenCompra.NumeroOC : "Manual",
                    estado = op.Estado
                })
                .ToList();

            return Json(new { success = true, data = ordenes });
        }

        // 2. GUARDAR PRUEBA Y ACTUALIZAR CONTADORES DE LA ORDEN DE PRODUCCIÓN
        [HttpPost("GuardarPrueba")]
        async public Task<IActionResult> GuardarPrueba([FromBody] GuardarPruebaRequest request)
        {
            try
            {
                var (_, historialStore, _, _, _) = _store.Obtener();

                // A. Crear el registro de la prueba en BD
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
                await _db.SaveChangesAsync(); // ✅ PRIMER SaveChanges - Guarda el historial y obtiene el ID

                // B. Guardar lecturas detalladas en tiempo real
                foreach (var lectura in historialStore)
                {
                    string estadoLectura = "OK";
                    if (lectura.Rpm.HasValue && lectura.Rpm < 2000) estadoLectura = "FUERA_DE_RANGO";
                    if (lectura.Temperatura.HasValue && (lectura.Temperatura < 18 || lectura.Temperatura > 28)) estadoLectura = "FUERA_DE_RANGO";

                    _db.DetalleLecturasSensores.Add(new DetalleLecturaSensor
                    {
                        HistorialId = nuevoHistorial.Id, // ✅ Este ID ya existe gracias al primer SaveChanges
                        FechaHora = lectura.FechaHora,
                        Rpm = lectura.Rpm,
                        Temperatura = lectura.Temperatura,
                        Humedad = lectura.Humedad,
                        Estado = estadoLectura
                    });
                }

                // ✅ SEGUNDO SaveChanges - Guarda TODOS los detalles de una vez
                await _db.SaveChangesAsync();

                // C. ACTUALIZAR CONTADORES DE LA ORDEN DE PRODUCCIÓN
                var ordenProduccion = await _db.OrdenProduccion.FindAsync(request.OrdenProduccionId);
                if (ordenProduccion != null)
                {
                    if (request.EsAprobado)
                        ordenProduccion.CantidadAprobada++;
                    else
                        ordenProduccion.CantidadRechazada++;

                    int totalProbado = ordenProduccion.CantidadAprobada + ordenProduccion.CantidadRechazada;
                    if (totalProbado >= ordenProduccion.CantidadAProducir)
                    {
                        ordenProduccion.Estado = "Completada";
                        ordenProduccion.FechaFin = DateTime.Now;
                    }

                    await _db.SaveChangesAsync(); // ✅ TERCER SaveChanges - Actualiza la orden
                }

                _store.Limpiar();

                return Json(new
                {
                    success = true,
                    historialId = nuevoHistorial.Id,
                    nuevosContadores = new
                    {
                        aprobados = ordenProduccion?.CantidadAprobada ?? 0,
                        rechazados = ordenProduccion?.CantidadRechazada ?? 0
                    }
                });
            }
            catch (Exception ex)
            {
                // ✅ Retorna el error con más detalles
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }
    }

    public class GuardarPruebaRequest
    {
        public int OrdenProduccionId { get; set; }
        public bool EsAprobado { get; set; }
        public string Observaciones { get; set; } = "";
        public int DuracionSegundos { get; set; }
    }
}