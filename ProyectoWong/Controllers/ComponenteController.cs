using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models;

namespace ProyectoWong.Controllers
{
    [Route("Componente")]
    public class ComponenteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ComponenteController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Componentes";
            return View();
        }

        [HttpGet("consultar-componentes")]
        public async Task<IActionResult> Consultar()
        {
            try
            {
                var componentes = await _context.Componentes
                    .Where(c => c.Activo)
                    .Select(c => new
                    {
                        id = c.Id,
                        numeroPieza = c.NumeroPieza,
                        nombre = c.Nombre,
                        cantidad = c.Cantidad,
                        minimoInventario = c.MinimoInventario,
                        maximoInventario = c.MaximoInventario,
                        descripcion = c.Descripcion,
                        categoria = c.Categoria,
                        proveedor = c.Proveedor,
                        precio = c.Precio,
                        unidadMedida = c.UnidadMedida,
                        numeroLote = c.NumeroLote,
                        fechaCaducidad = c.FechaCaducidad,
                        numeroSerie = c.NumeroSerie,
                        ubicacion = c.Ubicacion,
                        tieneImagen = c.Imagen != null,
                        activo = c.Activo
                    })
                    .ToListAsync();

                return Json(Respuesta.OK("Consulta exitosa", componentes));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpGet("obtener-componente/{id}")]
        public async Task<IActionResult> ObtenerComponente(int id)
        {
            try
            {
                var componente = await _context.Componentes.FindAsync(id);

                if (componente == null)
                    return Json(Respuesta.Error("Componente no encontrado"));

                var componenteDto = new
                {
                    id = componente.Id,
                    numeroPieza = componente.NumeroPieza,
                    nombre = componente.Nombre,
                    cantidad = componente.Cantidad,
                    minimoInventario = componente.MinimoInventario,
                    maximoInventario = componente.MaximoInventario,
                    descripcion = componente.Descripcion,
                    categoria = componente.Categoria,
                    proveedor = componente.Proveedor,
                    precio = componente.Precio,
                    unidadMedida = componente.UnidadMedida,
                    numeroLote = componente.NumeroLote,
                    fechaCaducidad = componente.FechaCaducidad?.ToString("yyyy-MM-dd"),
                    numeroSerie = componente.NumeroSerie,
                    ubicacion = componente.Ubicacion,
                    tieneImagen = componente.Imagen != null,
                    activo = componente.Activo
                };

                return Json(Respuesta.OK("Componente encontrado", componenteDto));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpPost("guardar-componente")]
        [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)] // 50 MB
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> GuardarComponente([FromForm] ComponenteViewModel model, IFormFile? imagenFile)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));

            try
            {
                byte[]? imagenBytes = null;

                // Procesar imagen si se subió una
                if (imagenFile != null && imagenFile.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await imagenFile.CopyToAsync(memoryStream);
                        imagenBytes = memoryStream.ToArray();
                    }
                }

                if (model.Id > 0)
                {
                    // EDICIÓN
                    var existente = await _context.Componentes.FindAsync(model.Id);
                    if (existente == null)
                        return Json(Respuesta.Error("No se encontró el componente"));

                    existente.NumeroPieza = model.NumeroPieza;
                    existente.Nombre = model.Nombre;
                    existente.Cantidad = model.Cantidad;
                    existente.MinimoInventario = model.MinimoInventario;
                    existente.MaximoInventario = model.MaximoInventario;
                    existente.Descripcion = model.Descripcion;
                    existente.Categoria = model.Categoria;
                    existente.Proveedor = model.Proveedor;
                    existente.Precio = model.Precio;
                    existente.UnidadMedida = model.UnidadMedida;
                    existente.NumeroLote = model.NumeroLote;
                    existente.FechaCaducidad = model.FechaCaducidad;
                    existente.NumeroSerie = model.NumeroSerie;
                    existente.Ubicacion = model.Ubicacion;
                    existente.Activo = model.Activo;

                    if (imagenBytes != null)
                    {
                        existente.Imagen = imagenBytes;
                    }
                }
                else
                {
                    // CREACIÓN
                    var nuevo = new Componente
                    {
                        NumeroPieza = model.NumeroPieza,
                        Nombre = model.Nombre,
                        Cantidad = model.Cantidad,
                        MinimoInventario = model.MinimoInventario,
                        MaximoInventario = model.MaximoInventario,
                        Descripcion = model.Descripcion,
                        Categoria = model.Categoria,
                        Proveedor = model.Proveedor,
                        Precio = model.Precio,
                        UnidadMedida = model.UnidadMedida,
                        NumeroLote = model.NumeroLote,
                        FechaCaducidad = model.FechaCaducidad,
                        NumeroSerie = model.NumeroSerie,
                        Ubicacion = model.Ubicacion,
                        Imagen = imagenBytes,
                        Activo = true,
                        FechaAlta = DateTime.Now
                    };
                    _context.Componentes.Add(nuevo);
                }

                await _context.SaveChangesAsync();
                return Json(Respuesta.OK(model.Id > 0 ? "Componente actualizado" : "Componente registrado"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        [HttpDelete("eliminar-componente/{id}")]
        public async Task<IActionResult> EliminarComponente(int id)
        {
            try
            {
                var componente = await _context.Componentes.FindAsync(id);
                if (componente == null)
                    return Json(Respuesta.Error("No se encontró el componente"));

                componente.Activo = false;
                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Componente eliminado"));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
        [HttpGet("obtener-imagen/{id}")]
        public async Task<IActionResult> ObtenerImagen(int id)
        {
            var c = await _context.Componentes.FindAsync(id);
            if (c?.Imagen == null) return NotFound();
            return File(c.Imagen, "image/jpeg"); // or detect the real content type
        }
    }
}
