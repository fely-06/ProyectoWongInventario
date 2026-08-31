using System.ComponentModel.DataAnnotations;

namespace ProyectoWong.Models
{
    public class ProveedorViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(10, MinimumLength = 10, ErrorMessage = "El teléfono debe tener exactamente 10 dígitos")]
        [RegularExpression(@"^\d+$", ErrorMessage = "El teléfono solo debe contener números")]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "Correo inválido")]
        public string? Correo { get; set; }

        public bool Activo { get; set; } = true;
    }
}