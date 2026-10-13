namespace ProyectoWong.Models.Sensores
{
    // Una lectura enviada por el Arduino (una línea JSON por el puerto serial).
    public class LecturaSensor
    {
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public double? Temperatura { get; set; }   // °C (DHT11)
        public double? Humedad { get; set; }       // % (DHT11)
        public double? Rpm { get; set; }           // RPM (sensor infrarrojo)
        public bool? PruebaTemperatura { get; set; } // true = aprobada, null = sin medir
        public bool? PruebaRpm { get; set; }
        public string? Error { get; set; }          // ej. "ERROR_SENSOR_DHT"
        public string Cruda { get; set; } = "";     // línea tal cual llegó del Arduino
    }
}
