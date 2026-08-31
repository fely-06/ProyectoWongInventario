using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Recepcion
{
    public class Ubicacion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "El código de ubicación es obligatorio")]
        [StringLength(20, ErrorMessage = "El código no puede exceder 20 caracteres")]
        public string Codigo { get; set; } = string.Empty; 

        [StringLength(50, ErrorMessage = "La zona no puede exceder 50 caracteres")]
        public string? Zona { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "El tipo no puede exceder 30 caracteres")]
        public string Tipo { get; set; } = "Disponible";

        public int? CapacidadMaxima { get; set; }

        public bool Activo { get; set; } = true;

        // navegacion
        public ICollection<MovimientoInventario>? Movimientos { get; set; }

    }
}
