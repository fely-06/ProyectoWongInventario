namespace ProyectoWong.Models.Indicadores
{
    
    public class IndicadoresFiltro
    {
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public int? ProductoId { get; set; }   // aplica a Producción, Pruebas, Scrap, Downtime y Producto Terminado
        public int? MaquinaId { get; set; }    // aplica a Scrap y Downtime
    }

  
    public class IndicadoresResultado
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public DateTime Generado { get; set; } = DateTime.Now;
        public string Agrupacion { get; set; } = "día"; 
        public List<ModuloIndicadores> Modulos { get; set; } = new();
    }

    public class ModuloIndicadores
    {
        public string Clave { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Icono { get; set; } = "bi-bar-chart";
        public string Descripcion { get; set; } = string.Empty;
        public List<IndicadorKpi> Kpis { get; set; } = new();
        public List<GraficaIndicador> Graficas { get; set; } = new();
        public TablaReporte? Tabla { get; set; }
    }

    public class IndicadorKpi
    {
        public string Nombre { get; set; } = string.Empty;
        public string Valor { get; set; } = "—";      // ya formateado (ej. "92.5 %", "$1,250.00")
        public double? Numero { get; set; }            // valor crudo, por si se quiere usar en JS
        public string Estado { get; set; } = "neutral"; // ok | warn | danger | neutral  (color del semáforo)
        public string? Meta { get; set; }              // texto de la meta, ej. "Meta ≥ 95 %"
        public string Descripcion { get; set; } = string.Empty;
    }

    public class GraficaIndicador
    {
        public string Titulo { get; set; } = string.Empty;
        public string Tipo { get; set; } = "bar";     
        public bool Horizontal { get; set; }
        public bool Apilada { get; set; }
        public bool Ancha { get; set; }               
        public string? Unidad { get; set; }         
        public List<string> Etiquetas { get; set; } = new();
        public List<SerieGrafica> Series { get; set; } = new();
    }

    public class SerieGrafica
    {
        public string Nombre { get; set; } = string.Empty;
        public List<double> Valores { get; set; } = new();
        public string? Color { get; set; }            
    }

  
    public class TablaReporte
    {
        public string Titulo { get; set; } = string.Empty;
        public List<string> Columnas { get; set; } = new();
        public List<List<string>> Filas { get; set; } = new();
    }
}
