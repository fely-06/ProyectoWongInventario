/* =====================================================================
   DATOS DE PRUEBA - SIV (ProyectoWong)
   ---------------------------------------------------------------------
   Llena la base con datos para revisar cada módulo de la app.
   Requisitos: haber corrido "dotnet ef database update" sobre la base.
   Uso: abrir en SSMS sobre la base del proyecto (p. ej. monitoreo) y
   ejecutar (F5). Todo va en una transacción: si algo falla, no se
   guarda nada.

   - Solo AGREGA registros; no borra ni cambia lo que ya existe, salvo
     sumar al stock lo que "entró" por las recepciones de prueba.
   - Si ya se corrió antes (existe el proveedor 'Motores del Norte'),
     no hace nada.
   - Contraseña de todos los usuarios de prueba: 12345
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Proveedores', N'U') IS NULL OR OBJECT_ID(N'dbo.RegistrosScrap', N'U') IS NULL OR OBJECT_ID(N'dbo.LotesProductoTerminado', N'U') IS NULL
BEGIN
    RAISERROR(N'Esta base no tiene las tablas de SIV. Selecciona la base del proyecto (p. ej. monitoreo) en la lista de bases de SSMS, o corre "dotnet ef database update" si faltan migraciones.', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM dbo.Proveedores WHERE Nombre = N'Motores del Norte')
BEGIN
    PRINT 'Los datos de prueba ya estaban cargados. No se hizo nada.';
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @ahora  DATETIME2 = SYSDATETIME();
DECLARE @hash   NVARCHAR(100) = N'$2a$11$3SePU8lYhefjbJpyoQ9c6eJClY1bqccdsAe/7zhteHbNghTpzNyaa'; -- BCrypt de "12345"

/* ---------------------------------------------------------------------
   0. CATÁLOGO BASE
      Usa lo que ya tenga la base: el producto "Mini Ventilador Portátil V1"
      (si hay varios, el que tiene receta) y, de su receta, el motor, las
      aspas, la carcasa y la batería. Solo crea lo que falte.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios)
    INSERT dbo.Usuarios (NombreCompleto, Email, Telefono, Activo, FechaAlta, PasswordHash)
    VALUES (N'Administrador', N'admin@wong.com', NULL, 1, @ahora, @hash);

IF NOT EXISTS (SELECT 1 FROM dbo.Productos WHERE Nombre = N'Mini Ventilador Portátil V1')
    INSERT dbo.Productos (Nombre, Descripcion, PrecioBase, Activo, FechaAlta, Cantidad)
    VALUES (N'Mini Ventilador Portátil V1', N'Ventilador portátil recargable de 3 aspas', 25.90, 1, @ahora, 0);

DECLARE @v1 INT = (
    SELECT TOP 1 p.Id FROM dbo.Productos p
    WHERE p.Nombre = N'Mini Ventilador Portátil V1'
    ORDER BY CASE WHEN EXISTS (SELECT 1 FROM dbo.ProductoComponentes pc WHERE pc.ProductoId = p.Id) THEN 0 ELSE 1 END, p.Id);

-- Componente de la receta del V1 cuyo número de pieza o nombre coincide con el patrón
DECLARE @mot INT = (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId
                    WHERE pc.ProductoId = @v1 AND (c.NumeroPieza LIKE N'%MOT%' OR c.Nombre LIKE N'%motor%') ORDER BY c.Id);
DECLARE @asp INT = (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId
                    WHERE pc.ProductoId = @v1 AND (c.NumeroPieza LIKE N'%ASP%' OR c.Nombre LIKE N'%aspa%') ORDER BY c.Id);
DECLARE @case INT = (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId
                     WHERE pc.ProductoId = @v1 AND (c.NumeroPieza LIKE N'%CASE%' OR c.NumeroPieza LIKE N'%CARC%' OR c.Nombre LIKE N'%carcasa%') ORDER BY c.Id);
DECLARE @bat INT = (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId
                    WHERE pc.ProductoId = @v1 AND (c.NumeroPieza LIKE N'%BAT%' OR c.Nombre LIKE N'%bater%') ORDER BY c.Id);

-- Si alguno no está en la receta, se busca por número de pieza estándar o se crea
IF @mot IS NULL SET @mot = (SELECT TOP 1 Id FROM dbo.Componentes WHERE NumeroPieza = N'MOT-001' ORDER BY Id);
IF @mot IS NULL BEGIN
    INSERT dbo.Componentes (NumeroPieza, Nombre, Cantidad, MinimoInventario, MaximoInventario, Descripcion, Categoria, Precio, UnidadMedida, Activo, FechaAlta)
    VALUES (N'MOT-001', N'Motor DC 3V', 100, 20, 500, N'Micromotor DC de 3V para ventilador portátil', N'Ventilador Portátil', 3.50, N'unidad', 1, @ahora);
    SET @mot = SCOPE_IDENTITY();
END
IF @asp IS NULL SET @asp = (SELECT TOP 1 Id FROM dbo.Componentes WHERE NumeroPieza = N'ASP-001' ORDER BY Id);
IF @asp IS NULL BEGIN
    INSERT dbo.Componentes (NumeroPieza, Nombre, Cantidad, MinimoInventario, MaximoInventario, Descripcion, Categoria, Precio, UnidadMedida, Activo, FechaAlta)
    VALUES (N'ASP-001', N'Juego de Aspas', 300, 60, 1500, N'Aspa plástica para ventilador portátil', N'Ventilador Portátil', 0.80, N'unidad', 1, @ahora);
    SET @asp = SCOPE_IDENTITY();
END
IF @case IS NULL SET @case = (SELECT TOP 1 Id FROM dbo.Componentes WHERE NumeroPieza = N'CASE-001' ORDER BY Id);
IF @case IS NULL BEGIN
    INSERT dbo.Componentes (NumeroPieza, Nombre, Cantidad, MinimoInventario, MaximoInventario, Descripcion, Categoria, Precio, UnidadMedida, Activo, FechaAlta)
    VALUES (N'CASE-001', N'Case / Carcasa', 100, 20, 500, N'Carcasa plástica exterior del ventilador portátil', N'Ventilador Portátil', 2.20, N'unidad', 1, @ahora);
    SET @case = SCOPE_IDENTITY();
END
IF @bat IS NULL SET @bat = (SELECT TOP 1 Id FROM dbo.Componentes WHERE NumeroPieza = N'BAT-001' ORDER BY Id);
IF @bat IS NULL BEGIN
    INSERT dbo.Componentes (NumeroPieza, Nombre, Cantidad, MinimoInventario, MaximoInventario, Descripcion, Categoria, Precio, UnidadMedida, Activo, FechaAlta)
    VALUES (N'BAT-001', N'Batería Li-ion 18650', 100, 20, 500, N'Batería recargable Li-ion 18650 3.7V', N'Ventilador Portátil', 4.10, N'unidad', 1, @ahora);
    SET @bat = SCOPE_IDENTITY();
END

-- Producto sin receta (base nueva): se le pone la receta estándar
IF NOT EXISTS (SELECT 1 FROM dbo.ProductoComponentes WHERE ProductoId = @v1)
    INSERT dbo.ProductoComponentes (ProductoId, ComponenteId, CantidadRequerida)
    VALUES (@v1, @mot, 1), (@v1, @asp, 3), (@v1, @case, 1), (@v1, @bat, 1);

DECLARE @admin INT = (SELECT TOP 1 ExpUsuarioId FROM dbo.Usuarios WHERE Activo = 1 ORDER BY ExpUsuarioId);

INSERT dbo.Maquinas (Codigo, Nombre, Activa)
SELECT v.Codigo, v.Nombre, 1
FROM (VALUES (N'MAQ-01', N'Ensambladora de motor'), (N'MAQ-02', N'Estación de aspas'), (N'MAQ-03', N'Estación de carcasa'),
             (N'MAQ-04', N'Cableado y batería'), (N'MAQ-05', N'Banco de pruebas'), (N'MAQ-06', N'Empaque')) v(Codigo, Nombre)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Maquinas m WHERE m.Codigo = v.Codigo);

DECLARE @maq1 INT = (SELECT TOP 1 Id FROM dbo.Maquinas WHERE Codigo = N'MAQ-01');
DECLARE @maq2 INT = (SELECT TOP 1 Id FROM dbo.Maquinas WHERE Codigo = N'MAQ-02');
DECLARE @maq3 INT = (SELECT TOP 1 Id FROM dbo.Maquinas WHERE Codigo = N'MAQ-03');
DECLARE @maq4 INT = (SELECT TOP 1 Id FROM dbo.Maquinas WHERE Codigo = N'MAQ-04');
DECLARE @maq5 INT = (SELECT TOP 1 Id FROM dbo.Maquinas WHERE Codigo = N'MAQ-05');
DECLARE @maq6 INT = (SELECT TOP 1 Id FROM dbo.Maquinas WHERE Codigo = N'MAQ-06');

/* ---------------------------------------------------------------------
   1. USUARIOS (pantalla Usuarios; el inactivo no puede iniciar sesión)
   --------------------------------------------------------------------- */
