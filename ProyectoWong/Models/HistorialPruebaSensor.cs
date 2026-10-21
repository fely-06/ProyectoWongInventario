using ProyectoWong.Models.Produccion;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models
{
    public class HistorialPruebaSensor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrdenProduccionId { get; set; } // CAMBIO CLAVE

        [Required]
        public DateTime FechaInicio { get; set; }

        [Required]
        public DateTime FechaFin { get; set; }

        [Required]
        [StringLength(20)]
        public string ResultadoFinal { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Observaciones { get; set; }

        [Required]
        public int DuracionSegundos { get; set; }

        // Navegación
        [ForeignKey("OrdenProduccionId")]
        public virtual OrdenProduccion OrdenProduccion { get; set; } = null!;
        public ICollection<DetalleLecturaSensor> Lecturas { get; set; } = new List<DetalleLecturaSensor>();

    }
}
