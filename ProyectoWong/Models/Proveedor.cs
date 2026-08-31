using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ProyectoWong.Models
{
    public class Proveedor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Se debe ingresar un nombre de proveedor")]
        public string Nombre { get; set; } = string.Empty;

        [RegularExpression(@"^\d{10}$", ErrorMessage = " El número de teléfono debe de contar con 10 digitos")]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage ="Correo inválido")]
        public string? Correo { get; set; }

        public bool Activo { get; set; } = true;
    }
}
