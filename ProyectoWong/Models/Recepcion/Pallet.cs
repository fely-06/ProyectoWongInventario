using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProyectoWong.Models.Recepcion
{
    public class Pallet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id {  get; set; }

        [Required]
        [ForeignKey(nameof(RecepcionDetalle))]
        public int RecepcionDetalleId {  get; set; }
        public RecepcionDetalle? RecepcionDetalle { get; set; }

        [Required(ErrorMessage = "El cédigo del pallet es obligatorio")]
        [StringLength(50, ErrorMessage = "El código del pallet no debe exceder los 50 caracteres")]
        public string CodigoPallet { get; set; } = string.Empty;
        public DateTime? FechaImpresionEtiqueta { get; set; }

        //navegacion
        public ICollection<MovimientoInventario>? Movimientos {  get; set; }
    }
}
