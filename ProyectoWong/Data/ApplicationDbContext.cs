using Microsoft.EntityFrameworkCore;
using ProyectoWong.Models;
using ProyectoWong.Models.Recepcion;

namespace ProyectoWong.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Usuarios> Usuarios { get; set; }
        public DbSet<Componente> Componentes { get; set; }  // ← ESTO DEBE ESTAR
        public DbSet<OrdenCompra> OrdenesCompra {  get; set; }
        public DbSet<OrdenCompraDetalle> OrdenCompraDetalle {  get; set; }
        public DbSet<Recepcion> Recepciones {  get; set; }
        public DbSet<RecepcionDetalle> RecepcionDetalles { get; set; }
        public DbSet<InspeccionQA> InspeccionesQA {  get; set; }
        public DbSet<Pallet> Pallets { get; set; }
        public DbSet<Ubicacion> Ubicaciones {  get; set; }
        public DbSet<MovimientoInventario> MovimientosInventario { get; set; }
        public DbSet<Proveedor> Proveedores {  get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuarios>()
                .HasKey(u => u.ExpUsuarioId);

            modelBuilder.Entity<Componente>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<InspeccionQA>()
               .HasOne(i => i.Inspector)
               .WithMany()
               .HasForeignKey(i => i.InspeccionadoPor)
               .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Recepcion>()
                .HasOne(r => r.Usuario)
                .WithMany()
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MovimientoInventario>()
                .HasOne(m => m.Usuario)
                .WithMany()
                .HasForeignKey(m => m.RealizadoPor)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
