using System.ComponentModel.DataAnnotations;
using ProyectoWong.Models.Planta;

namespace ProyectoWong.Models.Personal
{
    // Empleado de la planta (operador, supervisor, etc.)
    public class Empleado
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El número de empleado es obligatorio")]
        [StringLength(20)]
        public string NumeroEmpleado { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El puesto es obligatorio")]
        [StringLength(30)]
        public string Puesto { get; set; } = "Operador";

        public bool Activo { get; set; } = true;
    }

    // Turno de trabajo, p. ej. "Matutino" de 06:00 a 14:00
    public class Turno
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del turno es obligatorio")]
        [StringLength(50)]
        public string Nombre { get; set; } = string.Empty;

        public TimeSpan HoraEntrada { get; set; }
        public TimeSpan HoraSalida { get; set; }
    }

    // Qué turno, máquina/línea y días trabaja un empleado.
    // FechaFin vacía = asignación vigente. Al cambiar de turno se cierra la anterior,
    // así las asignaciones viejas quedan como historial.
    public class AsignacionTurno
    {
        public int Id { get; set; }

        public int EmpleadoId { get; set; }
        public Empleado? Empleado { get; set; }

        public int TurnoId { get; set; }
        public Turno? Turno { get; set; }

        public int? MaquinaId { get; set; }
        public Maquina? Maquina { get; set; }

        [StringLength(50)]
        public string? Linea { get; set; }

        // Días de la semana que trabaja: L,M,X,J,V,S,D
        [Required(ErrorMessage = "Selecciona al menos un día")]
        [StringLength(20)]
        public string Dias { get; set; } = "L,M,X,J,V";

        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
    }

    // Un registro por empleado por día: su entrada/salida o el motivo de su ausencia.
    public class Asistencia
    {
        public int Id { get; set; }

        public int EmpleadoId { get; set; }
        public Empleado? Empleado { get; set; }

        public DateTime Fecha { get; set; }
        public DateTime? Entrada { get; set; }
        public DateTime? Salida { get; set; }

        // "Asistencia" o el tipo de ausencia: Falta, Permiso, Vacaciones, Incapacidad
        [Required]
        [StringLength(20)]
        public string Tipo { get; set; } = "Asistencia";

        [StringLength(200)]
        public string? Observaciones { get; set; }
    }
}
