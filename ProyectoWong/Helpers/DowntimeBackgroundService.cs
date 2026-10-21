using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;

namespace ProyectoWong.Helpers
{
    // Igual que ProduccionBackgroundService: revisa cada 3 s los paros simulados en curso
    // y, cuando ya pasó su duración, los cierra (HoraFin, duración y estado "Finalizado").
    public class DowntimeBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DowntimeBackgroundService> _logger;

        public DowntimeBackgroundService(IServiceScopeFactory scopeFactory, ILogger<DowntimeBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("🚀 [SERVIDOR] Servicio de Downtime INICIADO y corriendo en segundo plano.");
            string ultimoError = "";

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var ahora = DateTime.Now;

                    var abiertos = await context.RegistrosDowntime
                        .Where(r => r.Estado == "EnCurso" && r.Simulado)
                        .ToListAsync(stoppingToken);

                    foreach (var paro in abiertos)
                    {
                        double minutosTranscurridos = (ahora - paro.HoraInicio).TotalMinutes;
                        if (minutosTranscurridos < paro.DuracionEstimadaMinutos) continue;

                        // Se cierra exactamente a la duración simulada
                        paro.HoraFin = paro.HoraInicio.AddMinutes(paro.DuracionEstimadaMinutos);
                        paro.DuracionMinutos = Math.Round(paro.DuracionEstimadaMinutos, 2);
                        paro.Estado = "Finalizado";
                        Console.WriteLine($"✅ [SERVIDOR] Paro #{paro.Id} finalizado automáticamente ({paro.DuracionMinutos} min).");
                    }

                    if (context.ChangeTracker.HasChanges())
                        await context.SaveChangesAsync(stoppingToken);

                    ultimoError = "";
                }
                catch (OperationCanceledException)
                {
                    break; // la app se está apagando
                }
                catch (Exception ex)
                {
                    // Evita repetir el mismo error cada 3 s (p. ej. si aún no existen las tablas)
                    if (ex.Message != ultimoError)
                    {
                        Console.WriteLine($"❌ [SERVIDOR] Error en el servicio de Downtime: {ex.Message}");
                        _logger.LogError(ex, "Error en DowntimeBackgroundService");
                        ultimoError = ex.Message;
                    }
                }

                try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
