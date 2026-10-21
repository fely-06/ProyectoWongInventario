using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoWong.Models.Planta
{
    // Un paro de máquina. HoraFin es null mientras el paro está en curso (simulación).
    public class RegistroDowntime
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MaquinaId { get; set; }

        [ForeignKey(nameof(MaquinaId))]
        public Maquina Maquina { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Causa { get; set; } = string.Empty;

        public DateTime HoraInicio { get; set; }
        public DateTime? HoraFin { get; set; }

        // Se calcula al guardar o finalizar: (HoraFin - HoraInicio) en minutos.
        public double DuracionMinutos { get; set; }

        // Solo para paros simulados: cuánto dura antes de cerrarse solo.
        public double DuracionEstimadaMinutos { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Finalizado"; // "EnCurso" | "Finalizado"

        public bool Simulado { get; set; }

        [StringLength(300)]
        public string? Observaciones { get; set; }
    }
}
