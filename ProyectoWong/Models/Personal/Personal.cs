using System.ComponentModel.DataAnnotations;
using ProyectoWong.Models.Planta;

namespace ProyectoWong.Models.Personal
{
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

    public class Turno
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del turno es obligatorio")]
        [StringLength(50)]
        public string Nombre { get; set; } = string.Empty;

        public TimeSpan HoraEntrada { get; set; }
        public TimeSpan HoraSalida { get; set; }
    }

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

        [Required(ErrorMessage = "Selecciona al menos un día")]
        [StringLength(20)]
        public string Dias { get; set; } = "L,M,X,J,V"; // L,M,X,J,V,S,D

        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; } // null = vigente
    }

    public class Asistencia
    {
        public int Id { get; set; }

        public int EmpleadoId { get; set; }
        public Empleado? Empleado { get; set; }

        public DateTime Fecha { get; set; }
        public DateTime? Entrada { get; set; }
        public DateTime? Salida { get; set; }

        [Required]
        [StringLength(20)]
        public string Tipo { get; set; } = "Asistencia"; // o Falta, Permiso, Vacaciones, Incapacidad

        [StringLength(200)]
        public string? Observaciones { get; set; }
    }
}
