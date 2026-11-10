/* Datos de prueba de Producto Terminado con su trazabilidad completa:
   OC -> recepcion de lotes de material -> orden de produccion -> inspeccion visual
   -> pruebas de sensores -> scrap y paros -> lote de producto terminado -> cajas/pallets y ubicaciones.
   Correr en SSMS sobre la base del proyecto (MONITOREO). Las fechas son relativas al dia en que se corre.
   Contrasena de los usuarios nuevos: 12345 */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.LotesProductoTerminado', N'U') IS NULL OR COL_LENGTH(N'dbo.DetalleLecturasSensores', N'Ruido') IS NULL
BEGIN
    RAISERROR(N'Faltan migraciones, correr "dotnet ef database update".', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM dbo.Proveedores WHERE Nombre = N'Distribuidora Industrial del Noroeste')
BEGIN
    PRINT 'Los datos de Producto Terminado ya estaban cargados.';
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @ahora DATETIME2 = SYSDATETIME();
DECLARE @hash NVARCHAR(100) = N'$2a$11$3SePU8lYhefjbJpyoQ9c6eJClY1bqccdsAe/7zhteHbNghTpzNyaa';

DECLARE @prod INT = (SELECT TOP 1 p.Id FROM dbo.Productos p
                     WHERE EXISTS (SELECT 1 FROM dbo.ProductoComponentes pc WHERE pc.ProductoId = p.Id)
                     ORDER BY CASE WHEN p.Nombre LIKE N'%V1%' THEN 0 ELSE 1 END, p.Id);
IF @prod IS NULL
BEGIN
    RAISERROR(N'No hay ningun producto con receta (componentes).', 16, 1);
    ROLLBACK;
    RETURN;
END

DECLARE @mot INT = (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId WHERE pc.ProductoId = @prod AND (c.NumeroPieza LIKE N'%MOT%' OR c.Nombre LIKE N'%motor%'));
DECLARE @asp INT = (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId WHERE pc.ProductoId = @prod AND (c.NumeroPieza LIKE N'%ASP%' OR c.Nombre LIKE N'%aspa%'));
DECLARE @car INT = (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId WHERE pc.ProductoId = @prod AND (c.NumeroPieza LIKE N'%CASE%' OR c.Nombre LIKE N'%carcasa%'));
DECLARE @bat INT = (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId WHERE pc.ProductoId = @prod AND (c.NumeroPieza LIKE N'%BAT%' OR c.Nombre LIKE N'%bater%'));

-- usuarios
INSERT dbo.Usuarios (NombreCompleto, Email, Telefono, Activo, FechaAlta, PasswordHash)
SELECT v.Nombre, v.Email, v.Tel, 1, DATEADD(DAY, -30, @ahora), @hash
FROM (VALUES (N'Mariana Castro (Almacén PT)', N'almacenpt@wong.com', N'6621450987'),
             (N'Roberto Félix (Calidad)',     N'inspeccion@wong.com', N'6623329014')) v(Nombre, Email, Tel)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Usuarios u WHERE u.Email = v.Email);
DECLARE @alm INT = (SELECT TOP 1 ExpUsuarioId FROM dbo.Usuarios WHERE Email = N'almacenpt@wong.com');
DECLARE @qa  INT = (SELECT TOP 1 ExpUsuarioId FROM dbo.Usuarios WHERE Email = N'inspeccion@wong.com');

-- maquinas y ubicaciones
INSERT dbo.Maquinas (Codigo, Nombre, Activa)
SELECT v.Codigo, v.Nombre, 1
FROM (VALUES (N'MAQ-01', N'Ensambladora de motor'), (N'MAQ-02', N'Estación de aspas'), (N'MAQ-03', N'Estación de carcasa'),
             (N'MAQ-04', N'Cableado y batería'), (N'MAQ-05', N'Banco de pruebas'), (N'MAQ-06', N'Empaque')) v(Codigo, Nombre)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Maquinas m WHERE m.Codigo = v.Codigo);

