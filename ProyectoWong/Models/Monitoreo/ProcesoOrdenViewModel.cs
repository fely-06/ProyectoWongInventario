namespace ProyectoWong.Models.Monitoreo
{
    public class ProcesoOrdenViewModel
    {
        public int Id { get; set; }
        public string NumeroOrden { get; set; }
        public string Producto { get; set; }
        public int Cantidad { get; set; }
        public string Estado { get; set; } // Pendiente, EnProceso, Pausada, Completada
        public string TiempoProduccion { get; set; }
        public int PorcentajeAvance { get; set; }
        public string LineaProduccion { get; set; }
    }
}