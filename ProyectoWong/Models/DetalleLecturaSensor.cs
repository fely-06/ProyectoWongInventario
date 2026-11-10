namespace ProyectoWong.Models
{
    public class DetalleLecturaSensor
    {
        public int Id { get; set; }
        public int HistorialId { get; set; }
        public DateTime FechaHora { get; set; }
        public double? Rpm { get; set; }
        public double? Temperatura { get; set; }
        public double? Humedad { get; set; }
        public double? Vibracion { get; set; }  // g
        public double? Ruido { get; set; }      // dB aprox.
        public string Estado { get; set; } = string.Empty; // "OK" o "FUERA_DE_RANGO"
    }
}
