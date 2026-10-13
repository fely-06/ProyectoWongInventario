using ProyectoWong.Models.Sensores;

namespace ProyectoWong.Helpers
{
    // Guarda en memoria las últimas lecturas del Arduino.
    // Se registra como Singleton: lo escribe SensorSerialService y lo lee el controlador.
    public class SensorDataStore
    {
        private const int MaxHistorial = 50;
        private readonly object _lock = new();
        private readonly List<LecturaSensor> _historial = new();

        private LecturaSensor? _ultima;
        private string _estado = "Sin iniciar";
        private bool _conectado;
        private string _puerto = "";

        public void RegistrarLectura(LecturaSensor lectura)
        {
            lock (_lock)
            {
                // Una lectura de error no pisa los últimos valores buenos, solo se agrega al historial.
                if (lectura.Error == null) _ultima = lectura;

                _historial.Insert(0, lectura);
                if (_historial.Count > MaxHistorial)
                    _historial.RemoveRange(MaxHistorial, _historial.Count - MaxHistorial);
            }
        }

        public void SetEstado(string estado, bool conectado, string puerto)
        {
            lock (_lock)
            {
                _estado = estado;
                _conectado = conectado;
                _puerto = puerto;
            }
        }

        public (LecturaSensor? Ultima, List<LecturaSensor> Historial, string Estado, bool Conectado, string Puerto) Obtener()
        {
            lock (_lock)
            {
                return (_ultima, _historial.ToList(), _estado, _conectado, _puerto);
            }
        }

        public void Limpiar()
        {
            lock (_lock)
            {
                _ultima = null;
                _historial.Clear();
            }
        }
    }
}
