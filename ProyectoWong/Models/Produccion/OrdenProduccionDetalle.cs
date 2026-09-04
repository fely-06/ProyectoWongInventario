using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Produccion
{
    public class OrdenProduccionDetalle
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(OrdenProduccion))]
        public int OrdenProduccionId { get; set; }
        public OrdenProduccion? OrdenProduccion { get; set; }

        [Required]
        [ForeignKey(nameof(Componente))]
        public int ComponenteId { get; set; }
        public Componente? Componente { get; set; }

        public int CantidadRequerida { get; set; }
        public int CantidadConsumida { get; set; } = 0;
    }
}
