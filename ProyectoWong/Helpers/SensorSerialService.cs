using System.IO.Ports;
using System.Text.Json;
using ProyectoWong.Models.Sensores;

namespace ProyectoWong.Helpers
{
    public class SensorSerialService : BackgroundService
    {
        private readonly SensorDataStore _store;
        private readonly IConfiguration _config;
        private readonly ILogger<SensorSerialService> _logger;

        // Variable a nivel de clase para poder leer Y escribir
        private SerialPort? _serialPort;
        private readonly object _lock = new object();

        public SensorSerialService(SensorDataStore store, IConfiguration config, ILogger<SensorSerialService> logger)
        {
            _store = store;
            _config = config;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => Ciclo(stoppingToken), stoppingToken);
        }

        private void Ciclo(CancellationToken ct)
        {
            bool habilitado = _config.GetValue("Sensores:Habilitado", true);
            string puerto = _config.GetValue("Sensores:PuertoCom", "COM3") ?? "COM3";
            int baudRate = _config.GetValue("Sensores:BaudRate", 9600);

            if (!habilitado)
            {
                _store.SetEstado("Lectura serial deshabilitada", false, puerto);
                return;
            }

            string ultimoEstado = "";

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    lock (_lock)
                    {
                        _serialPort = new SerialPort(puerto, baudRate)
                        {
                            ReadTimeout = 1000,
                            NewLine = "\n",
                            DtrEnable = true
                        };
                        _serialPort.Open();
                    }

                    _store.SetEstado("Conectado, esperando datos...", true, puerto);
                    _logger.LogInformation("Puerto {Puerto} abierto a {Baud} baudios.", puerto, baudRate);
                    ultimoEstado = "";

                    while (!ct.IsCancellationRequested)
                    {
                        string linea;
                        try
                        {
                            lock (_lock)
                            {
                                if (_serialPort == null || !_serialPort.IsOpen) break;
                                linea = _serialPort.ReadLine();
                            }
                        }
                        catch (TimeoutException) { continue; }
                        catch (InvalidOperationException) { break; }

                        ProcesarLinea(linea);
                        _store.SetEstado("Conectado, recibiendo datos", true, puerto);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    string estado = $"No se pudo leer {puerto}: {ex.Message}";
                    _store.SetEstado(estado, false, puerto);
                    if (estado != ultimoEstado)
                    {
                        _logger.LogWarning("{Estado}", estado);
                        ultimoEstado = estado;
                    }
                }
                finally
                {
                    lock (_lock)
                    {
                        if (_serialPort != null && _serialPort.IsOpen)
                        {
                            _serialPort.Close();
                            _serialPort.Dispose();
                            _serialPort = null;
                        }
                    }
                }
                ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
            }
        }

        // Envia un comando al Arduino (START, STOP, OK, FAIL, RESET).
        // Devuelve true solo si el comando realmente se escribio en el puerto serial.
        public bool EnviarComando(string comando)
        {
            lock (_lock)
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    try
                    {
                        _serialPort.WriteLine(comando);
                        _logger.LogInformation("Comando enviado al Arduino: {Comando}", comando);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar comando {Comando}", comando);
                        return false;
                    }
                }

                _logger.LogWarning("No se pudo enviar {Comando}: el puerto serial no esta abierto.", comando);
                return false;
            }
        }

        private void ProcesarLinea(string linea)
        {
            linea = linea.Trim();
            if (linea.Length == 0 || linea[0] != '{') return;

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
            catch (JsonException) { return; }

            _store.RegistrarLectura(lectura);
        }

        private static double? LeerNumero(JsonElement root, string nombre) =>
            root.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

        private static bool? LeerBool(JsonElement root, string nombre)
        {
            if (!root.TryGetProperty(nombre, out var v)) return null;
            return v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
        }
    }
}