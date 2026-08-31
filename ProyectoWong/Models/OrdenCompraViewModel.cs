using System.ComponentModel.DataAnnotations;

namespace ProyectoWong.Models
{
    public class OrdenCompraViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El número de OC es obligatorio")]
        [StringLength(50, ErrorMessage = "El número de OC no puede exceder 50 caracteres")]
        public string NumeroOC { get; set; } = string.Empty;

        [Required(ErrorMessage = "El proveedor es obligatorio")]
        public int ProveedorId { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio")]
        [StringLength(20, ErrorMessage = "El estado no puede exceder 20 caracteres")]
        public string Estado { get; set; } = "Abierta";

        public DateTime? FechaEsperada { get; set; }
        public List<OrdenCompraDetalleViewModel> Detalles { get; set; } = new();
    }

    public class OrdenCompraDetalleViewModel
    {
        public int Id { get; set; }

        public int OrdenCompraId { get; set; }

        [Required(ErrorMessage = "El componente es obligatorio")]
        public int ComponenteId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad esperada debe ser mayor a 0")]
        public int CantidadEsperada { get; set; }
    }
}