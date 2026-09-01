namespace ProyectoWong.Models
{
    public class DashboardViewModel
    {
        // ── KPIs principales ─────────────────────────────────────────
        public int TotalComponentes { get; set; }
        public int ComponentesBajoMinimo { get; set; }
        public int OrdenesCompraAbiertas { get; set; }
        public int RecepcionesEnProceso { get; set; }
        public int PalletsSinUbicacion { get; set; }
        public int UbicacionesActivas { get; set; }

        // ── Listas de apoyo ──────────────────────────────────────────
        public List<ComponenteBajoStockItem> ComponentesCriticos { get; set; } = new();
        public List<MovimientoRecienteItem> UltimosMovimientos { get; set; } = new();
        public List<RecepcionPendienteItem> RecepcionesPendientes { get; set; } = new();
    }

    public class ComponenteBajoStockItem
    {
        public int Id { get; set; }
        public string NumeroPieza { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public int MinimoInventario { get; set; }
    }

    public class MovimientoRecienteItem
    {
        public string CodigoPallet { get; set; } = string.Empty;
        public string? Componente { get; set; }
        public string? Ubicacion { get; set; }
        public string TipoMovimiento { get; set; } = string.Empty;
        public DateTime FechaMovimiento { get; set; }
        public string? RealizadoPor { get; set; }
    }

    public class RecepcionPendienteItem
    {
        public int Id { get; set; }
        public string NumeroOC { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaRecepcion { get; set; }
        public int CantidadLotes { get; set; }
    }
}
