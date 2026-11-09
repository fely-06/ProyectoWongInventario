using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models.Personal;

namespace ProyectoWong.Controllers
{
    // Módulo de Personal: empleados, turnos, asignaciones a máquinas, asistencia y reportes.
    [Route("Personal")]
    public class PersonalController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PersonalController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Personal";
            return View();
        }

        // Todo lo que necesita la pantalla en una sola consulta
        [HttpGet("datos")]
        public async Task<IActionResult> Datos()
        {
            var hoy = DateTime.Today;

            var empleados = await _context.Empleados.OrderBy(e => e.Nombre).ToListAsync();
            var turnos = await _context.Turnos.OrderBy(t => t.HoraEntrada).ToListAsync();
            var maquinas = await _context.Maquinas.Where(m => m.Activa)
                .Select(m => new { m.Id, m.Nombre }).ToListAsync();

            var asignaciones = await _context.AsignacionesTurno
                .OrderByDescending(a => a.Id)
                .Select(a => new
                {
                    a.Id,
                    a.EmpleadoId,
                    empleado = a.Empleado!.Nombre,
                    turno = a.Turno!.Nombre,
                    maquina = a.Maquina != null ? a.Maquina.Nombre : null,
                    a.Linea,
                    a.Dias,
                    a.FechaInicio,
                    a.FechaFin
                }).ToListAsync();

            var asistenciaHoy = await _context.Asistencias
                .Where(a => a.Fecha == hoy)
                .Select(a => new { a.EmpleadoId, a.Entrada, a.Salida, a.Tipo, a.Observaciones })
                .ToListAsync();

            return Json(Respuesta.OK("Consulta exitosa", new { empleados, turnos, maquinas, asignaciones, asistenciaHoy }));
        }

        // ---------- Empleados ----------
        [HttpPost("guardar-empleado")]
        public async Task<IActionResult> GuardarEmpleado([FromBody] Empleado empleado)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));

            bool repetido = await _context.Empleados.AnyAsync(e => e.NumeroEmpleado == empleado.NumeroEmpleado && e.Id != empleado.Id);
            if (repetido) return Json(Respuesta.Error("Ya existe un empleado con ese número"));

            _context.Empleados.Update(empleado); // Id = 0 lo da de alta, Id > 0 lo edita
            await _context.SaveChangesAsync();
            return Json(Respuesta.OK("Empleado guardado"));
        }

        // ---------- Turnos ----------
        [HttpPost("guardar-turno")]
        public async Task<IActionResult> GuardarTurno([FromBody] Turno turno)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));

            _context.Turnos.Update(turno);
            await _context.SaveChangesAsync();
            return Json(Respuesta.OK("Turno guardado"));
        }

        // ---------- Asignaciones ----------
        // Asigna (o cambia) el turno, máquina, línea y días de un empleado.
        // La asignación vigente anterior se cierra con la fecha de hoy y queda en el historial.
        [HttpPost("asignar")]
        public async Task<IActionResult> Asignar([FromBody] AsignacionTurno asignacion)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));

            var anteriores = await _context.AsignacionesTurno
                .Where(a => a.EmpleadoId == asignacion.EmpleadoId && a.FechaFin == null)
                .ToListAsync();
            foreach (var a in anteriores)
                a.FechaFin = DateTime.Today;

            asignacion.Id = 0;
            asignacion.FechaInicio = DateTime.Today;
            asignacion.FechaFin = null;
            _context.AsignacionesTurno.Add(asignacion);

            await _context.SaveChangesAsync();
            return Json(Respuesta.OK(anteriores.Count > 0 ? "Cambio de turno registrado" : "Asignación registrada"));
        }

        // ---------- Asistencia ----------
        [HttpPost("entrada/{empleadoId}")]
        public async Task<IActionResult> Entrada(int empleadoId)
        {
            var hoy = DateTime.Today;
            if (await _context.Asistencias.AnyAsync(a => a.EmpleadoId == empleadoId && a.Fecha == hoy))
                return Json(Respuesta.Error("El empleado ya tiene registro el día de hoy"));

            _context.Asistencias.Add(new Asistencia
            {
                EmpleadoId = empleadoId,
                Fecha = hoy,
                Entrada = DateTime.Now,
                Tipo = "Asistencia"
            });
            await _context.SaveChangesAsync();
            return Json(Respuesta.OK("Entrada registrada"));
        }

        [HttpPost("salida/{empleadoId}")]
        public async Task<IActionResult> Salida(int empleadoId)
        {
            // La última entrada sin salida (puede ser de ayer si es turno nocturno)
            var registro = await _context.Asistencias
                .Where(a => a.EmpleadoId == empleadoId && a.Entrada != null && a.Salida == null)
                .OrderByDescending(a => a.Entrada)
                .FirstOrDefaultAsync();

            if (registro == null)
                return Json(Respuesta.Error("El empleado no tiene una entrada abierta"));

            registro.Salida = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(Respuesta.OK("Salida registrada"));
        }

        [HttpPost("ausencia")]
        public async Task<IActionResult> Ausencia([FromBody] Asistencia ausencia)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));
            if (ausencia.Tipo == "Asistencia")
                return Json(Respuesta.Error("Selecciona el tipo de ausencia"));

            ausencia.Fecha = ausencia.Fecha.Date;
            if (await _context.Asistencias.AnyAsync(a => a.EmpleadoId == ausencia.EmpleadoId && a.Fecha == ausencia.Fecha))
                return Json(Respuesta.Error("El empleado ya tiene registro ese día"));

            ausencia.Id = 0;
            ausencia.Entrada = null;
            ausencia.Salida = null;
            _context.Asistencias.Add(ausencia);
            await _context.SaveChangesAsync();
            return Json(Respuesta.OK("Ausencia registrada"));
        }

        // ---------- Reportes ----------
        [HttpGet("reporte")]
        public async Task<IActionResult> Reporte(DateTime desde, DateTime hasta)
        {
            // Asistencia y horas trabajadas por empleado en el periodo
            var registros = await _context.Asistencias
                .Where(a => a.Fecha >= desde.Date && a.Fecha <= hasta.Date)
                .ToListAsync();
            var empleados = await _context.Empleados.Where(e => e.Activo).OrderBy(e => e.Nombre).ToListAsync();

            var porEmpleado = empleados.Select(e =>
            {
                var suyos = registros.Where(a => a.EmpleadoId == e.Id).ToList();
                return new
                {
                    empleado = e.Nombre,
                    asistencias = suyos.Count(a => a.Tipo == "Asistencia"),
                    ausencias = suyos.Count(a => a.Tipo != "Asistencia"),
                    horas = Math.Round(suyos
                        .Where(a => a.Entrada != null && a.Salida != null)
                        .Sum(a => (a.Salida!.Value - a.Entrada!.Value).TotalHours), 1)
                };
            });

            // Cobertura de hoy: por turno, cuántos debían venir y cuántos llegaron
            var hoy = DateTime.Today;
            string letraHoy = "DLMXJVS"[(int)hoy.DayOfWeek].ToString();

            var tocanHoy = (await _context.AsignacionesTurno
                    .Include(a => a.Turno)
                    .Where(a => a.FechaFin == null && a.Empleado!.Activo)
                    .ToListAsync())
                .Where(a => a.Dias.Split(',').Contains(letraHoy));

            var llegaron = await _context.Asistencias
                .Where(a => a.Fecha == hoy && a.Tipo == "Asistencia")
                .Select(a => a.EmpleadoId)
                .ToListAsync();

            var cobertura = tocanHoy
                .GroupBy(a => a.Turno!.Nombre)
                .Select(g => new
                {
                    turno = g.Key,
                    programados = g.Count(),
                    presentes = g.Count(a => llegaron.Contains(a.EmpleadoId))
                });

            return Json(Respuesta.OK("Reporte generado", new { porEmpleado, cobertura }));
        }
    }
}
