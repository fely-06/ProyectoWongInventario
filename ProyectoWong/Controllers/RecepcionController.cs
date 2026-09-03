using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;
using ProyectoWong.Models;
using ProyectoWong.Models.Recepcion;

namespace ProyectoWong.Controllers
{
    [Route("Recepcion")]
    public class RecepcionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecepcionController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.ActiveMenu = "Recepcion";
            return View();
        }


        // ── PASO 1-2: RECEIVING + SCAN PO ──────────────────────────────
        // Crea la cabecera de recepción ligada a una orden de compra
        [HttpPost("iniciar")]
        public async Task<IActionResult> Iniciar([FromBody] RecepcionViewModel model)
        {
            if (!ModelState.IsValid) return Json(Respuesta.FromModelState(ModelState));

            try
            {
                var orden = await _context.OrdenesCompra.FindAsync(model.OrdenCompraId);
                if (orden == null)
                    return Json(Respuesta.Error("Orden de compra no encontrada"));

                // VALIDACIÓN: Solo permitir recepción si está Pendiente o En Proceso
                if (orden.Estado == "Recibida" || orden.Estado == "Completada")
                    return Json(Respuesta.Error("Esta Orden de Compra ya fue recibida anteriormente. No se puede iniciar una nueva recepción."));

                var usuarioId = model.UsuarioId;

                var recepcion = new Recepcion
                {
                    OrdenCompraId = model.OrdenCompraId,
                    UsuarioId = usuarioId,
                    Estado = "EnProceso",
                    FechaRecepcion = DateTime.Now
                };

                _context.Recepciones.Add(recepcion);
                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Recepción iniciada", new { recepcionId = recepcion.Id }));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
        // ── PASO 3-4: SCAN MATERIAL + BATCH NUMBER + QUANTITY ──────────
        // Captura un lote recibido dentro de la recepción
        //[HttpPost("agregar-lote")]
        //public async Task<IActionResult> AgregarLote([FromBody] RecepcionDetalleInput model)
        //{
        //    try
        //    {
        //        var recepcion = await _context.Recepciones.FindAsync(model.RecepcionId);
        //        if (recepcion == null)
        //            return Json(Respuesta.Error("Recepción no encontrada"));

        //        if (string.IsNullOrWhiteSpace(model.NumeroLote))
        //            return Json(Respuesta.Error("El número de lote es obligatorio"));

        //        if (model.CantidadRecibida <= 0)
        //            return Json(Respuesta.Error("La cantidad recibida debe ser mayor a 0"));

        //        var detalle = new RecepcionDetalle
        //        {
        //            RecepcionId = model.RecepcionId,
        //            ComponenteId = model.ComponenteId,
        //            NumeroLote = model.NumeroLote,
        //            FechaCaducidad = model.FechaCaducidad,
        //            CantidadRecibida = model.CantidadRecibida,
        //            Estado = "Pendiente"
        //        };

        //        _context.RecepcionDetalles.Add(detalle);
        //        await _context.SaveChangesAsync();

        //        return Json(Respuesta.OK("Lote registrado", new { recepcionDetalleId = detalle.Id }));
        //    }
        //    catch (Exception e)
        //    {
        //        return Json(Respuesta.Error(e.Message));
        //    }
        //}
        [HttpPost("agregar-lote")]
        public async Task<IActionResult> AgregarLote([FromBody] RecepcionDetalleInput model)
        {
            try
            {
                var recepcion = await _context.Recepciones.FindAsync(model.RecepcionId);
                if (recepcion == null) return Json(Respuesta.Error("Recepción no encontrada"));

                var componente = await _context.Componentes.FindAsync(model.ComponenteId);
                if (componente == null) return Json(Respuesta.Error("Componente no encontrado"));

                // 1. VALIDACIÓN: No recibir más de lo esperado en la OC
                var detalleOC = await _context.OrdenCompraDetalle
                    .FirstOrDefaultAsync(d => d.OrdenCmpraId == recepcion.OrdenCompraId && d.ComponenteId == model.ComponenteId);

                if (detalleOC != null)
                {
                    var yaRecibidoEnEstaRecepcion = await _context.RecepcionDetalles
                        .Where(rd => rd.RecepcionId == model.RecepcionId && rd.ComponenteId == model.ComponenteId)
                        .SumAsync(rd => rd.CantidadRecibida);

                    if (yaRecibidoEnEstaRecepcion + model.CantidadRecibida > detalleOC.CantidadEsperada)
                    {
                        return Json(Respuesta.Error($"Excede la cantidad esperada. Pedido: {detalleOC.CantidadEsperada}, Ya recibido: {yaRecibidoEnEstaRecepcion}, Intentas recibir: {model.CantidadRecibida}"));
                    }
                }

                // 2. GENERACIÓN AUTOMÁTICA DE LOTE (Si viene vacío)
                if (string.IsNullOrWhiteSpace(model.NumeroLote))
                {
                    string fecha = DateTime.Now.ToString("yyyyMMdd");
                    string random = new Random().Next(1000, 9999).ToString();
                    // Usa el Número de Pieza para hacerlo legible, o el Id si prefieres
                    string piezaLimpia = new string(componente.NumeroPieza.Where(char.IsLetterOrDigit).ToArray()).Substring(0, Math.Min(6, componente.NumeroPieza.Length));
                    model.NumeroLote = $"LOT-{piezaLimpia}-{fecha}-{random}";
                }

                if (model.CantidadRecibida <= 0)
                    return Json(Respuesta.Error("La cantidad recibida debe ser mayor a 0"));

                var detalle = new RecepcionDetalle
                {
                    RecepcionId = model.RecepcionId,
                    ComponenteId = model.ComponenteId,
                    NumeroLote = model.NumeroLote, // Aquí va el generado o el escaneado
                    FechaCaducidad = model.FechaCaducidad,
                    CantidadRecibida = model.CantidadRecibida,
                    Estado = "Pendiente"
                };

                _context.RecepcionDetalles.Add(detalle);
                await _context.SaveChangesAsync();

                // Devolvemos el número de lote generado por si el frontend necesita mostrarlo
                return Json(Respuesta.OK("Lote registrado", new
                {
                    recepcionDetalleId = detalle.Id,
                    numeroLoteGenerado = detalle.NumeroLote
                }));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ── PASO 5: QA INSPECTION ───────────────────────────────────────
        [HttpPost("inspeccionar")]
        public async Task<IActionResult> Inspeccionar([FromBody] InspeccionQAInput model)
        {
            try
            {
                var detalle = await _context.RecepcionDetalles.FindAsync(model.RecepcionDetalleId);
                if (detalle == null)
                    return Json(Respuesta.Error("No se encontró el lote a inspeccionar"));

                var resultado = (model.EmpaqueOk && model.EtiquetaOk && model.MaterialOk && model.CertificadoDisponible)
                    ? "Aprobado"
                    : "Rechazado";

                var inspeccion = new InspeccionQA
                {
                    RecepcionDetalleId = model.RecepcionDetalleId,
                    EmpaqueOk = model.EmpaqueOk,
                    EtiquetaOk = model.EtiquetaOk,
                    MaterialOk = model.MaterialOk,
                    CertificadoDisponible = model.CertificadoDisponible,
                    InspeccionadoPor = model.InspeccionadoPor,
                    Resultado = resultado,
                    Comentarios = model.Comentarios,
                    FechaInspeccion = DateTime.Now
                };

                _context.InspeccionesQA.Add(inspeccion);

                // Actualiza el estado del lote según el resultado de QA
                detalle.Estado = resultado;

                await _context.SaveChangesAsync();

                return Json(Respuesta.OK($"Inspección registrada: {resultado}", new { resultado }));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ── PASO 6-7: PRINT LABEL + SCAN PALLET ────────────────────────
        // Genera un pallet para un lote ya aprobado por QA
        [HttpPost("generar-pallet")]
        public async Task<IActionResult> GenerarPallet([FromBody] PalletInput model)
        {
            try
            {
                var detalle = await _context.RecepcionDetalles
                    .Include(d => d.Inspeccion)
                    .FirstOrDefaultAsync(d => d.Id == model.RecepcionDetalleId);

                if (detalle == null)
                    return Json(Respuesta.Error("No se encontró el lote"));

                if (detalle.Inspeccion == null || detalle.Inspeccion.Resultado != "Aprobado")
                    return Json(Respuesta.Error("El lote no ha sido aprobado por QA, no se puede generar etiqueta"));

                if (string.IsNullOrWhiteSpace(model.CodigoPallet))
                    return Json(Respuesta.Error("El código de pallet es obligatorio"));

                var pallet = new Pallet
                {
                    RecepcionDetalleId = model.RecepcionDetalleId,
                    CodigoPallet = model.CodigoPallet,
                    FechaImpresionEtiqueta = DateTime.Now
                };

                _context.Pallets.Add(pallet);
                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Etiqueta generada", new { palletId = pallet.Id, codigoPallet = pallet.CodigoPallet }));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ── PASO 8: SCAN LOCATION ───────────────────────────────────────
        // Al asignar la primera ubicación de un pallet, el material entra
        // formalmente al inventario disponible y se debe sumar su cantidad
        // al stock del componente. Si el pallet se reubica más adelante
        // (segundo, tercer movimiento, etc.) NO se debe volver a sumar,
        // solo se registra el nuevo movimiento.
        [HttpPost("asignar-ubicacion")]
        public async Task<IActionResult> AsignarUbicacion([FromBody] MovimientoInput model)
        {
            try
            {
                var pallet = await _context.Pallets
                    .Include(p => p.Movimientos)
                    .Include(p => p.RecepcionDetalle!)
                        .ThenInclude(d => d.Componente)
                    .FirstOrDefaultAsync(p => p.Id == model.PalletId);

                if (pallet == null)
                    return Json(Respuesta.Error("Pallet no encontrado"));

                if (pallet.RecepcionDetalle == null)
                    return Json(Respuesta.Error("El pallet no tiene un lote de recepción asociado"));

                if (pallet.RecepcionDetalle.Estado != "Aprobado")
                    return Json(Respuesta.Error("El lote no ha sido aprobado por QA, no se puede almacenar"));

                var ubicacion = await _context.Ubicaciones.FindAsync(model.UbicacionId);
                if (ubicacion == null)
                    return Json(Respuesta.Error("Ubicación no encontrada"));

                bool esPrimeraUbicacion = pallet.Movimientos == null || !pallet.Movimientos.Any();

                var movimiento = new MovimientoInventario
                {
                    PalletId = model.PalletId,
                    UbicacionId = model.UbicacionId,
                    TipoMovimiento = "Recepcion",
                    FechaMovimiento = DateTime.Now,
                    RealizadoPor = model.RealizadoPor
                };
                _context.MovimientosInventario.Add(movimiento);

                // ── Incremento de stock ──────────────────────────────────
                // Solo se suma la cantidad recibida la primera vez que el
                // pallet queda ubicado; movimientos posteriores del mismo
                // pallet son solo cambios de ubicación (traspasos), no
                // nuevas entradas de material.
                if (esPrimeraUbicacion && pallet.RecepcionDetalle.Componente != null)
                {
                    pallet.RecepcionDetalle.Componente.Cantidad += pallet.RecepcionDetalle.CantidadRecibida;

                    // Mantiene sincronizado el campo de texto libre Ubicacion
                    // del componente (usado en la pantalla de Componentes)
                    // con la ubicación real asignada en la recepción.
                    pallet.RecepcionDetalle.Componente.Ubicacion = ubicacion.Codigo;
                }

                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Ubicación asignada", new { stockActualizado = esPrimeraUbicacion }));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ── PASO 9: CONFIRM RECEIPT ─────────────────────────────────────
        // Cierra la recepción una vez que todos los lotes tienen ubicación
        [HttpPost("confirmar/{recepcionId}")]
        public async Task<IActionResult> Confirmar(int recepcionId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var recepcion = await _context.Recepciones
                    .Include(r => r.OrdenCompra) // <-- Importante incluir la OC
                    .Include(r => r.Detalles)
                        .ThenInclude(d => d.Pallets)
                            .ThenInclude(p => p.Movimientos)
                    .FirstOrDefaultAsync(r => r.Id == recepcionId);

                if (recepcion == null)
                    return Json(Respuesta.Error("Recepción no encontrada"));

                if (recepcion.Detalles == null || !recepcion.Detalles.Any())
                    return Json(Respuesta.Error("La recepción no tiene lotes capturados"));

                // Validar que los lotes APROBADOS tengan ubicación
                var lotesAprobadosSinUbicacion = recepcion.Detalles
                    .Where(d => d.Estado == "Aprobado")
                    .Any(d => d.Pallets == null || !d.Pallets.Any(p => p.Movimientos != null && p.Movimientos.Any()));

                if (lotesAprobadosSinUbicacion)
                    return Json(Respuesta.Error("Hay lotes APROBADOS sin ubicación asignada. No se puede completar."));

                // 1. Cerrar la recepción
                recepcion.Estado = "Completada";

                // 2. Actualizar el estado de la Orden de Compra
                if (recepcion.OrdenCompra != null)
                {
                    bool hayLotesRechazados = recepcion.Detalles.Any(d => d.Estado == "Rechazado");

                    if (hayLotesRechazados)
                    {
                        // Si hubo rechazos, la OC no se recibió al 100%. 
                        // Queda disponible para una futura recepción de reemplazo.
                        recepcion.OrdenCompra.Estado = "Parcialmente Recibida";
                    }
                    else
                    {
                        // Todo lo recibido fue aprobado
                        recepcion.OrdenCompra.Estado = "Recibida";
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                string mensaje = recepcion.OrdenCompra?.Estado == "Parcialmente Recibida"
                    ? "Recepción completada. La OC queda como 'Parcialmente Recibida' debido a lotes rechazados."
                    : "Recepción confirmada. La Orden de Compra ha sido marcada como 'Recibida'.";

                return Json(Respuesta.OK(mensaje));
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ── Consulta general de una recepción con todo su detalle ──────
        [HttpGet("obtener/{id}")]
        public async Task<IActionResult> Obtener(int id)
        {
            try
            {
                var recepcion = await _context.Recepciones
                    .Include(r => r.OrdenCompra)
                    .Include(r => r.Detalles!)
                        .ThenInclude(d => d.Componente)
                    .Include(r => r.Detalles!)
                        .ThenInclude(d => d.Inspeccion)
                    .Include(r => r.Detalles!)
                        .ThenInclude(d => d.Pallets!)
                            .ThenInclude(p => p.Movimientos!)
                                .ThenInclude(m => m.Ubicacion)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (recepcion == null)
                    return Json(Respuesta.Error("Recepción no encontrada"));

                var dto = new
                {
                    id = recepcion.Id,
                    numeroOC = recepcion.OrdenCompra?.NumeroOC,
                    estado = recepcion.Estado,
                    fechaRecepcion = recepcion.FechaRecepcion,
                    lotes = recepcion.Detalles?.Select(d => new
                    {
                        id = d.Id,
                        componente = d.Componente?.Nombre,
                        numeroLote = d.NumeroLote,
                        fechaCaducidad = d.FechaCaducidad,
                        cantidadRecibida = d.CantidadRecibida,
                        estado = d.Estado,
                        inspeccion = d.Inspeccion == null ? null : new
                        {
                            resultado = d.Inspeccion.Resultado,
                            comentarios = d.Inspeccion.Comentarios
                        },
                        pallets = d.Pallets?.Select(p => new
                        {
                            id = p.Id,
                            codigoPallet = p.CodigoPallet,
                            ubicacion = p.Movimientos != null && p.Movimientos.Any()
                                ? p.Movimientos.OrderByDescending(m => m.FechaMovimiento).First().Ubicacion?.Codigo
                                : null
                        })
                    })
                };

                return Json(Respuesta.OK("Recepción encontrada", dto));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }
        // Agrega esto a tu RecepcionController

        // ── AUXILIAR: Listar ubicaciones para el modal ───────────────────────
        [HttpGet("listar-ubicaciones")]
        public async Task<IActionResult> ListarUbicaciones()
        {
            try
            {
                var ubicaciones = await _context.Ubicaciones
                    .Where(u => u.Activo) // Asumiendo que tienes un campo Activo, si no, quita el Where
                    .Select(u => new
                    {
                        id = u.Id,
                        codigo = u.Codigo,
                        zona = u.Zona
                    })
                    .ToListAsync();

                return Json(Respuesta.OK("Ubicaciones cargadas", ubicaciones));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // ── AUXILIAR: Crear nueva ubicación desde el modal ───────────────────
        [HttpPost("crear-ubicacion")]
        public async Task<IActionResult> CrearUbicacion([FromBody] CrearUbicacionInput model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Codigo))
                    return Json(Respuesta.Error("El código de ubicación es obligatorio"));

                var nuevaUbicacion = new Ubicacion
                {
                    Codigo = model.Codigo,
                    Zona = model.Zona,
                    CapacidadMaxima = model.CapacidadMaxima,
                    Activo = true
                };

                _context.Ubicaciones.Add(nuevaUbicacion);
                await _context.SaveChangesAsync();

                return Json(Respuesta.OK("Ubicación creada", new { id = nuevaUbicacion.Id, codigo = nuevaUbicacion.Codigo, zona = nuevaUbicacion.Zona }));
            }
            catch (Exception e)
            {
                return Json(Respuesta.Error(e.Message));
            }
        }

        // Agrega esta clase DTO al final de tu archivo RecepcionController
        public class CrearUbicacionInput
        {
            public string Codigo { get; set; } = string.Empty;
            public string? Zona { get; set; }
            public int? CapacidadMaxima { get; set; }
        }
    }

    // ── Inputs auxiliares para cada paso del wizard ─────────────────────
    public class RecepcionDetalleInput
    {
        public int RecepcionId { get; set; }
        public int ComponenteId { get; set; }
        public string NumeroLote { get; set; } = string.Empty;
        public DateTime? FechaCaducidad { get; set; }
        public int CantidadRecibida { get; set; }
    }

    public class InspeccionQAInput
    {
        public int RecepcionDetalleId { get; set; }
        public bool EmpaqueOk { get; set; }
        public bool EtiquetaOk { get; set; }
        public bool MaterialOk { get; set; }
        public bool CertificadoDisponible { get; set; }
        public int InspeccionadoPor { get; set; }
        public string? Comentarios { get; set; }
    }

    public class PalletInput
    {
        public int RecepcionDetalleId { get; set; }
        public string CodigoPallet { get; set; } = string.Empty;
    }

    public class MovimientoInput
    {
        public int PalletId { get; set; }
        public int UbicacionId { get; set; }
        public int RealizadoPor { get; set; }
    }
}