using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProyectoWong.Models
{
    public class Usuarios
    {
        [Key]
        public int ExpUsuarioId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string Email { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaAlta { get; set; } = DateTime.Now;

        [JsonIgnore]
        public string? PasswordHash { get; set; }
    }
}