INSERT dbo.Ubicaciones (Codigo, Zona, Tipo, CapacidadMaxima, Activo)
SELECT v.Codigo, v.Zona, N'Disponible', v.Cap, 1
FROM (VALUES (N'A-01', N'A', 500), (N'A-02', N'A', 500), (N'B-01', N'B', 300), (N'B-02', N'B', 300),
             (N'PT-01', N'PT', 200), (N'PT-02', N'PT', 200), (N'PT-03', N'PT', 200), (N'PT-04', N'PT', 200)) v(Codigo, Zona, Cap)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Ubicaciones u WHERE u.Codigo = v.Codigo);

-- proveedor, ordenes de compra y recepcion de material
INSERT dbo.Proveedores (Nombre, Telefono, Correo, Activo)
VALUES (N'Distribuidora Industrial del Noroeste', N'6622187340', N'ventas@dinoroeste.mx', 1);
DECLARE @prov INT = SCOPE_IDENTITY();

DECLARE @oc TABLE (Clave CHAR(1), Id INT, NumeroOC NVARCHAR(30), Dias INT, Sufijo NVARCHAR(5));
INSERT dbo.OrdenesCompra (NumeroOC, ProveedorId, Estado, FechaEsperada, FechaCreacion)
OUTPUT N'X', inserted.Id, inserted.NumeroOC, 0, N'' INTO @oc
VALUES (N'OC-2026-0141', @prov, N'Recibida', DATEADD(DAY, -16, @ahora), DATEADD(DAY, -21, @ahora)),
       (N'OC-2026-0158', @prov, N'Recibida', DATEADD(DAY, -7, @ahora),  DATEADD(DAY, -12, @ahora));
UPDATE @oc SET Clave = 'A', Dias = 16, Sufijo = N'A' WHERE NumeroOC = N'OC-2026-0141';
UPDATE @oc SET Clave = 'B', Dias = 7,  Sufijo = N'B' WHERE NumeroOC = N'OC-2026-0158';

DECLARE @mat TABLE (Comp INT, Codigo NVARCHAR(10), CantA INT, CantB INT, Ubic NVARCHAR(10));
INSERT @mat VALUES (@mot, N'MOT', 80, 70, N'A-01'), (@asp, N'ASP', 240, 210, N'A-02'), (@car, N'CASE', 80, 70, N'B-01'), (@bat, N'BAT', 80, 70, N'B-02');

INSERT dbo.OrdenCompraDetalle (OrdenCmpraId, ComponenteId, CantidadEsperada)
SELECT o.Id, m.Comp, CASE o.Clave WHEN 'A' THEN m.CantA ELSE m.CantB + CASE WHEN m.Codigo = N'BAT' THEN 20 ELSE 0 END END
FROM @oc o CROSS JOIN @mat m;

