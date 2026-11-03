using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProyectoWong.Models.Recepcion;

namespace ProyectoWong.Models.ProductoTerminado
{
    // Una caja o pallet de producto terminado. Cada una tiene su propio código y etiqueta,
    // aunque varias pertenezcan al mismo lote.
    public class PalletProductoTerminado
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(40)]
        public string CodigoPallet { get; set; } = string.Empty; // LPT-AAAAMMDD-NNN-P01 (índice único)

        public int LoteId { get; set; }
        [ForeignKey(nameof(LoteId))]
        public LoteProductoTerminado? Lote { get; set; }

        public int Cantidad { get; set; }

        public DateTime FechaEtiqueta { get; set; } = DateTime.Now;

        // Se llenan cuando el operador confirma la ubicación de almacenamiento
        public int? UbicacionId { get; set; }
        [ForeignKey(nameof(UbicacionId))]
        public Ubicacion? Ubicacion { get; set; }

        public DateTime? FechaUbicacion { get; set; }

        public int? UbicadoPor { get; set; }
        [ForeignKey(nameof(UbicadoPor))]
        public Usuarios? Operador { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "PorUbicar"; // "PorUbicar" | "Almacenado"
    }
}