INSERT dbo.Usuarios (NombreCompleto, Email, Telefono, Activo, FechaAlta, PasswordHash) VALUES
    (N'Laura Méndez (Calidad)',   N'calidad@wong.com',  N'6621234567', 1, DATEADD(DAY, -20, @ahora), @hash),
    (N'Jorge Ruiz (Almacén)',     N'almacen@wong.com',  N'6627654321', 1, DATEADD(DAY, -15, @ahora), @hash),
    (N'Pedro Soto (dado de baja)', N'baja@wong.com',    NULL,          0, DATEADD(DAY, -60, @ahora), @hash);
DECLARE @qa INT = (SELECT TOP 1 ExpUsuarioId FROM dbo.Usuarios WHERE Email = N'calidad@wong.com');
DECLARE @alm INT = (SELECT TOP 1 ExpUsuarioId FROM dbo.Usuarios WHERE Email = N'almacen@wong.com');

/* ---------------------------------------------------------------------
   2. PROVEEDORES (uno inactivo para ver que no aparece en la lista)
   --------------------------------------------------------------------- */
INSERT dbo.Proveedores (Nombre, Telefono, Correo, Activo) VALUES
    (N'Motores del Norte',       N'8112345678', N'ventas@motoresnorte.mx', 1),
    (N'Plásticos Wong',          N'6629876543', N'pedidos@plasticoswong.mx', 1),
    (N'Energía Litio MX',        N'5551237890', N'contacto@energialitio.mx', 1),
    (N'Proveedor Antiguo (baja)', NULL,          NULL,                       0);
