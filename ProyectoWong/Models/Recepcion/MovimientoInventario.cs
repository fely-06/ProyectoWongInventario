using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Recepcion
{
    public class MovimientoInventario
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Pallet))]
        public int PalletId { get; set; }
        public Pallet? Pallet { get; set; }

        [Required]
        [ForeignKey(nameof(Ubicacion))]
        public int UbicacionId { get; set; }
        public Ubicacion? Ubicacion { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "El tipo de movimiento no puede exceder 30 caracteres")]
        public string TipoMovimiento { get; set; } = "Recepcion";

        public DateTime FechaMovimiento { get; set; } = DateTime.Now;

        [Required]
        [ForeignKey(nameof(Usuario))]
        public int RealizadoPor { get; set; }
        public Usuarios? Usuario { get; set; }

    }
}
