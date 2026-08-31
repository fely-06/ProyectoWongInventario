using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Recepcion
{
    public class InspeccionQA
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(RecepcionDetalle))]
        public int RecepcionDetalleId { get; set; }
        public RecepcionDetalle? RecepcionDetalle { get; set; }

        public bool EmpaqueOk { get; set; } = true;

        public bool EtiquetaOk { get; set; } = true;

        public bool MaterialOk { get; set; } = true;

        public bool CertificadoDisponible { get; set; } = true;

        [Required]
        [ForeignKey(nameof(Inspector))]
        public int InspeccionadoPor { get; set; }
        public Usuarios? Inspector { get; set; }

        [Required]
        [StringLength(20, ErrorMessage = "El resultado no puede exceder 20 caracteres")]
        public string Resultado { get; set; } = "Aprobado";

        [StringLength(500, ErrorMessage = "Los comentarios no pueden exceder 500 caracteres")]
        public string? Comentarios { get; set; }

        public DateTime FechaInspeccion { get; set; } = DateTime.Now;

    }
}