DECLARE @provMot INT = (SELECT TOP 1 Id FROM dbo.Proveedores WHERE Nombre = N'Motores del Norte');
DECLARE @provPla INT = (SELECT TOP 1 Id FROM dbo.Proveedores WHERE Nombre = N'Plásticos Wong');
DECLARE @provLit INT = (SELECT TOP 1 Id FROM dbo.Proveedores WHERE Nombre = N'Energía Litio MX');

/* ---------------------------------------------------------------------
   3. COMPONENTES Y PRODUCTOS EXTRA
      - CAB-001 queda bajo su mínimo -> "Stock crítico" en Inicio/Inventario
      - Escalas de descuento del V1 -> "Registrar Pedido" en Productos
   --------------------------------------------------------------------- */
INSERT dbo.Componentes (NumeroPieza, Nombre, Cantidad, MinimoInventario, MaximoInventario, Descripcion, Categoria, Proveedor, Precio, UnidadMedida, Activo, FechaAlta)
VALUES (N'CAB-001', N'Cable USB-C de carga', 8, 25, 300, N'Cable de carga USB-C 30 cm', N'Ventilador Portátil', N'Energía Litio MX', 1.30, N'unidad', 1, @ahora),
       (N'INT-001', N'Interruptor de 3 velocidades', 150, 30, 400, N'Interruptor deslizable', N'Ventilador Portátil', N'Plásticos Wong', 0.65, N'unidad', 1, @ahora);
DECLARE @cab INT = (SELECT TOP 1 Id FROM dbo.Componentes WHERE NumeroPieza = N'CAB-001');
DECLARE @int INT = (SELECT TOP 1 Id FROM dbo.Componentes WHERE NumeroPieza = N'INT-001');

INSERT dbo.Productos (Nombre, Descripcion, PrecioBase, Activo, FechaAlta, Cantidad)
VALUES (N'Mini Ventilador Portátil V2', N'Versión con interruptor de 3 velocidades y cable USB-C', 32.50, 1, @ahora, 0);
DECLARE @v2 INT = SCOPE_IDENTITY();
INSERT dbo.ProductoComponentes (ProductoId, ComponenteId, CantidadRequerida) VALUES
    (@v2, @mot, 1), (@v2, @asp, 3), (@v2, @case, 1), (@v2, @bat, 1), (@v2, @int, 1), (@v2, @cab, 1);

INSERT dbo.EscalasDescuento (ProductoId, CantidadMinima, PorcentajeDescuento)
SELECT @v1, v.Minimo, v.Pct FROM (VALUES (10, 5.00), (50, 10.00), (100, 15.00)) v(Minimo, Pct)
WHERE NOT EXISTS (SELECT 1 FROM dbo.EscalasDescuento e WHERE e.ProductoId = @v1 AND e.CantidadMinima = v.Minimo);

/* ---------------------------------------------------------------------
   4. UBICACIONES DEL ALMACÉN
   --------------------------------------------------------------------- */
INSERT dbo.Ubicaciones (Codigo, Zona, Tipo, CapacidadMaxima, Activo)
SELECT v.Codigo, v.Zona, N'Disponible', v.Cap, 1
FROM (VALUES (N'A-01', N'A', 500), (N'A-02', N'A', 500), (N'B-01', N'B', 300), (N'B-02', N'B', 300),
             (N'PT-01', N'PT', 200), (N'PT-02', N'PT', 200)) v(Codigo, Zona, Cap)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Ubicaciones u WHERE u.Codigo = v.Codigo);
DECLARE @ubA1 INT = (SELECT TOP 1 Id FROM dbo.Ubicaciones WHERE Codigo = N'A-01');
DECLARE @ubA2 INT = (SELECT TOP 1 Id FROM dbo.Ubicaciones WHERE Codigo = N'A-02');
DECLARE @ubB1 INT = (SELECT TOP 1 Id FROM dbo.Ubicaciones WHERE Codigo = N'B-01');

/* ---------------------------------------------------------------------
   5. ÓRDENES DE COMPRA + RECEPCIONES (una por cada estado del flujo)
   --------------------------------------------------------------------- */
DECLARE @oc1 INT, @oc2 INT, @oc3 INT, @oc4 INT, @rec INT, @det INT, @pal INT;

