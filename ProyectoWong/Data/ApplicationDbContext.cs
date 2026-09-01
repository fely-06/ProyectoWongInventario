using Microsoft.EntityFrameworkCore;
using ProyectoWong.Models;
using ProyectoWong.Models.Recepcion;

namespace ProyectoWong.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Usuarios> Usuarios { get; set; }
        public DbSet<Componente> Componentes { get; set; }
        public DbSet<OrdenCompra> OrdenesCompra { get; set; }
        public DbSet<OrdenCompraDetalle> OrdenCompraDetalle { get; set; }

        public DbSet<Recepcion> Recepciones { get; set; }
        public DbSet<RecepcionDetalle> RecepcionDetalles { get; set; }
        public DbSet<InspeccionQA> InspeccionesQA { get; set; }
        public DbSet<Pallet> Pallets { get; set; }
        public DbSet<Ubicacion> Ubicaciones { get; set; }
        public DbSet<MovimientoInventario> MovimientosInventario { get; set; }
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<ProductoComponente> ProductoComponentes { get; set; }
        public DbSet<EscalaDescuento> EscalasDescuento { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Usuarios
            modelBuilder.Entity<Usuarios>().HasKey(u => u.ExpUsuarioId);

            // Componente
            modelBuilder.Entity<Componente>().HasKey(c => c.Id);
            modelBuilder.Entity<ProductoComponente>()
    .HasOne(pc => pc.Producto)
    .WithMany(p => p.Componentes)
    .HasForeignKey(pc => pc.ProductoId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductoComponente>()
                .HasOne(pc => pc.Componente)
                .WithMany()
                .HasForeignKey(pc => pc.ComponenteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EscalaDescuento>()
    .HasOne(e => e.Producto)
    .WithMany(p => p.EscalasDescuento)  // ← Con navegación bidireccional
    .HasForeignKey(e => e.ProductoId)
    .OnDelete(DeleteBehavior.Cascade);
            // OrdenCompra -> OrdenCompraDetalle
            modelBuilder.Entity<OrdenCompraDetalle>()
                .HasOne(od => od.OrdenCompra)
                .WithMany(oc => oc.Detalles)
                .HasForeignKey(od => od.OrdenCmpraId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrdenCompraDetalle>()
                .HasOne(od => od.Componente)
                .WithMany()
                .HasForeignKey(od => od.ComponenteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Recepcion -> RecepcionDetalle
            modelBuilder.Entity<RecepcionDetalle>()
                .HasOne(rd => rd.Recepcion)
                .WithMany(r => r.Detalles)
                .HasForeignKey(rd => rd.RecepcionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RecepcionDetalle>()
                .HasOne(rd => rd.Componente)
                .WithMany()
                .HasForeignKey(rd => rd.ComponenteId)
                .OnDelete(DeleteBehavior.Restrict);

            // InspeccionQA
            modelBuilder.Entity<InspeccionQA>()
                .HasOne(i => i.Inspector)
                .WithMany()
                .HasForeignKey(i => i.InspeccionadoPor)
                .OnDelete(DeleteBehavior.NoAction);

            // Recepcion (Usuario)
            modelBuilder.Entity<Recepcion>()
                .HasOne(r => r.Usuario)
                .WithMany()
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.NoAction);

            // MovimientoInventario (Usuario, Pallet, Ubicacion)
            modelBuilder.Entity<MovimientoInventario>()
                .HasOne(m => m.Usuario)
                .WithMany()
                .HasForeignKey(m => m.RealizadoPor)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MovimientoInventario>()
                .HasOne(m => m.Pallet)
                .WithMany(p => p.Movimientos)
                .HasForeignKey(m => m.PalletId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MovimientoInventario>()
                .HasOne(m => m.Ubicacion)
                .WithMany(u => u.Movimientos)
                .HasForeignKey(m => m.UbicacionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Pallet -> RecepcionDetalle
            modelBuilder.Entity<Pallet>()
                .HasOne(p => p.RecepcionDetalle)
                .WithMany(rd => rd.Pallets)
                .HasForeignKey(p => p.RecepcionDetalleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}