using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace ProyectoWong.Models.Recepcion
{
    public class OrdenCompra
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get;set; }

        [StringLength(50, ErrorMessage = "El número de orden de compra no debe de exceder los 50 caracteres")]
        public string NumeroOC { get; set; } = string.Empty;

        [Required]
        [ForeignKey(nameof(Proveedor))]
        public int? ProveedorId { get; set; }
        public string? Proveedor { get; set; }
        public string Estado { get; set; } = "Abierta";
        public DateTime? FechaEsperada { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        //navegacion
        public ICollection<OrdenCompraDetalle>?Detalles { get; set; }
        public ICollection<Recepcion>? Recepciones {  get; set; }
    }
}
