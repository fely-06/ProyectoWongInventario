using ProyectoWong.Data;
using ProyectoWong.Models.Planta;

namespace ProyectoWong.Helpers
{
    public static class PlantaSeeder
    {
        // Si no hay máquinas, crea las estaciones de la línea del mini ventilador.
        public static void SeedMaquinas(ApplicationDbContext context)
        {
            if (context.Maquinas.Any()) return;

            context.Maquinas.AddRange(
                new Maquina { Codigo = "MAQ-01", Nombre = "Ensambladora de motor" },
                new Maquina { Codigo = "MAQ-02", Nombre = "Estación de aspas" },
                new Maquina { Codigo = "MAQ-03", Nombre = "Estación de carcasa" },
                new Maquina { Codigo = "MAQ-04", Nombre = "Cableado y batería" },
                new Maquina { Codigo = "MAQ-05", Nombre = "Banco de pruebas" },
                new Maquina { Codigo = "MAQ-06", Nombre = "Empaque" }
            );
            context.SaveChanges();
        }
    }
}
