using System.IO.Ports;
using System.Text.Json;
using ProyectoWong.Models.Sensores;

namespace ProyectoWong.Helpers
{
    // Lee el puerto serial del Arduino en segundo plano y guarda las lecturas en SensorDataStore.
    // Configuración en appsettings.json, sección "Sensores".
    public class SensorSerialService : BackgroundService
    {
        private readonly SensorDataStore _store;
        private readonly IConfiguration _config;
        private readonly ILogger<SensorSerialService> _logger;

        public SensorSerialService(SensorDataStore store, IConfiguration config, ILogger<SensorSerialService> logger)
        {
            _store = store;
            _config = config;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Task.Run: la lectura serial es bloqueante y no debe frenar el arranque del servidor.
            return Task.Run(() => Ciclo(stoppingToken), stoppingToken);
        }

        private void Ciclo(CancellationToken ct)
        {
            bool habilitado = _config.GetValue("Sensores:Habilitado", true);
            string puerto = _config.GetValue("Sensores:PuertoCom", "COM3") ?? "COM3";
            int baudRate = _config.GetValue("Sensores:BaudRate", 9600);

            if (!habilitado)
            {
                _store.SetEstado("Lectura serial deshabilitada (Sensores:Habilitado = false)", false, puerto);
                return;
            }

            string ultimoEstado = "";

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var serial = new SerialPort(puerto, baudRate)
                    {
                        ReadTimeout = 1000,
                        NewLine = "\n",
                        DtrEnable = true
                    };
                    serial.Open();
                    _store.SetEstado("Conectado, esperando datos del Arduino...", true, puerto);
                    _logger.LogInformation("Puerto serial {Puerto} abierto a {Baud} baudios.", puerto, baudRate);
                    ultimoEstado = "";

                    while (!ct.IsCancellationRequested)
                    {
                        string linea;
                        try
                        {
                            linea = serial.ReadLine();
                        }
                        catch (TimeoutException)
                        {
                            continue; // no llegó nada en 1 s, seguir esperando
                        }

                        ProcesarLinea(linea);
                        _store.SetEstado("Conectado, recibiendo datos", true, puerto);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    string estado = $"No se pudo leer {puerto}: {ex.Message}";
                    _store.SetEstado(estado, false, puerto);

                    // Evita llenar el log con el mismo error cada 5 segundos.
                    if (estado != ultimoEstado)
                    {
                        _logger.LogWarning("{Estado}", estado);
                        ultimoEstado = estado;
                    }
                }

                // Espera antes de reintentar (Arduino desconectado, puerto ocupado, etc.)
                ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
            }
        }

        private void ProcesarLinea(string linea)
        {
            linea = linea.Trim();
            if (linea.Length == 0 || linea[0] != '{') return; // ignora texto que no sea JSON

            var lectura = new LecturaSensor { FechaHora = DateTime.Now, Cruda = linea };

            try
            {
                using var doc = JsonDocument.Parse(linea);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                {
                    lectura.Error = err.GetString();
                }
                else
                {
                    lectura.Temperatura = LeerNumero(root, "temp");
                    lectura.Humedad = LeerNumero(root, "hum");
                    lectura.Rpm = LeerNumero(root, "rpm");
                    lectura.PruebaTemperatura = LeerBool(root, "pruebaTemp");
                    lectura.PruebaRpm = LeerBool(root, "pruebaRpm");
                }
            }
            catch (JsonException)
            {
                // Línea incompleta (pasa al abrir el puerto): se descarta.
                _logger.LogDebug("Línea serial no válida: {Linea}", linea);
                return;
            }

            _store.RegistrarLectura(lectura);
        }

        private static double? LeerNumero(JsonElement root, string nombre) =>
            root.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

        private static bool? LeerBool(JsonElement root, string nombre)
        {
            if (!root.TryGetProperty(nombre, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
    }
}
