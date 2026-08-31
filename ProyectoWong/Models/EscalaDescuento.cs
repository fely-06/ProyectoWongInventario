using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models
{
    public class EscalaDescuento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ProductoId { get; set; }

        [Range(1, int.MaxValue)]
        public int CantidadMinima { get; set; } // A partir de cuántas unidades aplica

        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal PorcentajeDescuento { get; set; } // Ej: 5.00 = 5%

        [ForeignKey("ProductoId")]
        public Producto Producto { get; set; } = null!;
    }
}