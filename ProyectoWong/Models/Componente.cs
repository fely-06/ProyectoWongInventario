using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ProyectoWong.Models
{
    public class Componente
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "El número de pieza es obligatorio")]
        [StringLength(50, ErrorMessage = "El número de pieza no puede exceder 50 caracteres")]
        public string NumeroPieza { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres")]
        public string Nombre { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa")]
        public int Cantidad { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "El mínimo no puede ser negativo")]
        public int MinimoInventario { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "El máximo no puede ser negativo")]
        public int MaximoInventario { get; set; } = 0;

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string? Descripcion { get; set; }

        [StringLength(100, ErrorMessage = "La categoría no puede exceder 100 caracteres")]
        public string? Categoria { get; set; }

        [StringLength(150, ErrorMessage = "El proveedor no puede exceder 150 caracteres")]
        public string? Proveedor { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo")]
        public decimal Precio { get; set; } = 0;

        [StringLength(50, ErrorMessage = "La unidad de medida no puede exceder 50 caracteres")]
        public string? UnidadMedida { get; set; }

        [StringLength(100, ErrorMessage = "El número de lote no puede exceder 100 caracteres")]
        public string? NumeroLote { get; set; }

        public DateTime? FechaCaducidad { get; set; }

        [StringLength(100, ErrorMessage = "El número de serie no puede exceder 100 caracteres")]
        public string? NumeroSerie { get; set; }

        [StringLength(150, ErrorMessage = "La ubicación no puede exceder 150 caracteres")]
        public string? Ubicacion { get; set; }

        [JsonIgnore]
        public byte[]? Imagen { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaAlta { get; set; } = DateTime.Now;
    }
}
