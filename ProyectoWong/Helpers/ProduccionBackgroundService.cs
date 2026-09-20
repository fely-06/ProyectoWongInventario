using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProyectoWong.Data;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Models.Produccion;

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
                            .Include(o => o.Detalles)
                            .Where(o => o.Estado == "EnProceso" && o.FechaInicio.HasValue)
                            .ToListAsync();

                        if (ordenesActivas.Any())
                        {
                            Console.WriteLine($"🔍 [SERVIDOR] Revisando {ordenesActivas.Count} órdenes en proceso...");
                        }

                        foreach (var orden in ordenesActivas)
                        {
                            double segundosTranscurridos = (ahora - orden.FechaInicio.Value).TotalSeconds - (orden.TiempoPausadoMinutos * 60);
                            double segundosRequeridos = orden.DuracionEstimadaMinutos * 60;

                            Console.WriteLine($"⏱ [SERVIDOR] Orden {orden.NumeroOP}: Llevan {segundosTranscurridos:F0}s de {segundosRequeridos:F0}s requeridos.");

                            if (segundosTranscurridos >= segundosRequeridos)
                            {
                                Console.WriteLine($"✅ [SERVIDOR] ¡TIEMPO CUMPLIDO! Completando orden {orden.NumeroOP} automáticamente...");

                                try
                                {
                                    // 🔥 USAMOS ExecuteUpdateAsync PARA GARANTIZAR QUE LOS CAMBIOS SE APLIQUEN 🔥

                                    // 1. Descontar componentes usando SQL directo
                                    foreach (var detalle in orden.Detalles)
                                    {
                                        await context.Productos
                                            .Where(p => p.Id == detalle.ComponenteId)
                                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Cantidad, p => p.Cantidad - detalle.CantidadRequerida));

                                        Console.WriteLine($"   ↳ Descontado {detalle.CantidadRequerida} del componente ID {detalle.ComponenteId}");
                                    }

                                    // 2. Aumentar producto final usando SQL directo
                                    await context.Productos
                                        .Where(p => p.Id == orden.ProductoId)
                                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Cantidad, p => p.Cantidad + (int)orden.CantidadAProducir));

                                    Console.WriteLine($"   ↳ Sumado {(int)orden.CantidadAProducir} al producto final ID {orden.ProductoId}");

                                    // 3. Marcar orden como completada usando SQL directo
                                    await context.OrdenProduccion
                                        .Where(o => o.Id == orden.Id)
                                        .ExecuteUpdateAsync(s => s
                                            .SetProperty(o => o.Estado, "Completada")
                                            .SetProperty(o => o.FechaFin, ahora));

                                    Console.WriteLine($"🎉 [SERVIDOR] ÉXITO: Orden {orden.NumeroOP} completada e inventario actualizado en BD.");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ [SERVIDOR] ERROR al completar {orden.NumeroOP}: {ex.Message}");
                                    _logger.LogError(ex, $"Error al completar {orden.NumeroOP}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [SERVIDOR] Error general en el ciclo: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}