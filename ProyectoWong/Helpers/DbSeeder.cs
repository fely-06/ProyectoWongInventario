using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Models;
namespace ProyectoWong.Helpers
{
    public static class DbSeeder
    {
        // Si la tabla Usuarios está vacía, crea un admin por defecto.
        public static void SeedAdminUser(ApplicationDbContext context)
        {
            if (context.Usuarios.Any())
                return; // ya hay usuarios, no hacer nada

            var admin = new Usuarios
            {
                NombreCompleto = "Administrador",
                Email = "admin@wong.com",
                Activo = true,
                FechaAlta = DateTime.Now,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345")
            };

            context.Usuarios.Add(admin);
            context.SaveChanges();
        }

    }
}
