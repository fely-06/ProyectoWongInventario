using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models
{
    public class ProductoComponente
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ProductoId { get; set; }
        public int ComponenteId { get; set; }

        [Range(1, int.MaxValue)]
        public int CantidadRequerida { get; set; } // Cuántos de este componente por cada producto

        // Navegación
        [ForeignKey("ProductoId")]
        public Producto Producto { get; set; } = null!;

        [ForeignKey("ComponenteId")]
        public Componente Componente { get; set; } = null!;
    }
}