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

        // Si el producto "Mini Ventilador Portátil V1" no existe, crea sus 4
        // componentes (motor, aspas, case y batería), el producto y la receta
        // (ProductoComponente) para poder correr una simulación de producción.
        public static void SeedVentiladorPortatil(ApplicationDbContext context)
        {
            if (context.Productos.Any(p => p.Nombre == "Mini Ventilador Portátil V1"))
                return; // ya existe, no hacer nada

            var motor = new Componente
            {
                NumeroPieza = "MOT-001",
                Nombre = "Motor DC 3V",
                Descripcion = "Micromotor DC de 3V para ventilador portátil",
                Categoria = "Ventilador Portátil",
                UnidadMedida = "unidad",
                Cantidad = 100,
                MinimoInventario = 20,
                MaximoInventario = 500,
                Precio = 3.50m,
                Activo = true
            };

            var aspas = new Componente
            {
                NumeroPieza = "ASP-001",
                Nombre = "Juego de Aspas",
                Descripcion = "Aspa plástica para ventilador portátil",
                Categoria = "Ventilador Portátil",
                UnidadMedida = "unidad",
                Cantidad = 300,
                MinimoInventario = 60,
                MaximoInventario = 1500,
                Precio = 0.80m,
                Activo = true
            };

            var carcasa = new Componente
            {
                NumeroPieza = "CASE-001",
                Nombre = "Case / Carcasa",
                Descripcion = "Carcasa plástica exterior del ventilador portátil",
                Categoria = "Ventilador Portátil",
                UnidadMedida = "unidad",
                Cantidad = 100,
                MinimoInventario = 20,
                MaximoInventario = 500,
                Precio = 2.20m,
                Activo = true
            };

            var bateria = new Componente
            {
                NumeroPieza = "BAT-001",
                Nombre = "Batería Li-ion 18650",
                Descripcion = "Batería recargable Li-ion 18650 3.7V",
                Categoria = "Ventilador Portátil",
                UnidadMedida = "unidad",
                Cantidad = 100,
                MinimoInventario = 20,
                MaximoInventario = 500,
                Precio = 4.10m,
                Activo = true
            };

            context.Componentes.AddRange(motor, aspas, carcasa, bateria);
            context.SaveChanges(); // necesario para obtener los Id generados

            var ventilador = new Producto
            {
                Nombre = "Mini Ventilador Portátil V1",
                Descripcion = "Ventilador portátil recargable de 3 aspas",
                PrecioBase = 25.90m,
                Cantidad = 0,
                Activo = true
            };
            context.Productos.Add(ventilador);
            context.SaveChanges(); // necesario para obtener el Id generado

            context.ProductoComponentes.AddRange(
                new ProductoComponente { ProductoId = ventilador.Id, ComponenteId = motor.Id, CantidadRequerida = 1 },
                new ProductoComponente { ProductoId = ventilador.Id, ComponenteId = aspas.Id, CantidadRequerida = 3 },
                new ProductoComponente { ProductoId = ventilador.Id, ComponenteId = carcasa.Id, CantidadRequerida = 1 },
                new ProductoComponente { ProductoId = ventilador.Id, ComponenteId = bateria.Id, CantidadRequerida = 1 }
            );
            context.SaveChanges();
        }
    }
}
