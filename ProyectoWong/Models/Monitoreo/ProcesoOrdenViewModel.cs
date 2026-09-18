namespace ProyectoWong.Models.Monitoreo
{
    // ViewModel usado por MonitoreoController.Produccion() para alimentar
    // la tabla "Proceso de orden" en la vista de Monitoreo de Producción.
    public class ProcesoOrdenViewModel
    {
        public string NumeroOrden { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }

        // NOTA: TiempoProduccion, PorcentajeAvance y LineaProduccion NO existen
        // todavía en OrdenProduccion / OrdenProduccionDetalle. Por ahora se
        // calculan como valores de EJEMPLO en el controlador. Cuando agregues
        // esos campos reales al modelo, solo hay que cambiar el mapeo en
        // MonitoreoController.Produccion().
        public string TiempoProduccion { get; set; } = string.Empty;
        public string EstadoProduccion { get; set; } = string.Empty;
        public int PorcentajeAvance { get; set; }
        public string LineaProduccion { get; set; } = string.Empty;
    }
}