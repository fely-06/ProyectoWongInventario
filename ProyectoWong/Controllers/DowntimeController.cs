using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models.Planta;

namespace ProyectoWong.Controllers
{
    [Route("Downtime")]
    public class DowntimeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public DowntimeController(ApplicationDbContext context) => _context = context;

        // 1. VISTA PRINCIPAL
        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Downtime";
            return View();
        }

        // 2. DATOS PARA LOS SELECTS: máquinas y causas de paro
        [HttpGet("catalogos")]
        public async Task<IActionResult> Catalogos()
        {
            try
            {
                var maquinas = await _context.Maquinas
                    .AsNoTracking()
                    .Where(m => m.Activa)
                    .OrderBy(m => m.Codigo)
                    .Select(m => new { id = m.Id, nombre = m.Codigo + " · " + m.Nombre })
                    .ToListAsync();

                return Json(Respuesta.OK("OK", new { maquinas, causas = CatalogosPlanta.CausasParo }));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // 3. CONSULTAR REGISTROS + RESUMEN (acumulado por máquina y causa con más pérdida)
        //    La vista lo consulta cada 3 s (igual que la simulación de Producción).
        [HttpGet("consultar")]
        public async Task<IActionResult> Consultar()
        {
            try
            {
                var ahora = DateTime.Now;

                var lista = await _context.RegistrosDowntime
                    .AsNoTracking()
                    .OrderByDescending(r => r.HoraInicio)
                    .Select(r => new
                    {
                        r.Id,
                        r.MaquinaId,
                        Maquina = r.Maquina.Nombre,
                        r.Causa,
                        r.HoraInicio,
                        r.HoraFin,
                        r.DuracionMinutos,
                        r.DuracionEstimadaMinutos,
                        r.Estado,
                        r.Simulado,
                        r.Observaciones
                    })
                    .ToListAsync();

                var maquinasActivas = await _context.Maquinas
                    .AsNoTracking()
                    .Where(m => m.Activa)
                    .OrderBy(m => m.Codigo)
                    .Select(m => new { m.Id, m.Codigo, m.Nombre })
                    .ToListAsync();

                // Minutos efectivos: si el paro sigue en curso se cuenta hasta este momento
                var items = lista.Select(x =>
                {
                    bool enCurso = x.Estado == "EnCurso" && !x.HoraFin.HasValue;
                    double minutos = enCurso
                        ? Math.Max(0, (ahora - x.HoraInicio).TotalMinutes)
                        : x.DuracionMinutos;
                    double progreso = enCurso && x.Simulado && x.DuracionEstimadaMinutos > 0
                        ? Math.Min(100, minutos / x.DuracionEstimadaMinutos * 100)
                        : (enCurso ? 0 : 100);
                    return new
                    {
                        x.Id,
                        x.MaquinaId,
                        x.Maquina,
                        x.Causa,
                        x.HoraInicio,
                        x.HoraFin,
                        x.Estado,
                        x.Simulado,
                        x.DuracionEstimadaMinutos,
                        x.Observaciones,
                        EnCurso = enCurso,
                        Minutos = Math.Round(minutos, 2),
                        Progreso = Math.Round(progreso, 1)
                    };
                }).ToList();

                double totalMinutos = items.Sum(i => i.Minutos);

                var porMaquina = maquinasActivas
                    .Select(m =>
                    {
                        var suyos = items.Where(i => i.MaquinaId == m.Id).ToList();
                        double min = suyos.Sum(i => i.Minutos);
                        return new
                        {
                            maquinaId = m.Id,
                            maquina = m.Codigo + " · " + m.Nombre,
                            paros = suyos.Count,
                            minutos = Math.Round(min, 1),
                            porcentaje = totalMinutos > 0 ? Math.Round(min / totalMinutos * 100, 1) : 0,
                            enParo = suyos.Any(i => i.EnCurso)
                        };
                    })
                    .OrderByDescending(m => m.minutos)
                    .ToList();

                var porCausa = items
                    .GroupBy(i => i.Causa)
                    .Select(g =>
                    {
                        double min = g.Sum(i => i.Minutos);
                        return new
                        {
                            causa = g.Key,
                            paros = g.Count(),
                            minutos = Math.Round(min, 1),
                            porcentaje = totalMinutos > 0 ? Math.Round(min / totalMinutos * 100, 1) : 0
                        };
                    })
                    .OrderByDescending(c => c.minutos)
                    .ToList();

                var causaTop = porCausa.FirstOrDefault();

                return Json(Respuesta.OK("OK", new
                {
                    registros = items.Select(i => new
                    {
                        id = i.Id,
                        maquinaId = i.MaquinaId,
                        maquina = i.Maquina,
                        causa = i.Causa,
                        horaInicio = i.HoraInicio,
                        horaFin = i.HoraFin,
                        estado = i.Estado,
                        simulado = i.Simulado,
                        duracionEstimadaMinutos = i.DuracionEstimadaMinutos,
                        observaciones = i.Observaciones,
                        enCurso = i.EnCurso,
                        minutos = i.Minutos,
                        progreso = i.Progreso
                    }),
                    porMaquina,
                    porCausa,
                    resumen = new
                    {
                        totalMinutos = Math.Round(totalMinutos, 1),
                        totalParos = items.Count,
                        enCurso = items.Count(i => i.EnCurso),
                        causaTop = causaTop?.causa ?? "—",
                        causaTopMinutos = causaTop?.minutos ?? 0
                    }
                }));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // 4. GUARDAR PARO MANUAL (Id = 0 crea, Id > 0 edita). Duración = Fin - Inicio.
        [HttpPost("guardar")]
        public async Task<IActionResult> Guardar([FromBody] DowntimeRequest request)
        {
            try
            {
                if (request == null) return Json(Respuesta.Error("Datos inválidos."));
                if (!await MaquinaValida(request.MaquinaId)) return Json(Respuesta.Error("Selecciona una máquina válida."));
                if (!CatalogosPlanta.CausasParo.Contains(request.Causa)) return Json(Respuesta.Error("Selecciona una causa de paro válida."));

                if (!TryFecha(request.HoraInicio, out var inicio)) return Json(Respuesta.Error("La hora de inicio no es válida."));
                if (!TryFecha(request.HoraFin, out var fin)) return Json(Respuesta.Error("La hora de fin no es válida."));
                if (fin <= inicio) return Json(Respuesta.Error("La hora de fin debe ser posterior a la hora de inicio."));
                if (fin > DateTime.Now.AddMinutes(1)) return Json(Respuesta.Error("La hora de fin no puede estar en el futuro."));

                if (await HayTraslape(request.MaquinaId, inicio, fin, request.Id))
                    return Json(Respuesta.Error("Esa máquina ya tiene un paro registrado que se empalma con ese horario."));

                var observaciones = LimpiarObs(request.Observaciones);
                double minutos = Math.Round((fin - inicio).TotalMinutes, 2);

                if (request.Id > 0)
                {
                    var existente = await _context.RegistrosDowntime.FirstOrDefaultAsync(r => r.Id == request.Id);
                    if (existente == null) return Json(Respuesta.Error("El registro ya no existe."));
                    if (existente.Estado == "EnCurso") return Json(Respuesta.Error("Finaliza el paro en curso antes de editarlo."));

                    existente.MaquinaId = request.MaquinaId;
                    existente.Causa = request.Causa;
                    existente.HoraInicio = inicio;
                    existente.HoraFin = fin;
                    existente.DuracionMinutos = minutos;
                    existente.Observaciones = observaciones;
                    await _context.SaveChangesAsync();
                    return Json(Respuesta.OK($"Paro actualizado. Duración: {minutos} min."));
                }

                _context.RegistrosDowntime.Add(new RegistroDowntime
                {
                    MaquinaId = request.MaquinaId,
                    Causa = request.Causa,
                    HoraInicio = inicio,
                    HoraFin = fin,
                    DuracionMinutos = minutos,
                    Estado = "Finalizado",
                    Simulado = false,
                    Observaciones = observaciones
                });
                await _context.SaveChangesAsync();
                return Json(Respuesta.OK($"Paro registrado. Duración: {minutos} min."));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // 5. ELIMINAR
        [HttpPost("eliminar/{id}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            try
            {
                var registro = await _context.RegistrosDowntime.FirstOrDefaultAsync(r => r.Id == id);
                if (registro == null) return Json(Respuesta.Error("El registro ya no existe."));

                _context.RegistrosDowntime.Remove(registro);
                await _context.SaveChangesAsync();
                return Json(Respuesta.OK("Registro eliminado."));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // 6. SIMULACIÓN: INICIAR PARO (se cierra solo al cumplirse la duración, lo hace DowntimeBackgroundService)
        [HttpPost("simular/iniciar")]
        public async Task<IActionResult> IniciarSimulacion([FromBody] SimularParoRequest request)
        {
            try
            {
                if (request == null) return Json(Respuesta.Error("Datos inválidos."));
                if (!await MaquinaValida(request.MaquinaId)) return Json(Respuesta.Error("Selecciona una máquina válida."));
                if (!CatalogosPlanta.CausasParo.Contains(request.Causa)) return Json(Respuesta.Error("Selecciona una causa de paro válida."));
                if (request.DuracionMinutos <= 0 || request.DuracionMinutos > 480)
                    return Json(Respuesta.Error("La duración simulada debe estar entre 0.1 y 480 minutos."));

                var ahora = DateTime.Now;
                if (await HayTraslape(request.MaquinaId, ahora, ahora.AddMinutes(request.DuracionMinutos), 0))
                    return Json(Respuesta.Error("Esa máquina ya está detenida o tiene un paro en ese horario."));

                _context.RegistrosDowntime.Add(new RegistroDowntime
                {
                    MaquinaId = request.MaquinaId,
                    Causa = request.Causa,
                    HoraInicio = ahora,
                    HoraFin = null,
                    DuracionMinutos = 0,
                    DuracionEstimadaMinutos = request.DuracionMinutos,
                    Estado = "EnCurso",
                    Simulado = true
                });
                await _context.SaveChangesAsync();
                return Json(Respuesta.OK($"Paro simulado iniciado. Se cerrará solo en {request.DuracionMinutos} min."));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // 7. SIMULACIÓN: FINALIZAR AHORA (sin esperar a que se cumpla el tiempo)
        [HttpPost("simular/finalizar/{id}")]
        public async Task<IActionResult> FinalizarSimulacion(int id)
        {
            try
            {
                var registro = await _context.RegistrosDowntime.FirstOrDefaultAsync(r => r.Id == id);
                if (registro == null || registro.Estado != "EnCurso")
                    return Json(Respuesta.Error("Ese paro ya no está en curso."));

                var ahora = DateTime.Now;
                registro.HoraFin = ahora;
                registro.DuracionMinutos = Math.Round(Math.Max(0, (ahora - registro.HoraInicio).TotalMinutes), 2);
                registro.Estado = "Finalizado";
                await _context.SaveChangesAsync();
                return Json(Respuesta.OK($"Paro finalizado. Duración: {registro.DuracionMinutos} min."));
            }
            catch (Exception ex)
            {
                return Json(Respuesta.Error(MensajeBD(ex)));
            }
        }

        // ---------- helpers ----------
        private Task<bool> MaquinaValida(int id) => _context.Maquinas.AnyAsync(m => m.Id == id && m.Activa);

        // ¿La máquina ya tiene un paro que se empalma con [inicio, fin)?
        private Task<bool> HayTraslape(int maquinaId, DateTime inicio, DateTime fin, int excluirId) =>
            _context.RegistrosDowntime.AnyAsync(x =>
                x.MaquinaId == maquinaId
                && x.Id != excluirId
                && x.HoraInicio < fin
                && (x.HoraFin ?? DateTime.MaxValue) > inicio);

        private static bool TryFecha(string? texto, out DateTime fecha) =>
            DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

        private static string? LimpiarObs(string? obs)
        {
            if (string.IsNullOrWhiteSpace(obs)) return null;
            obs = obs.Trim();
            return obs.Length > 300 ? obs[..300] : obs;
        }

        private static string MensajeBD(Exception ex) =>
            "No se pudo completar la operación. ¿Ya se ejecutó Database/scrap_downtime.sql? Detalle: "
            + (ex.InnerException?.Message ?? ex.Message);
    }

    public class DowntimeRequest
    {
        public int Id { get; set; }
        public int MaquinaId { get; set; }
        public string Causa { get; set; } = string.Empty;
        public string? HoraInicio { get; set; }
        public string? HoraFin { get; set; }
        public string? Observaciones { get; set; }
    }

    public class SimularParoRequest
    {
        public int MaquinaId { get; set; }
        public string Causa { get; set; } = string.Empty;
        public double DuracionMinutos { get; set; }
    }
}
