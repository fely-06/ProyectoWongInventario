using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ProyectoWong.Helpers
{
    public class Respuesta
    {
        public bool Success { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public object? Result { get; set; }

        public static Respuesta OK(string mensaje, object? result = null) =>
            new Respuesta { Success = true, Mensaje = mensaje, Result = result };

        public static Respuesta Error(string mensaje) =>
            new Respuesta { Success = false, Mensaje = mensaje };

        public static Respuesta FromModelState(ModelStateDictionary modelState)
        {
            var errors = modelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return new Respuesta { Success = false, Mensaje = string.Join(" | ", errors) };
        }
    }
}
