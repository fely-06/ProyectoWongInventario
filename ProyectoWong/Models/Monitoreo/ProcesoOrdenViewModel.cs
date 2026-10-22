namespace ProyectoWong.Models.Monitoreo
{
    public class ProcesoOrdenViewModel
    {
        public int Id { get; set; }
        public string NumeroOrden { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string Estado { get; set; } = string.Empty; // Pendiente, EnProceso, Pausada, Completada
        public string TiempoProduccion { get; set; } = string.Empty;
        public int PorcentajeAvance { get; set; }
        public string LineaProduccion { get; set; } = string.Empty;
    }
}