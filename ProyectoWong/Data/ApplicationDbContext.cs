using Microsoft.EntityFrameworkCore;
using ProyectoWong.Models;

namespace ProyectoWong.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Usuarios> Usuarios { get; set; }
        public DbSet<Componente> Componentes { get; set; }  // ← ESTO DEBE ESTAR

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuarios>()
                .HasKey(u => u.ExpUsuarioId);

            modelBuilder.Entity<Componente>()
                .HasKey(c => c.Id);
        }
    }
}
