using ProyectoWong.Models.Recepcion;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Produccion
{
    public class OrdenProduccion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [StringLength(50)]
        public string NumeroOP { get; set; } = string.Empty;

        [Required]
        [ForeignKey(nameof(Producto))]
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        public int CantidadAProducir { get; set; }
        public string Estado { get; set; } = "Pendiente";

        [ForeignKey(nameof(OrdenCompra))]
        public int? OrdenCompraId { get; set; }
        public OrdenCompra? OrdenCompra { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }

        public double DuracionEstimadaMinutos { get; set; }
        public DateTime? FechaPausa { get; set; }
        public double TiempoPausadoMinutos { get; set; } // Acumulado de tiempo en pausa

        // --- Campos para Inspección Visual ---
        public int CantidadAprobada { get; set; } = 0;
        public int CantidadRechazada { get; set; } = 0;
        public string? ObservacionesInspeccion { get; set; }

        // --- Evidencia fotográfica de la inspección visual (requisito de monitoreo visual) ---
        public byte[]? FotoInspeccion { get; set; }
        public DateTime? FechaFotoInspeccion { get; set; }

        public ICollection<OrdenProduccionDetalle>? Detalles { get; set; }
    }
}