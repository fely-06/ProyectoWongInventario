using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace ProyectoWong.Models.Recepcion
{
    public class Recepcion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(OrdenCompra))]
        public int OrdenCompraId { get; set; }
        public OrdenCompra? OrdenCompra { get; set; }

        [Required]
        [ForeignKey(nameof(Usuario))]
        public int UsuarioId { get; set; }
        public Usuarios? Usuario { get; set; }

        public DateTime FechaRecepcion { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20, ErrorMessage = "El estado no puede exceder 20 caracteres")]
        public string Estado { get; set; } = "EnProceso";

        // navegacion
        public ICollection<RecepcionDetalle>? Detalles { get; set; }

    }
}