-- OC-PRUEBA-001: RECIBIDA completa (2 lotes aprobados, con pallet y ubicación)
INSERT dbo.OrdenesCompra (NumeroOC, ProveedorId, Estado, FechaEsperada, FechaCreacion)
VALUES (N'OC-PRUEBA-001', @provMot, N'Recibida', DATEADD(DAY, -10, @ahora), DATEADD(DAY, -14, @ahora));
SET @oc1 = SCOPE_IDENTITY();
INSERT dbo.OrdenCompraDetalle (OrdenCmpraId, ComponenteId, CantidadEsperada) VALUES (@oc1, @mot, 50), (@oc1, @asp, 150), (@oc1, @case, 40);
INSERT dbo.Recepciones (OrdenCompraId, UsuarioId, FechaRecepcion, Estado) VALUES (@oc1, @alm, DATEADD(DAY, -10, @ahora), N'Completada');
SET @rec = SCOPE_IDENTITY();

INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
VALUES (@rec, @mot, N'LOT-MOT-2410', NULL, 50, N'Aprobado');
SET @det = SCOPE_IDENTITY();
INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
VALUES (@det, 1, 1, 1, 1, @qa, N'Aprobado', N'Sin observaciones', DATEADD(DAY, -10, @ahora));
INSERT dbo.Pallets (RecepcionDetalleId, CodigoPallet, FechaImpresionEtiqueta) VALUES (@det, N'PAL-PRUEBA-001', DATEADD(DAY, -10, @ahora));
SET @pal = SCOPE_IDENTITY();
INSERT dbo.MovimientosInventario (PalletId, UbicacionId, TipoMovimiento, FechaMovimiento, RealizadoPor) VALUES (@pal, @ubA1, N'Recepcion', DATEADD(DAY, -10, @ahora), @alm);

INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
VALUES (@rec, @asp, N'LOT-ASP-2410', NULL, 150, N'Aprobado');
SET @det = SCOPE_IDENTITY();
INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
VALUES (@det, 1, 1, 1, 1, @qa, N'Aprobado', NULL, DATEADD(DAY, -10, @ahora));
INSERT dbo.Pallets (RecepcionDetalleId, CodigoPallet, FechaImpresionEtiqueta) VALUES (@det, N'PAL-PRUEBA-002', DATEADD(DAY, -10, @ahora));
SET @pal = SCOPE_IDENTITY();
INSERT dbo.MovimientosInventario (PalletId, UbicacionId, TipoMovimiento, FechaMovimiento, RealizadoPor) VALUES (@pal, @ubA2, N'Recepcion', DATEADD(DAY, -10, @ahora), @alm);

UPDATE dbo.Componentes SET Cantidad = Cantidad + 50,  Ubicacion = N'A-01' WHERE Id = @mot;
UPDATE dbo.Componentes SET Cantidad = Cantidad + 150, Ubicacion = N'A-02' WHERE Id = @asp;

INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
VALUES (@rec, @case, N'LOT-CASE-2410', NULL, 40, N'Aprobado');
SET @det = SCOPE_IDENTITY();
INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
VALUES (@det, 1, 1, 1, 1, @qa, N'Aprobado', NULL, DATEADD(DAY, -10, @ahora));
INSERT dbo.Pallets (RecepcionDetalleId, CodigoPallet, FechaImpresionEtiqueta) VALUES (@det, N'PAL-PRUEBA-004', DATEADD(DAY, -10, @ahora));
SET @pal = SCOPE_IDENTITY();
INSERT dbo.MovimientosInventario (PalletId, UbicacionId, TipoMovimiento, FechaMovimiento, RealizadoPor) VALUES (@pal, @ubA2, N'Recepcion', DATEADD(DAY, -10, @ahora), @alm);
UPDATE dbo.Componentes SET Cantidad = Cantidad + 40 WHERE Id = @case;

-- OC-PRUEBA-002: PARCIALMENTE RECIBIDA (un lote aprobado y uno rechazado por QA)
INSERT dbo.OrdenesCompra (NumeroOC, ProveedorId, Estado, FechaEsperada, FechaCreacion)
VALUES (N'OC-PRUEBA-002', @provLit, N'Parcialmente Recibida', DATEADD(DAY, -3, @ahora), DATEADD(DAY, -8, @ahora));
SET @oc2 = SCOPE_IDENTITY();
INSERT dbo.OrdenCompraDetalle (OrdenCmpraId, ComponenteId, CantidadEsperada) VALUES (@oc2, @bat, 60);
INSERT dbo.Recepciones (OrdenCompraId, UsuarioId, FechaRecepcion, Estado) VALUES (@oc2, @alm, DATEADD(DAY, -3, @ahora), N'Completada');
SET @rec = SCOPE_IDENTITY();

INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
VALUES (@rec, @bat, N'LOT-BAT-2411A', DATEADD(YEAR, 2, @ahora), 30, N'Aprobado');
SET @det = SCOPE_IDENTITY();
INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
VALUES (@det, 1, 1, 1, 1, @qa, N'Aprobado', NULL, DATEADD(DAY, -3, @ahora));
INSERT dbo.Pallets (RecepcionDetalleId, CodigoPallet, FechaImpresionEtiqueta) VALUES (@det, N'PAL-PRUEBA-003', DATEADD(DAY, -3, @ahora));
SET @pal = SCOPE_IDENTITY();
INSERT dbo.MovimientosInventario (PalletId, UbicacionId, TipoMovimiento, FechaMovimiento, RealizadoPor) VALUES (@pal, @ubB1, N'Recepcion', DATEADD(DAY, -3, @ahora), @alm);
UPDATE dbo.Componentes SET Cantidad = Cantidad + 30, Ubicacion = N'B-01' WHERE Id = @bat;

INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
VALUES (@rec, @bat, N'LOT-BAT-2411B', DATEADD(YEAR, 2, @ahora), 10, N'Rechazado');
SET @det = SCOPE_IDENTITY();
INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
VALUES (@det, 0, 1, 0, 1, @qa, N'Rechazado', N'Empaque abierto y 3 baterías infladas', DATEADD(DAY, -3, @ahora));

-- OC-PRUEBA-003: PENDIENTE sin recepción -> para probar Recepción desde el paso 1
INSERT dbo.OrdenesCompra (NumeroOC, ProveedorId, Estado, FechaEsperada, FechaCreacion)
VALUES (N'OC-PRUEBA-003', @provPla, N'Pendiente', DATEADD(DAY, 2, @ahora), DATEADD(DAY, -1, @ahora));
SET @oc3 = SCOPE_IDENTITY();
INSERT dbo.OrdenCompraDetalle (OrdenCmpraId, ComponenteId, CantidadEsperada) VALUES (@oc3, @case, 40), (@oc3, @asp, 120);

-- OC-PRUEBA-004: PENDIENTE con recepción EN PROCESO
--   lote de cables esperando QA + lote de interruptores aprobado sin pallet
INSERT dbo.OrdenesCompra (NumeroOC, ProveedorId, Estado, FechaEsperada, FechaCreacion)
VALUES (N'OC-PRUEBA-004', @provLit, N'Pendiente', @ahora, DATEADD(DAY, -2, @ahora));
SET @oc4 = SCOPE_IDENTITY();
INSERT dbo.OrdenCompraDetalle (OrdenCmpraId, ComponenteId, CantidadEsperada) VALUES (@oc4, @cab, 100), (@oc4, @int, 50);
INSERT dbo.Recepciones (OrdenCompraId, UsuarioId, FechaRecepcion, Estado) VALUES (@oc4, @alm, DATEADD(HOUR, -2, @ahora), N'EnProceso');
SET @rec = SCOPE_IDENTITY();
INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
VALUES (@rec, @cab, N'LOT-CAB-2412', NULL, 100, N'Pendiente');
INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
VALUES (@rec, @int, N'LOT-INT-2412', NULL, 50, N'Aprobado');
SET @det = SCOPE_IDENTITY();
INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
VALUES (@det, 1, 1, 1, 1, @qa, N'Aprobado', NULL, DATEADD(HOUR, -1, @ahora));

/* ---------------------------------------------------------------------
   6. ÓRDENES DE PRODUCCIÓN (una en cada estado)
      Duración simulada: 0.5 min por pieza.
   --------------------------------------------------------------------- */
DECLARE @op TABLE (Clave NVARCHAR(20) PRIMARY KEY, Id INT);

INSERT dbo.OrdenProduccion (NumeroOP, ProductoId, CantidadAProducir, Estado, OrdenCompraId, FechaCreacion, FechaInicio, FechaFin,
    DuracionEstimadaMinutos, FechaPausa, TiempoPausadoMinutos, CantidadAprobada, CantidadRechazada, ObservacionesInspeccion, PruebasAprobadas, PruebasRechazadas)
