using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProyectoWong.Models.Produccion;

namespace ProyectoWong.Models.Planta
{
    // Un evento de scrap: piezas rechazadas de una orden de producción en una máquina.
    public class RegistroScrap
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrdenProduccionId { get; set; }

        [ForeignKey(nameof(OrdenProduccionId))]
        public OrdenProduccion OrdenProduccion { get; set; } = null!;

        [Required]
        public int MaquinaId { get; set; }

        [ForeignKey(nameof(MaquinaId))]
        public Maquina Maquina { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string MotivoRechazo { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
        public int Cantidad { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        [StringLength(300)]
        public string? Observaciones { get; set; }

        // true si lo generó Pruebas de Sensores al rechazar una pieza (false = captura manual)
        public bool Automatico { get; set; }
    }
}
