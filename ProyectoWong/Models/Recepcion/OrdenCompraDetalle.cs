using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Recepcion
{
    [Table("OrdenCompraDetalle")]
    public class OrdenCompraDetalle
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id {  get; set; }

        [Required]
        [ForeignKey(nameof(OrdenCompra))]
        public int OrdenCmpraId {  get; set; }
        public OrdenCompra? OrdenCompra { get; set; }

        [Required]
        [ForeignKey(nameof(Componente))]
        public int ComponenteId { get; set; }
        public Componente? Componente {  get; set; }

        [Range(0,int.MaxValue, ErrorMessage= "La cantidad esperada no puede ser negativa")]
        public int CantidadEsperada {  get; set; }
    }
}