DECLARE @k CHAR(1) = 'A', @ocId INT, @dias INT, @suf NVARCHAR(5), @rec INT, @fRec DATETIME2;
WHILE @k IS NOT NULL
BEGIN
    SELECT @ocId = Id, @dias = Dias, @suf = Sufijo FROM @oc WHERE Clave = @k;
    SET @fRec = DATEADD(HOUR, 9, CAST(CAST(DATEADD(DAY, -@dias, @ahora) AS DATE) AS DATETIME2));

    INSERT dbo.Recepciones (OrdenCompraId, UsuarioId, FechaRecepcion, Estado) VALUES (@ocId, @alm, @fRec, N'Completada');
    SET @rec = SCOPE_IDENTITY();

    DECLARE @det TABLE (DetId INT, Comp INT);
    DELETE @det;
    INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
    OUTPUT inserted.Id, inserted.ComponenteId INTO @det
    SELECT @rec, m.Comp, N'LOT-' + m.Codigo + N'-2610' + @suf,
           CASE WHEN m.Codigo = N'BAT' THEN DATEADD(YEAR, 2, @fRec) END,
           CASE @k WHEN 'A' THEN m.CantA ELSE m.CantB END, N'Aprobado'
    FROM @mat m;

    INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
    SELECT d.DetId, 1, 1, 1, 1, @qa, N'Aprobado', NULL, DATEADD(MINUTE, 40, @fRec) FROM @det d;

    DECLARE @pal TABLE (PalId INT, Comp INT);
    DELETE @pal;
    INSERT dbo.Pallets (RecepcionDetalleId, CodigoPallet, FechaImpresionEtiqueta)
    OUTPUT inserted.Id, NULL INTO @pal (PalId, Comp)
    SELECT d.DetId, N'PAL-' + m.Codigo + N'-2610' + @suf, DATEADD(MINUTE, 50, @fRec)
    FROM @det d JOIN @mat m ON m.Comp = d.Comp;

    INSERT dbo.MovimientosInventario (PalletId, UbicacionId, TipoMovimiento, FechaMovimiento, RealizadoPor)
    SELECT p.Id, u.Id, N'Recepcion', DATEADD(MINUTE, 70, @fRec), @alm
    FROM @pal x
    JOIN dbo.Pallets p ON p.Id = x.PalId
    JOIN dbo.RecepcionDetalles d ON d.Id = p.RecepcionDetalleId
    JOIN @mat m ON m.Comp = d.ComponenteId
    JOIN dbo.Ubicaciones u ON u.Codigo = m.Ubic;

    IF @k = 'B'
    BEGIN
        INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
        VALUES (@rec, @bat, N'LOT-BAT-2610C', DATEADD(YEAR, 2, @fRec), 20, N'Rechazado');
        INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
        VALUES (SCOPE_IDENTITY(), 0, 1, 0, 1, @qa, N'Rechazado', N'Caja golpeada, 4 baterías infladas', DATEADD(MINUTE, 45, @fRec));
    END

    SET @k = CASE @k WHEN 'A' THEN 'B' END;
END

-- ordenes de produccion completadas
DECLARE @ops TABLE (
    N INT, NumeroOP NVARCHAR(30), OC CHAR(1), Cant INT, VisOk INT, VisMal INT, PrOk INT, PrMal INT,
    DiasFin INT, Defecto NVARCHAR(200) NULL,
    Empaque NVARCHAR(10) NULL, PorUnidad INT NULL, Ubicados INT NULL,   -- NULL = aun no entra al almacen
    Id INT NULL
);
INSERT @ops (N, NumeroOP, OC, Cant, VisOk, VisMal, PrOk, PrMal, DiasFin, Defecto, Empaque, PorUnidad, Ubicados) VALUES
 (1, N'WO-' + CONVERT(NVARCHAR(8), DATEADD(DAY, -13, @ahora), 112) + N'-A1F3C2', 'A', 40, 38, 2, 37, 1, 12, N'Carcasa con rebaba en 2 piezas', N'Pallet', 20, 2),
 (2, N'WO-' + CONVERT(NVARCHAR(8), DATEADD(DAY, -10, @ahora), 112) + N'-B7D410', 'A', 25, 25, 0, 24, 1, 9,  NULL,                              N'Caja',   10, 3),
 (3, N'WO-' + CONVERT(NVARCHAR(8), DATEADD(DAY, -5, @ahora), 112)  + N'-C93E57', 'B', 30, 28, 2, 26, 2, 4,  N'Aspa desbalanceada',              N'Pallet', 15, 1),
 (4, N'WO-' + CONVERT(NVARCHAR(8), DATEADD(DAY, -2, @ahora), 112)  + N'-D52A08', 'B', 20, 19, 1, 19, 0, 1,  N'Rayón en carcasa',                NULL, NULL, NULL),
 (5, N'WO-' + CONVERT(NVARCHAR(8), @ahora, 112)                    + N'-E1B6F9', 'B', 15, 15, 0, 14, 1, 0,  NULL,                              NULL, NULL, NULL);

