using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;

namespace ProyectoWong.Helpers
{
    // Lógica única para terminar la fabricación de una orden. La usan el botón
    // "Completar" (ProduccionController), el envío a inspección (MonitoreoVisual)
    // y el cierre automático (ProduccionBackgroundService), para que el
    // inventario se mueva igual sin importar quién termine la orden.
    public static class ProduccionFlujo
    {
        // Estados en los que la orden todavía se está fabricando
        public static readonly string[] EstadosFabricando = { "Pendiente", "EnProceso", "Pausada" };

        // Estados en los que la fabricación ya terminó (inventario ya movido)
        public static readonly string[] EstadosFabricada = { "EnInspeccion", "EnPruebasTecnicas", "Completada" };

        // Descuenta componentes, suma el producto terminado y pasa la orden a "EnInspeccion".
        // Devuelve false si la orden no existe o ya no está fabricándose (por ejemplo,
        // porque otro proceso la terminó un instante antes): en ese caso no toca el inventario.
        public static async Task<bool> FinalizarFabricacionAsync(ApplicationDbContext context, int ordenId, CancellationToken ct = default)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            // Cambio de estado atómico: solo una petición puede "ganar" la orden,
            // así el inventario nunca se descuenta dos veces.
            int filas = await context.OrdenProduccion
                .Where(o => o.Id == ordenId && EstadosFabricando.Contains(o.Estado))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Estado, "EnInspeccion")
                    .SetProperty(o => o.FechaPausa, (DateTime?)null), ct);

            if (filas == 0)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            var orden = await context.OrdenProduccion
                .Include(o => o.Producto)
                .Include(o => o.Detalles!).ThenInclude(d => d.Componente)
                .FirstAsync(o => o.Id == ordenId, ct);

            // 1. Descontar componentes del inventario
            foreach (var detalle in orden.Detalles ?? [])
            {
                if (detalle.Componente != null)
                    detalle.Componente.Cantidad -= detalle.CantidadRequerida;
                detalle.CantidadConsumida = detalle.CantidadRequerida;
            }

            // 2. Aumentar stock del producto terminado (ya fabricado, pendiente de calidad)
            if (orden.Producto != null)
                orden.Producto.Cantidad += orden.CantidadAProducir;

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
    }
}
