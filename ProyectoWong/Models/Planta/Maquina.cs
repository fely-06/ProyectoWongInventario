using System.ComponentModel.DataAnnotations;

namespace ProyectoWong.Models.Planta
{
    // Catálogo de máquinas / estaciones de la línea. Lo usan Scrap y Downtime.
    public class Maquina
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Codigo { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        public bool Activa { get; set; } = true;
    }
}
