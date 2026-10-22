using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProyectoWong.Data;
using Microsoft.EntityFrameworkCore;

namespace ProyectoWong.Helpers
{
    public class ProduccionBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ProduccionBackgroundService> _logger;

        public ProduccionBackgroundService(IServiceScopeFactory serviceScopeFactory, ILogger<ProduccionBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("🚀 [SERVIDOR] Servicio de Producción INICIADO y corriendo en segundo plano.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var ahora = DateTime.Now;

                        // Obtener solo las órdenes en proceso (sin tracking para mejor rendimiento)
                        var ordenesActivas = await context.OrdenProduccion
                            .AsNoTracking()
                            .Where(o => o.Estado == "EnProceso" && o.FechaInicio.HasValue)
                            .Select(o => new { o.Id, o.NumeroOP, FechaInicio = o.FechaInicio!.Value, o.TiempoPausadoMinutos, o.DuracionEstimadaMinutos })
                            .ToListAsync(stoppingToken);

                        foreach (var orden in ordenesActivas)
                        {
                            double segundosTranscurridos = (ahora - orden.FechaInicio).TotalSeconds - (orden.TiempoPausadoMinutos * 60);
                            double segundosRequeridos = orden.DuracionEstimadaMinutos * 60;

                            if (segundosTranscurridos < segundosRequeridos) continue;

                            Console.WriteLine($"✅ [SERVIDOR] ¡TIEMPO CUMPLIDO! Finalizando fabricación de {orden.NumeroOP}...");

                            try
                            {
                                // Misma lógica que el botón "Completar": descuenta componentes,
                                // suma el producto terminado y pasa la orden a Inspección Visual.
                                if (await ProduccionFlujo.FinalizarFabricacionAsync(context, orden.Id, stoppingToken))
                                    Console.WriteLine($"🎉 [SERVIDOR] Orden {orden.NumeroOP} enviada a Inspección Visual e inventario actualizado.");

                                context.ChangeTracker.Clear();
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                Console.WriteLine($"❌ [SERVIDOR] ERROR al completar {orden.NumeroOP}: {ex.Message}");
                                _logger.LogError(ex, "Error al completar {NumeroOP}", orden.NumeroOP);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break; // la app se está apagando
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [SERVIDOR] Error general en el ciclo: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