OUTPUT inserted.NumeroOP, inserted.Id INTO @op (Clave, Id)
VALUES
 -- Pendiente: para probar Iniciar / Ver simulación (2 min)
 (N'WO-PRUEBA-001', @v1, 4,   N'Pendiente',         NULL, DATEADD(MINUTE, -30, @ahora), NULL, NULL, 0, NULL, 0, 0, 0, NULL, 0, 0),
 -- En proceso: lleva 3 de 15 min; al cumplirse se cierra sola y descuenta componentes
 (N'WO-PRUEBA-002', @v1, 30,  N'EnProceso',         NULL, DATEADD(MINUTE, -20, @ahora), DATEADD(MINUTE, -3, @ahora), NULL, 15, NULL, 0, 0, 0, NULL, 0, 0),
 -- Pausada: trabajó 2 de 4 min y lleva 5 min en pausa (probar Reanudar)
 (N'WO-PRUEBA-003', @v1, 8,   N'Pausada',           NULL, DATEADD(MINUTE, -30, @ahora), DATEADD(MINUTE, -7, @ahora), NULL, 4, DATEADD(MINUTE, -5, @ahora), 0, 0, 0, NULL, 0, 0),
 -- En inspección: lista para Inspección Visual con foto
 (N'WO-PRUEBA-004', @v1, 6,   N'EnInspeccion',      NULL, DATEADD(HOUR, -5, @ahora), DATEADD(HOUR, -5, @ahora), NULL, 3, NULL, 0, 0, 0, NULL, 0, 0),
 -- En pruebas técnicas: 4 aprobadas en visual, ya se probaron 2 (faltan 2 para el Arduino)
 (N'WO-PRUEBA-005', @v1, 5,   N'EnPruebasTecnicas', NULL, DATEADD(HOUR, -8, @ahora), DATEADD(HOUR, -8, @ahora), NULL, 2.5, NULL, 0, 4, 1, N'Rayón en carcasa', 1, 1),
 -- Completadas
 (N'WO-PRUEBA-006', @v1, 10,  N'Completada',        NULL, DATEADD(DAY, -2, @ahora), DATEADD(DAY, -2, @ahora), DATEADD(DAY, -2, DATEADD(HOUR, 1, @ahora)), 5, NULL, 0, 9, 1, N'Aspa desbalanceada', 8, 1),
 (N'WO-PRUEBA-007', @v1, 30,  N'Completada',        NULL, DATEADD(DAY, -4, @ahora), DATEADD(DAY, -4, @ahora), DATEADD(DAY, -4, DATEADD(HOUR, 1, @ahora)), 15, NULL, 0, 30, 0, NULL, 30, 0),
 -- Completada sin piezas aprobadas en visual (ligada a OC-PRUEBA-001 para ver "OC vinculada")
 (N'WO-PRUEBA-008', @v1, 8,   N'Completada',        @oc1, DATEADD(DAY, -6, @ahora), DATEADD(DAY, -6, @ahora), DATEADD(DAY, -6, DATEADD(HOUR, 1, @ahora)), 4, NULL, 0, 0, 8, N'Motor mal soldado en todo el lote', 0, 0);

-- Receta copiada a cada orden; las que ya se fabricaron tienen el consumo registrado
INSERT dbo.OrdenProduccionDetalle (OrdenProduccionId, ComponenteId, CantidadRequerida, CantidadConsumida)
SELECT o.Id, pc.ComponenteId, pc.CantidadRequerida * op.CantidadAProducir,
       CASE WHEN op.Estado IN (N'Pendiente', N'EnProceso', N'Pausada') THEN 0 ELSE pc.CantidadRequerida * op.CantidadAProducir END
FROM @op o
JOIN dbo.OrdenProduccion op ON op.Id = o.Id
JOIN dbo.ProductoComponentes pc ON pc.ProductoId = op.ProductoId;


DECLARE @op5 INT = (SELECT Id FROM @op WHERE Clave = N'WO-PRUEBA-005');
DECLARE @op6 INT = (SELECT Id FROM @op WHERE Clave = N'WO-PRUEBA-006');
DECLARE @op7 INT = (SELECT Id FROM @op WHERE Clave = N'WO-PRUEBA-007');
DECLARE @op8 INT = (SELECT Id FROM @op WHERE Clave = N'WO-PRUEBA-008');

/* ---------------------------------------------------------------------
   7. PRUEBAS DE SENSORES (historial + lecturas)
   --------------------------------------------------------------------- */
DECLARE @h INT, @i INT, @t0 DATETIME2;

-- WO-PRUEBA-005, pieza 1: APROBADA (RPM y temperatura dentro de rango)
SET @t0 = DATEADD(HOUR, -3, @ahora);
INSERT dbo.HistorialPruebasSensores (OrdenProduccionId, FechaInicio, FechaFin, ResultadoFinal, Observaciones, DuracionSegundos)
VALUES (@op5, @t0, DATEADD(SECOND, 120, @t0), N'APROBADO', N'Pruebas completadas exitosamente', 120);
SET @h = SCOPE_IDENTITY(); SET @i = 0;
WHILE @i < 6 BEGIN
    INSERT dbo.DetalleLecturasSensores (HistorialId, FechaHora, Rpm, Temperatura, Humedad, Estado)
    VALUES (@h, DATEADD(SECOND, 55 + @i, @t0), 2450 + @i * 12, NULL, NULL, N'OK'),
           (@h, DATEADD(SECOND, 115 + @i, @t0), NULL, 23.4 + @i * 0.1, 46 + @i, N'OK');
    SET @i += 1;
END

-- WO-PRUEBA-005, pieza 2: RECHAZADA en RPM (no pasó a temperatura) -> scrap automático
SET @t0 = DATEADD(HOUR, -2, @ahora);
INSERT dbo.HistorialPruebasSensores (OrdenProduccionId, FechaInicio, FechaFin, ResultadoFinal, Observaciones, DuracionSegundos)
VALUES (@op5, @t0, DATEADD(SECOND, 60, @t0), N'RECHAZADO', N'Fallo en límites establecidos', 60);
SET @h = SCOPE_IDENTITY(); SET @i = 0;
WHILE @i < 6 BEGIN
    INSERT dbo.DetalleLecturasSensores (HistorialId, FechaHora, Rpm, Temperatura, Humedad, Estado)
    VALUES (@h, DATEADD(SECOND, 55 + @i, @t0), 1580 + @i * 9, NULL, NULL, N'FUERA_DE_RANGO');
    SET @i += 1;