DECLARE @n INT = 1, @opId INT, @cant INT, @fin DATETIME2, @ini DATETIME2;
WHILE @n <= 5
BEGIN
    SELECT @cant = Cant,
           @fin = CASE WHEN DiasFin = 0 THEN DATEADD(HOUR, -1, @ahora)
                       ELSE DATEADD(MINUTE, 15 * 60 + N * 7, CAST(CAST(DATEADD(DAY, -DiasFin, @ahora) AS DATE) AS DATETIME2)) END
    FROM @ops WHERE N = @n;
    SET @ini = DATEADD(MINUTE, -(@cant * 3 + 180), @fin);

    INSERT dbo.OrdenProduccion (NumeroOP, ProductoId, CantidadAProducir, Estado, OrdenCompraId, FechaCreacion, FechaInicio, FechaFin,
        DuracionEstimadaMinutos, FechaPausa, TiempoPausadoMinutos, CantidadAprobada, CantidadRechazada, ObservacionesInspeccion, PruebasAprobadas, PruebasRechazadas)
    SELECT o.NumeroOP, @prod, o.Cant, N'Completada', oc.Id, DATEADD(HOUR, -3, @ini), @ini, @fin,
           o.Cant * 0.5, NULL, 0, o.VisOk, o.VisMal, o.Defecto, o.PrOk, o.PrMal
    FROM @ops o JOIN @oc oc ON oc.Clave = o.OC
    WHERE o.N = @n;
    SET @opId = SCOPE_IDENTITY();
    UPDATE @ops SET Id = @opId WHERE N = @n;

    INSERT dbo.OrdenProduccionDetalle (OrdenProduccionId, ComponenteId, CantidadRequerida, CantidadConsumida)
    SELECT @opId, pc.ComponenteId, pc.CantidadRequerida * @cant, pc.CantidadRequerida * @cant
    FROM dbo.ProductoComponentes pc WHERE pc.ProductoId = @prod;

    -- paros durante la orden
    INSERT dbo.RegistrosDowntime (MaquinaId, Causa, HoraInicio, HoraFin, DuracionMinutos, DuracionEstimadaMinutos, Estado, Simulado, Observaciones, OrdenProduccionId)
    SELECT m.Id, v.Causa, DATEADD(MINUTE, v.Despues, @ini), DATEADD(MINUTE, v.Despues + v.Minutos, @ini), v.Minutos, 0, N'Finalizado', 0, v.Obs, @opId
    FROM (VALUES
            (1, N'MAQ-01', N'Falla mecánica',           35,  18, N'Se trabó el eje del motor'),
            (1, N'MAQ-05', N'Ajuste de calidad',        150, 12, N'Recalibración del sensor de ruido'),
            (2, N'MAQ-02', N'Falta de material',        50,  25, N'Se acabaron las aspas en la estación'),
            (3, N'MAQ-03', N'Cambio de modelo (setup)', 20,  30, NULL),
            (3, N'MAQ-04', N'Falla eléctrica',          110, 14, N'Se botó el breaker de la línea'),
            (4, N'MAQ-06', N'Falta de operador',        90,  20, N'Operador en capacitación'),
            (5, N'MAQ-05', N'Mantenimiento programado', 30,  15, NULL)
         ) v(N, Maq, Causa, Despues, Minutos, Obs)
    JOIN dbo.Maquinas m ON m.Codigo = v.Maq
    WHERE v.N = @n;

    -- scrap de la inspeccion visual
    INSERT dbo.RegistrosScrap (OrdenProduccionId, MaquinaId, MotivoRechazo, Cantidad, Fecha, Observaciones, Automatico)
    SELECT @opId, m.Id, N'Defecto visual', o.VisMal, DATEADD(MINUTE, -70, @fin), o.Defecto, 0
    FROM @ops o JOIN dbo.Maquinas m ON m.Codigo = N'MAQ-03'
    WHERE o.N = @n AND o.VisMal > 0;

    -- pruebas de sensores: un registro por pieza
    DECLARE @piezas INT, @malas INT, @i INT = 1, @falla INT, @t0 DATETIME2, @h INT, @s INT, @motivo NVARCHAR(50);
    SELECT @piezas = PrOk + PrMal, @malas = PrMal FROM @ops WHERE N = @n;
    SET @i = 1;
    WHILE @i <= @piezas
    BEGIN
        SET @falla = CASE WHEN @i > @piezas - @malas THEN (@n + @i) % 4 ELSE -1 END;
        SET @t0 = DATEADD(MINUTE, -(@piezas - @i + 1) * 5, @fin);

        INSERT dbo.HistorialPruebasSensores (OrdenProduccionId, FechaInicio, FechaFin, ResultadoFinal, Observaciones, DuracionSegundos)
        VALUES (@opId, @t0, DATEADD(SECOND, CASE WHEN @falla < 0 THEN 240 ELSE (@falla + 1) * 60 END, @t0),
                CASE WHEN @falla < 0 THEN N'APROBADO' ELSE N'RECHAZADO' END,
                CASE WHEN @falla < 0 THEN N'Pruebas completadas exitosamente' ELSE N'Fallo en límites establecidos' END,
                CASE WHEN @falla < 0 THEN 240 ELSE (@falla + 1) * 60 END);
        SET @h = SCOPE_IDENTITY();

        SET @s = 0;
        WHILE @s <= CASE WHEN @falla < 0 THEN 3 ELSE @falla END
        BEGIN
            INSERT dbo.DetalleLecturasSensores (HistorialId, FechaHora, Rpm, Temperatura, Humedad, Vibracion, Ruido, Estado)
            SELECT @h, DATEADD(SECOND, @s * 60 + 55 + v.j, @t0),
                   ROUND(CASE WHEN @s = 0 THEN CASE WHEN @s = @falla THEN 1550 + x.r * 300 ELSE 2300 + x.r * 450 END END, 0),
                   ROUND(CASE WHEN @s = 1 THEN CASE WHEN @s = @falla THEN 29.3 + x.r * 1.5 ELSE 22.5 + x.r * 3.5 END END, 1),
                   ROUND(CASE WHEN @s = 1 THEN 64 + x.r * 14 END, 1),
                   ROUND(CASE WHEN @s = 2 THEN CASE WHEN @s = @falla THEN 1.36 + x.r * 0.2 ELSE 1.02 + x.r * 0.18 END END, 2),
                   ROUND(CASE WHEN @s = 3 THEN CASE WHEN @s = @falla THEN 32.5 + x.r * 5 ELSE 23.5 + x.r * 5.5 END END, 1),
                   CASE WHEN @s = @falla THEN N'FUERA_DE_RANGO' ELSE N'OK' END
            FROM (VALUES (0), (1), (2)) v(j)
            CROSS APPLY (SELECT (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0 AS r) x;
            SET @s += 1;
        END

        IF @falla >= 0
        BEGIN
            SET @motivo = CASE @falla WHEN 0 THEN N'Velocidad baja (RPM)' WHEN 1 THEN N'Falla de temperatura'
                                      WHEN 2 THEN N'Vibración excesiva' ELSE N'Ruido excesivo' END;
            INSERT dbo.RegistrosScrap (OrdenProduccionId, MaquinaId, MotivoRechazo, Cantidad, Fecha, Observaciones, Automatico)
            SELECT @opId, m.Id, @motivo, 1, DATEADD(SECOND, (@falla + 1) * 60, @t0), N'Generado por Pruebas de Sensores. Falló: ' + @motivo, 1
            FROM dbo.Maquinas m WHERE m.Codigo = N'MAQ-05';
        END
        SET @i += 1;
    END

    SET @n += 1;
END

-- entrada al almacen de producto terminado (las ordenes 4 y 5 quedan pendientes de recibir)
DECLARE @lote INT, @numLote NVARCHAR(30), @fLote DATETIME2, @buenas INT, @porUnidad INT, @ubicados INT, @emp NVARCHAR(10), @u INT, @unidades INT;
DECLARE @ubic TABLE (Orden INT IDENTITY, Codigo NVARCHAR(10));
INSERT @ubic (Codigo) VALUES (N'PT-01'), (N'PT-01'), (N'PT-02'), (N'PT-02'), (N'PT-03'), (N'PT-03'), (N'PT-04');
DECLARE @sigUbic INT = 1;

SET @n = 1;
WHILE @n <= 3
BEGIN
    SELECT @opId = o.Id, @buenas = o.PrOk, @porUnidad = o.PorUnidad, @ubicados = o.Ubicados, @emp = o.Empaque,
           @fLote = DATEADD(HOUR, 18, CAST(CAST(op.FechaFin AS DATE) AS DATETIME2))
    FROM @ops o JOIN dbo.OrdenProduccion op ON op.Id = o.Id
    WHERE o.N = @n;

    SET @numLote = N'LPT-' + CONVERT(NVARCHAR(8), @fLote, 112) + N'-'
                 + RIGHT(N'00' + CAST((SELECT COUNT(*) FROM dbo.LotesProductoTerminado WHERE NumeroLote LIKE N'LPT-' + CONVERT(NVARCHAR(8), @fLote, 112) + N'-%') + 1 AS NVARCHAR(3)), 3);
    SET @unidades = CEILING(@buenas / CAST(@porUnidad AS FLOAT));

    INSERT dbo.LotesProductoTerminado (NumeroLote, OrdenProduccionId, ProductoId, Cantidad, TipoEmpaque, FechaRecepcion, RecibidoPor, Estado, Observaciones)
    VALUES (@numLote, @opId, @prod, @buenas, @emp, @fLote, @alm,
            CASE WHEN @ubicados >= @unidades THEN N'Almacenado' ELSE N'PorUbicar' END,
            CASE @n WHEN 1 THEN N'Pallets emplayados' WHEN 3 THEN N'Un pallet espera lugar en rack' END);
    SET @lote = SCOPE_IDENTITY();

    SET @u = 1;
    WHILE @u <= @unidades
    BEGIN
        INSERT dbo.PalletsProductoTerminado (CodigoPallet, LoteId, Cantidad, FechaEtiqueta, UbicacionId, FechaUbicacion, UbicadoPor, Estado)
        SELECT @numLote + N'-' + CASE @emp WHEN N'Caja' THEN N'C' ELSE N'P' END + RIGHT(N'0' + CAST(@u AS NVARCHAR(2)), 2),
               @lote,
               CASE WHEN @u < @unidades THEN @porUnidad ELSE @buenas - @porUnidad * (@unidades - 1) END,
               DATEADD(MINUTE, 5, @fLote),
               CASE WHEN @u <= @ubicados THEN (SELECT Id FROM dbo.Ubicaciones WHERE Codigo = (SELECT Codigo FROM @ubic WHERE Orden = @sigUbic)) END,
               CASE WHEN @u <= @ubicados THEN DATEADD(MINUTE, 20 + @u * 6, @fLote) END,
               CASE WHEN @u <= @ubicados THEN @alm END,
               CASE WHEN @u <= @ubicados THEN N'Almacenado' ELSE N'PorUbicar' END;
        IF @u <= @ubicados SET @sigUbic += 1;
        SET @u += 1;
    END
    SET @n += 1;
END

-- existencias: lo ubicado entra al stock del producto; el material recibido menos lo consumido al de componentes
UPDATE dbo.Productos
SET Cantidad = Cantidad + (SELECT ISNULL(SUM(p.Cantidad), 0) FROM dbo.PalletsProductoTerminado p
                           JOIN dbo.LotesProductoTerminado l ON l.Id = p.LoteId
                           JOIN @ops o ON o.Id = l.OrdenProduccionId
                           WHERE p.Estado = N'Almacenado')
WHERE Id = @prod;

UPDATE c
SET Cantidad = c.Cantidad + m.CantA + m.CantB
             - (SELECT SUM(d.CantidadConsumida) FROM dbo.OrdenProduccionDetalle d JOIN @ops o ON o.Id = d.OrdenProduccionId WHERE d.ComponenteId = c.Id)
FROM dbo.Componentes c JOIN @mat m ON m.Comp = c.Id;

COMMIT;

PRINT 'Datos de Producto Terminado cargados.';
PRINT 'Usuarios: almacenpt@wong.com e inspeccion@wong.com (contraseña 12345).';
