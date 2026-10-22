namespace ProyectoWong.Models.Planta
{
    // Listas fijas para los selects de Scrap y Downtime.
    // Si necesitan agregar más opciones, basta con editar estos arreglos.
    public static class CatalogosPlanta
    {
        public static readonly string[] MotivosRechazo =
        {
            "Falla de temperatura",
            "Velocidad baja (RPM)",
            "Defecto visual",
            "Aspas dañadas",
            "Motor defectuoso",
            "Ensamble incorrecto",
            "Falla de batería o cableado",
            "Otro"
        };

        // Máquina donde se hacen las pruebas físicas (sensores); ahí se registra el scrap automático
        public const string CodigoBancoPruebas = "MAQ-05";

        // Prueba física fallida (id del paso en Pruebas de Sensores) -> motivo de rechazo
        public static readonly Dictionary<string, string> MotivoPorPrueba = new()
        {
            ["rpm"] = "Velocidad baja (RPM)",
            ["temp"] = "Falla de temperatura"
        };

        public static readonly string[] CausasParo =
        {
            "Falla mecánica",
            "Falla eléctrica",
            "Falta de material",
            "Cambio de modelo (setup)",
            "Mantenimiento programado",
            "Ajuste de calidad",
            "Falta de operador",
            "Otro"
        };
    }
}