END

-- WO-PRUEBA-006: una aprobada y una RECHAZADA en temperatura
SET @t0 = DATEADD(DAY, -2, DATEADD(HOUR, 2, @ahora));
INSERT dbo.HistorialPruebasSensores (OrdenProduccionId, FechaInicio, FechaFin, ResultadoFinal, Observaciones, DuracionSegundos)
VALUES (@op6, @t0, DATEADD(SECOND, 120, @t0), N'APROBADO', N'Pruebas completadas exitosamente', 120);
SET @h = SCOPE_IDENTITY(); SET @i = 0;
WHILE @i < 6 BEGIN
    INSERT dbo.DetalleLecturasSensores (HistorialId, FechaHora, Rpm, Temperatura, Humedad, Estado)
    VALUES (@h, DATEADD(SECOND, 55 + @i, @t0), 2610 - @i * 7, NULL, NULL, N'OK'),
           (@h, DATEADD(SECOND, 115 + @i, @t0), NULL, 24.1 + @i * 0.1, 44, N'OK');
    SET @i += 1;
END

SET @t0 = DATEADD(MINUTE, 10, @t0);
INSERT dbo.HistorialPruebasSensores (OrdenProduccionId, FechaInicio, FechaFin, ResultadoFinal, Observaciones, DuracionSegundos)
VALUES (@op6, @t0, DATEADD(SECOND, 120, @t0), N'RECHAZADO', N'Fallo en límites establecidos', 120);
SET @h = SCOPE_IDENTITY(); SET @i = 0;
WHILE @i < 6 BEGIN
    INSERT dbo.DetalleLecturasSensores (HistorialId, FechaHora, Rpm, Temperatura, Humedad, Estado)
    VALUES (@h, DATEADD(SECOND, 55 + @i, @t0), 2390 + @i * 5, NULL, NULL, N'OK'),
           (@h, DATEADD(SECOND, 115 + @i, @t0), NULL, 29.6 + @i * 0.2, 41, N'FUERA_DE_RANGO');
    SET @i += 1;
END

/* ---------------------------------------------------------------------
   8. SCRAP (automático por pruebas + capturado a mano)
   --------------------------------------------------------------------- */
INSERT dbo.RegistrosScrap (OrdenProduccionId, MaquinaId, MotivoRechazo, Cantidad, Fecha, Observaciones, Automatico) VALUES
 (@op5, @maq5, N'Velocidad baja (RPM)', 1, DATEADD(HOUR, -2, @ahora), N'Generado por Pruebas de Sensores. Falló: Velocidad baja (RPM)', 1),
 (@op5, @maq3, N'Defecto visual',       1, DATEADD(HOUR, -6, @ahora), N'Rayón en carcasa (inspección visual)', 0),
 (@op6, @maq5, N'Falla de temperatura', 1, DATEADD(DAY, -2, DATEADD(HOUR, 2, @ahora)), N'Generado por Pruebas de Sensores. Falló: Falla de temperatura', 1),
 (@op6, @maq2, N'Aspas dañadas',        1, DATEADD(DAY, -2, DATEADD(HOUR, 1, @ahora)), N'Aspa desbalanceada', 0),
 (@op8, @maq1, N'Motor defectuoso',     3, DATEADD(DAY, -6, DATEADD(HOUR, 1, @ahora)), N'Motor mal soldado', 0),
 (@op8, @maq1, N'Motor defectuoso',     2, DATEADD(DAY, -6, DATEADD(HOUR, 2, @ahora)), N'Motor no arranca', 0),
 (@op8, @maq4, N'Ensamble incorrecto',  3, DATEADD(DAY, -6, DATEADD(HOUR, 1, @ahora)), N'Cable de batería invertido', 0);

/* ---------------------------------------------------------------------
   8b. ALMACÉN DE PRODUCTO TERMINADO
      - WO-PRUEBA-007 (30 buenas) ya se recibió: lote con 3 pallets,
        2 ubicados y 1 esperando ubicación.
      - WO-PRUEBA-006 (8 buenas) queda pendiente de recibir.
      - WO-PRUEBA-008 no tiene piezas buenas: no aparece para recibir.
      El stock del producto solo sube con los pallets ya ubicados.
   --------------------------------------------------------------------- */
DECLARE @ubPT1 INT = (SELECT TOP 1 Id FROM dbo.Ubicaciones WHERE Codigo = N'PT-01');
DECLARE @ubPT2 INT = (SELECT TOP 1 Id FROM dbo.Ubicaciones WHERE Codigo = N'PT-02');
DECLARE @fRec DATETIME2 = DATEADD(DAY, -4, DATEADD(HOUR, 3, @ahora));
DECLARE @numLote NVARCHAR(30) = N'LPT-' + CONVERT(NVARCHAR(8), @fRec, 112) + N'-901';
DECLARE @lote INT;

