using ProyectoWong.Models.Recepcion;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Produccion
{
    public class OrdenProduccion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [StringLength(50)]
        public string NumeroOP { get; set; } = string.Empty;

        [Required]
        [ForeignKey(nameof(Producto))]
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        public int CantidadAProducir { get; set; }
        public string Estado { get; set; } = "Pendiente";

        [ForeignKey(nameof(OrdenCompra))]
        public int? OrdenCompraId { get; set; }
        public OrdenCompra? OrdenCompra { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }

        public ICollection<OrdenProduccionDetalle>? Detalles { get; set; }
    }
}