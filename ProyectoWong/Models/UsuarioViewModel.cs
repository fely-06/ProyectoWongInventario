using System.ComponentModel.DataAnnotations;

namespace ProyectoWong.Models
{
    public class UsuarioViewModel
    {
        public int ExpUsuarioId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio")]
        public string Email { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        public bool Activo { get; set; }

        // Este campo solo existe en el formulario, no en la base de datos
        public string? Password { get; set; }
    }
}