INSERT dbo.LotesProductoTerminado (NumeroLote, OrdenProduccionId, ProductoId, Cantidad, TipoEmpaque, FechaRecepcion, RecibidoPor, Estado, Observaciones)
VALUES (@numLote, @op7, @v1, 30, N'Pallet', @fRec, @alm, N'PorUbicar', N'Lote de prueba');
SET @lote = SCOPE_IDENTITY();
INSERT dbo.PalletsProductoTerminado (CodigoPallet, LoteId, Cantidad, FechaEtiqueta, UbicacionId, FechaUbicacion, UbicadoPor, Estado) VALUES
 (@numLote + N'-P01', @lote, 10, @fRec, @ubPT1, DATEADD(MINUTE, 20, @fRec), @alm, N'Almacenado'),
 (@numLote + N'-P02', @lote, 10, @fRec, @ubPT2, DATEADD(MINUTE, 25, @fRec), @alm, N'Almacenado'),
 (@numLote + N'-P03', @lote, 10, @fRec, NULL,   NULL,                       NULL, N'PorUbicar');
UPDATE dbo.Productos SET Cantidad = Cantidad + 20 WHERE Id = @v1;

/* ---------------------------------------------------------------------
   9. DOWNTIME (paros terminados + uno simulado en curso)
   --------------------------------------------------------------------- */
DECLARE @d DATE = CAST(@ahora AS DATE);
INSERT dbo.RegistrosDowntime (MaquinaId, Causa, HoraInicio, HoraFin, DuracionMinutos, DuracionEstimadaMinutos, Estado, Simulado, Observaciones) VALUES
 (@maq1, N'Falla mecánica',           DATEADD(MINUTE, 8*60,      CAST(DATEADD(DAY, -3, @d) AS DATETIME2)), DATEADD(MINUTE, 8*60+45,  CAST(DATEADD(DAY, -3, @d) AS DATETIME2)), 45, 0, N'Finalizado', 0, N'Banda del motor rota'),
 (@maq1, N'Mantenimiento programado', DATEADD(MINUTE, 7*60,      CAST(DATEADD(DAY, -1, @d) AS DATETIME2)), DATEADD(MINUTE, 7*60+30,  CAST(DATEADD(DAY, -1, @d) AS DATETIME2)), 30, 0, N'Finalizado', 0, NULL),
 (@maq2, N'Falta de material',        DATEADD(MINUTE, 10*60,     CAST(DATEADD(DAY, -3, @d) AS DATETIME2)), DATEADD(MINUTE, 11*60+20, CAST(DATEADD(DAY, -3, @d) AS DATETIME2)), 80, 0, N'Finalizado', 0, N'No llegó el lote de aspas'),
 (@maq2, N'Falta de material',        DATEADD(MINUTE, 9*60,      CAST(DATEADD(DAY, -2, @d) AS DATETIME2)), DATEADD(MINUTE, 9*60+40,  CAST(DATEADD(DAY, -2, @d) AS DATETIME2)), 40, 0, N'Finalizado', 0, NULL),
 (@maq3, N'Cambio de modelo (setup)', DATEADD(MINUTE, 13*60,     CAST(DATEADD(DAY, -2, @d) AS DATETIME2)), DATEADD(MINUTE, 13*60+25, CAST(DATEADD(DAY, -2, @d) AS DATETIME2)), 25, 0, N'Finalizado', 0, N'Cambio de V1 a V2'),
 (@maq4, N'Falla eléctrica',          DATEADD(MINUTE, 15*60,     CAST(DATEADD(DAY, -1, @d) AS DATETIME2)), DATEADD(MINUTE, 15*60+35, CAST(DATEADD(DAY, -1, @d) AS DATETIME2)), 35, 0, N'Finalizado', 0, N'Corto en la fuente'),
 (@maq5, N'Ajuste de calidad',        DATEADD(MINUTE, 11*60,     CAST(DATEADD(DAY, -1, @d) AS DATETIME2)), DATEADD(MINUTE, 11*60+15, CAST(DATEADD(DAY, -1, @d) AS DATETIME2)), 15, 0, N'Finalizado', 0, N'Calibración del sensor DHT11'),
 (@maq5, N'Falla eléctrica',          DATEADD(MINUTE, -95, @ahora), DATEADD(MINUTE, -83, @ahora), 12, 12, N'Finalizado', 1, NULL),
 (@maq6, N'Falta de operador',        DATEADD(MINUTE, 14*60,     CAST(DATEADD(DAY, -3, @d) AS DATETIME2)), DATEADD(MINUTE, 14*60+20, CAST(DATEADD(DAY, -3, @d) AS DATETIME2)), 20, 0, N'Finalizado', 0, NULL),
 -- Paro simulado EN CURSO: aparece en "Simulación en vivo" y se cierra solo en ~25 min
 (@maq6, N'Falla mecánica',           DATEADD(MINUTE, -5, @ahora), NULL, 0, 30, N'EnCurso', 1, NULL);

COMMIT TRANSACTION;

PRINT 'Datos de prueba cargados.';
PRINT 'Usuarios: calidad@wong.com y almacen@wong.com (contraseña 12345); baja@wong.com está inactivo.';
