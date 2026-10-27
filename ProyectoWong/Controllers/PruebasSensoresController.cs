using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data; // Ajusta si tu DbContext está en otro namespace
using ProyectoWong.Helpers;
using ProyectoWong.Models;
using ProyectoWong.Models.Planta;
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
                    producto = op.Producto != null ? op.Producto.Nombre : "—",
                    // Solo se prueban las piezas que pasaron la inspección visual
                    cantidadTotal = op.CantidadAprobada,
                    aprobados = op.PruebasAprobadas,
                    rechazados = op.PruebasRechazadas,
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
                var ordenProduccion = await _db.OrdenProduccion.FindAsync(request.OrdenProduccionId);
                if (ordenProduccion == null)
                    return Json(new { success = false, error = "Orden de producción no encontrada." });
                if (ordenProduccion.Estado != "EnPruebasTecnicas")
                    return Json(new { success = false, error = $"La orden no está en pruebas técnicas. Estado actual: {ordenProduccion.Estado}" });
                if (ordenProduccion.PruebasAprobadas + ordenProduccion.PruebasRechazadas >= ordenProduccion.CantidadAprobada)
                    return Json(new { success = false, error = "Ya se probaron todas las piezas de esta orden." });

                var (_, historialStore, _, _, _) = _store.Obtener();

                // Todo o nada: historial, lecturas y contadores de la orden
                await using var transaction = await _db.Database.BeginTransactionAsync();

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

                // B. Guardar lecturas detalladas de TODAS las pruebas realizadas. La pantalla las manda
                //    porque el Arduino se limpia al iniciar cada prueba; si no llegan, se usa lo que haya en memoria.
                var lecturas = request.Lecturas is { Count: > 0 }
                    ? request.Lecturas.Select(l => new LecturaSensor { FechaHora = l.FechaHora, Rpm = l.Rpm, Temperatura = l.Temperatura, Humedad = l.Humedad }).ToList()
                    : historialStore.Where(l => l.Error == null).ToList();

                foreach (var lectura in lecturas)
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

                // C. ACTUALIZAR CONTADORES DE PRUEBAS TÉCNICAS (separados de los de la inspección visual)
                bool scrapRegistrado = false;
                if (request.EsAprobado)
                    ordenProduccion.PruebasAprobadas++;
                else
                {
                    ordenProduccion.PruebasRechazadas++;
                    // D. La pieza que no pasa la prueba física se cuantifica como scrap automáticamente
                    scrapRegistrado = await RegistrarScrapAutomaticoAsync(ordenProduccion, request.PruebasFallidas);
                }

                int totalProbado = ordenProduccion.PruebasAprobadas + ordenProduccion.PruebasRechazadas;
                if (totalProbado >= ordenProduccion.CantidadAprobada)
                {
                    ordenProduccion.Estado = "Completada";
                    ordenProduccion.FechaFin = DateTime.Now;
                }

                // ✅ SEGUNDO SaveChanges - Guarda TODOS los detalles y la orden de una vez
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _store.Limpiar();

                return Json(new
                {
                    success = true,
                    historialId = nuevoHistorial.Id,
                    scrapRegistrado,
                    nuevosContadores = new
                    {
                        aprobados = ordenProduccion.PruebasAprobadas,
                        rechazados = ordenProduccion.PruebasRechazadas
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

        // Crea 1 pieza de scrap en el banco de pruebas con el motivo de la primera prueba que falló.
        // Devuelve false si no hay máquinas cargadas o si la orden ya tiene todo su scrap registrado.
        private async Task<bool> RegistrarScrapAutomaticoAsync(OrdenProduccion orden, List<string>? pruebasFallidas)
        {
            var maquina = await _db.Maquinas
                .Where(m => m.Activa)
                .OrderBy(m => m.Codigo == CatalogosPlanta.CodigoBancoPruebas ? 0 : 1)
                .ThenBy(m => m.Codigo)
                .FirstOrDefaultAsync();
            if (maquina == null) return false;

            int scrapActual = await _db.RegistrosScrap
                .Where(r => r.OrdenProduccionId == orden.Id)
                .SumAsync(r => (int?)r.Cantidad) ?? 0;
            if (scrapActual + 1 > orden.CantidadAProducir) return false;

            var motivos = (pruebasFallidas ?? [])
                .Where(CatalogosPlanta.MotivoPorPrueba.ContainsKey)
                .Select(p => CatalogosPlanta.MotivoPorPrueba[p])
                .Distinct()
                .ToList();

            _db.RegistrosScrap.Add(new RegistroScrap
            {
                OrdenProduccionId = orden.Id,
                MaquinaId = maquina.Id,
                MotivoRechazo = motivos.FirstOrDefault() ?? "Otro",
                Cantidad = 1,
                Fecha = DateTime.Now,
                Observaciones = motivos.Count > 0
                    ? "Generado por Pruebas de Sensores. Falló: " + string.Join(", ", motivos)
                    : "Generado por Pruebas de Sensores.",
                Automatico = true
            });
            return true;
        }

        // 3. VERIFICAR SI LA ORDEN YA TERMINÓ SUS PRUEBAS TÉCNICAS
        // GuardarPrueba es quien cierra la orden al probar la última pieza; aquí solo se informa el estado.
        [HttpPost("CompletarOP")]
        public async Task<IActionResult> CompletarOP([FromBody] CompletarOPRequest request)
        {
            var orden = await _db.OrdenProduccion.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == request.OrdenProduccionId);
            if (orden == null)
                return Json(new { success = false, mensaje = "Orden de producción no encontrada." });

            int faltan = Math.Max(0, orden.CantidadAprobada - orden.PruebasAprobadas - orden.PruebasRechazadas);
            string mensaje = orden.Estado == "Completada"
                ? $"Orden de producción {orden.NumeroOP} completada."
                : faltan == 1 ? "Falta 1 pieza por probar." : $"Faltan {faltan} piezas por probar.";

            return Json(new
            {
                success = true,
                mensaje,
                totalAprobados = orden.PruebasAprobadas,
                totalRechazados = orden.PruebasRechazadas
            });
        }
    }

    public class CompletarOPRequest
    {
        public int OrdenProduccionId { get; set; }
    }

    public class GuardarPruebaRequest
    {
        public int OrdenProduccionId { get; set; }
        public bool EsAprobado { get; set; }
        public string Observaciones { get; set; } = "";
        public int DuracionSegundos { get; set; }
        // Ids de los pasos que no pasaron ("rpm", "temp"); sirve para el motivo del scrap automático
        public List<string>? PruebasFallidas { get; set; }
        // Lecturas tomadas durante las pruebas (las mismas que la pantalla muestra en la tabla)
        public List<LecturaPruebaRequest>? Lecturas { get; set; }
    }

    public class LecturaPruebaRequest
    {
        public DateTime FechaHora { get; set; }
        public double? Rpm { get; set; }
        public double? Temperatura { get; set; }
        public double? Humedad { get; set; }
    }
}