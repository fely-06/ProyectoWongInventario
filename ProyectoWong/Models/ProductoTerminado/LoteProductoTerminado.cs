using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProyectoWong.Models.Produccion;

namespace ProyectoWong.Models.ProductoTerminado
{
    // Entrada al almacén de producto terminado (Finished Goods): las piezas buenas de UNA orden
    // de producción completada forman un lote, que se reparte en una o varias cajas/pallets.
    public class LoteProductoTerminado
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string NumeroLote { get; set; } = string.Empty; // LPT-AAAAMMDD-NNN

        // Una orden se recibe una sola vez (índice único)
        public int OrdenProduccionId { get; set; }
        [ForeignKey(nameof(OrdenProduccionId))]
        public OrdenProduccion? OrdenProduccion { get; set; }

        public int ProductoId { get; set; }
        [ForeignKey(nameof(ProductoId))]
        public Producto? Producto { get; set; }

        // Piezas buenas según Calidad (inspección visual + pruebas de sensores)
        public int Cantidad { get; set; }

        [Required]
        [StringLength(10)]
        public string TipoEmpaque { get; set; } = "Pallet"; // "Caja" | "Pallet"

        public DateTime FechaRecepcion { get; set; } = DateTime.Now;

        public int RecibidoPor { get; set; }
        [ForeignKey(nameof(RecibidoPor))]
        public Usuarios? Receptor { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "PorUbicar"; // "PorUbicar" | "Almacenado"

        [StringLength(300)]
        public string? Observaciones { get; set; }

        public ICollection<PalletProductoTerminado> Pallets { get; set; } = new List<PalletProductoTerminado>();
    }
}
