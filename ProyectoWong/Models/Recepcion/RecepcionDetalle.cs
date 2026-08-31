using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Recepcion
{
    public class RecepcionDetalle
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Recepcion))]
        public int RecepcionId { get; set; }
        public Recepcion? Recepcion { get; set; }

        [Required]
        [ForeignKey(nameof(Componente))]
        public int ComponenteId { get; set; }
        public Componente? Componente { get; set; }

        [Required(ErrorMessage = "El número de lote es obligatorio")]
        [StringLength(100, ErrorMessage = "El número de lote no puede exceder 100 caracteres")]
        public string NumeroLote { get; set; } = string.Empty;

        public DateTime? FechaCaducidad { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "La cantidad recibida no puede ser negativa")]
        public int CantidadRecibida { get; set; }

        [Required]
        [StringLength(20, ErrorMessage = "El estado no puede exceder 20 caracteres")]
        public string Estado { get; set; } = "Pendiente";

        // navegación
        public InspeccionQA? Inspeccion { get; set; }
        public ICollection<Pallet>? Pallets { get; set; }

    }
}
