/* Datos extra para el modulo de Indicadores de Desempeno.
   Usa solo el producto y los 4 componentes que ya existen. Agrega dos proveedores,
   ordenes de compra con recepciones e inspeccion QA, ordenes de produccion de las ultimas dos semanas
   y de hace dos meses, scrap, paros, pruebas de sensores, lotes de producto terminado y asistencias.
   Correr en SSMS sobre la base del proyecto (MONITOREO) despues de datos_producto_terminado.sql.
   Las fechas son relativas al dia en que se corre. Si ya se corrio antes, no hace nada. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET DATEFIRST 1; -- lunes = 1

IF NOT EXISTS (SELECT 1 FROM dbo.Proveedores WHERE Nombre = N'Distribuidora Industrial del Noroeste')
BEGIN
    RAISERROR(N'Primero hay que correr datos_producto_terminado.sql.', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM dbo.Proveedores WHERE Nombre = N'Electrónica y Plásticos del Pacífico')
BEGIN
    PRINT 'Los datos de Indicadores ya estaban cargados.';
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @ahora DATETIME2 = SYSDATETIME();
DECLARE @hoy DATETIME2 = CAST(CAST(@ahora AS DATE) AS DATETIME2);
DECLARE @alm INT = (SELECT TOP 1 ExpUsuarioId FROM dbo.Usuarios WHERE Email = N'almacenpt@wong.com');
DECLARE @qa  INT = (SELECT TOP 1 ExpUsuarioId FROM dbo.Usuarios WHERE Email = N'inspeccion@wong.com');
DECLARE @p1  INT = (SELECT TOP 1 Id FROM dbo.Productos ORDER BY CASE WHEN Nombre LIKE N'%V1%' THEN 0 ELSE 1 END, Id);

-- numeros 1..500 para generar filas
DECLARE @num TABLE (n INT PRIMARY KEY);
INSERT @num SELECT TOP 500 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) FROM sys.all_objects;

DECLARE @comp TABLE (Codigo NVARCHAR(10) PRIMARY KEY, Id INT);
INSERT @comp SELECT v.Codigo, c.Id
FROM (VALUES (N'MOT', N'%MOT%', N'%motor%'), (N'ASP', N'%ASP%', N'%aspa%'), (N'CASE', N'%CASE%', N'%carcasa%'), (N'BAT', N'%BAT%', N'%bater%')) v(Codigo, NP, Nom)
CROSS APPLY (SELECT TOP 1 c.Id FROM dbo.ProductoComponentes pc JOIN dbo.Componentes c ON c.Id = pc.ComponenteId
             WHERE pc.ProductoId = @p1 AND (c.NumeroPieza LIKE v.NP OR c.Nombre LIKE v.Nom)) c;

/* ---------- proveedores ---------- */
INSERT dbo.Proveedores (Nombre, Telefono, Correo, Activo)
VALUES (N'Electrónica y Plásticos del Pacífico', N'6699874521', N'pedidos@epp.mx', 1),
       (N'Motores y Baterías de Sonora', N'6624418830', N'ventas@mbsonora.mx', 1);

DECLARE @prov TABLE (Clave INT, Id INT);
INSERT @prov SELECT 1, Id FROM dbo.Proveedores WHERE Nombre = N'Distribuidora Industrial del Noroeste';
INSERT @prov SELECT 2, Id FROM dbo.Proveedores WHERE Nombre = N'Electrónica y Plásticos del Pacífico';
INSERT @prov SELECT 3, Id FROM dbo.Proveedores WHERE Nombre = N'Motores y Baterías de Sonora';

/* ---------- ordenes de compra, recepcion e inspeccion QA ---------- */
-- DCrea / DEsp / DRec = dias antes de hoy (DRec NULL = todavia no llega)
DECLARE @oc TABLE (NumeroOC NVARCHAR(30), Prov INT, Estado NVARCHAR(20), DCrea INT, DEsp INT, DRec INT NULL, Id INT NULL);
INSERT @oc (NumeroOC, Prov, Estado, DCrea, DEsp, DRec) VALUES
    (N'OC-2026-0118', 1, N'Recibida', 58, 52, 51),
    (N'OC-2026-0127', 3, N'Recibida', 46, 41, 41),
    (N'OC-2026-0163', 2, N'Recibida', 20, 14, 12),
    (N'OC-2026-0170', 3, N'Recibida', 15,  9,  9),
    (N'OC-2026-0176', 1, N'Recibida', 11,  5,  4),
    (N'OC-2026-0181', 2, N'Recibida',  8,  3,  3),
    (N'OC-2026-0187', 3, N'Abierta',   4, -3, NULL),
    (N'OC-2026-0190', 1, N'Abierta',   2, -5, NULL);

INSERT dbo.OrdenesCompra (NumeroOC, ProveedorId, Estado, FechaEsperada, FechaCreacion)
SELECT o.NumeroOC, p.Id, o.Estado, DATEADD(DAY, -o.DEsp, @hoy), DATEADD(HOUR, 10, DATEADD(DAY, -o.DCrea, @hoy))
FROM @oc o JOIN @prov p ON p.Clave = o.Prov;
UPDATE o SET Id = x.Id FROM @oc o JOIN dbo.OrdenesCompra x ON x.NumeroOC = o.NumeroOC;

-- material de cada OC y resultado de su inspeccion (1 = ok, 0 = falla)
DECLARE @mat TABLE (NumeroOC NVARCHAR(30), Codigo NVARCHAR(10), Cant INT, Res NVARCHAR(20), Emp BIT, Eti BIT, Mat BIT, Cert BIT, Coment NVARCHAR(200) NULL);
INSERT @mat VALUES
    (N'OC-2026-0118', N'MOT',  100, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0118', N'ASP',  300, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0118', N'CASE', 100, N'Aprobado',  1, 0, 1, 1, N'Etiqueta con lote ilegible, se re-etiquetó'),
    (N'OC-2026-0127', N'BAT',  120, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0127', N'MOT',  120, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0163', N'CASE', 150, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0163', N'ASP',  450, N'Rechazado', 1, 1, 0, 1, N'Aspas con rebaba y deformadas en la muestra'),
    (N'OC-2026-0163', N'BAT',  150, N'Aprobado',  0, 1, 1, 1, N'Caja maltratada, material en buen estado'),
    (N'OC-2026-0170', N'MOT',  150, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0170', N'BAT',  150, N'Rechazado', 1, 1, 1, 0, N'Llegó sin certificado UN38.3'),
    (N'OC-2026-0176', N'ASP',  450, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0176', N'CASE', 150, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0181', N'MOT',  100, N'Aprobado',  1, 1, 1, 1, NULL),
    (N'OC-2026-0181', N'CASE', 100, N'Rechazado', 0, 0, 1, 1, N'Empaque abierto y sin etiqueta de lote'),
    (N'OC-2026-0187', N'MOT',  120, NULL,         1, 1, 1, 1, NULL),
    (N'OC-2026-0187', N'BAT',  120, NULL,         1, 1, 1, 1, NULL),
    (N'OC-2026-0190', N'ASP',  360, NULL,         1, 1, 1, 1, NULL),
    (N'OC-2026-0190', N'CASE', 120, NULL,         1, 1, 1, 1, NULL);

INSERT dbo.OrdenCompraDetalle (OrdenCmpraId, ComponenteId, CantidadEsperada)
SELECT o.Id, c.Id, m.Cant FROM @mat m JOIN @oc o ON o.NumeroOC = m.NumeroOC JOIN @comp c ON c.Codigo = m.Codigo;

INSERT dbo.Recepciones (OrdenCompraId, UsuarioId, FechaRecepcion, Estado)
SELECT o.Id, @alm, DATEADD(HOUR, 9, DATEADD(DAY, -o.DRec, @hoy)), N'Completada' FROM @oc o WHERE o.DRec IS NOT NULL;

INSERT dbo.RecepcionDetalles (RecepcionId, ComponenteId, NumeroLote, FechaCaducidad, CantidadRecibida, Estado)
SELECT r.Id, c.Id, N'LOT-' + m.Codigo + N'-' + RIGHT(m.NumeroOC, 4), NULL, m.Cant, m.Res
FROM @mat m JOIN @oc o ON o.NumeroOC = m.NumeroOC JOIN dbo.Recepciones r ON r.OrdenCompraId = o.Id JOIN @comp c ON c.Codigo = m.Codigo
WHERE m.Res IS NOT NULL;

INSERT dbo.InspeccionesQA (RecepcionDetalleId, EmpaqueOk, EtiquetaOk, MaterialOk, CertificadoDisponible, InspeccionadoPor, Resultado, Comentarios, FechaInspeccion)
SELECT d.Id, m.Emp, m.Eti, m.Mat, m.Cert, @qa, m.Res, m.Coment, DATEADD(MINUTE, 40, r.FechaRecepcion)
FROM @mat m JOIN @oc o ON o.NumeroOC = m.NumeroOC JOIN dbo.Recepciones r ON r.OrdenCompraId = o.Id
JOIN @comp c ON c.Codigo = m.Codigo JOIN dbo.RecepcionDetalles d ON d.RecepcionId = r.Id AND d.ComponenteId = c.Id
WHERE m.Res IS NOT NULL;

-- un pallet por lote aprobado y su movimiento a una ubicacion de materia prima
INSERT dbo.Pallets (RecepcionDetalleId, CodigoPallet, FechaImpresionEtiqueta)
SELECT d.Id, N'PAL-' + m.Codigo + N'-' + RIGHT(m.NumeroOC, 4), DATEADD(MINUTE, 50, r.FechaRecepcion)
FROM @mat m JOIN @oc o ON o.NumeroOC = m.NumeroOC JOIN dbo.Recepciones r ON r.OrdenCompraId = o.Id
JOIN @comp c ON c.Codigo = m.Codigo JOIN dbo.RecepcionDetalles d ON d.RecepcionId = r.Id AND d.ComponenteId = c.Id
WHERE m.Res = N'Aprobado';

INSERT dbo.MovimientosInventario (PalletId, UbicacionId, TipoMovimiento, FechaMovimiento, RealizadoPor)
SELECT p.Id, u.Id, N'Recepcion', DATEADD(MINUTE, 70, r.FechaRecepcion), @alm
FROM @oc o JOIN dbo.Recepciones r ON r.OrdenCompraId = o.Id
JOIN dbo.RecepcionDetalles d ON d.RecepcionId = r.Id
JOIN dbo.Pallets p ON p.RecepcionDetalleId = d.Id
JOIN dbo.Ubicaciones u ON u.Codigo = CHOOSE(p.Id % 4 + 1, N'A-01', N'A-02', N'B-01', N'B-02');

/* ---------- ordenes de produccion ---------- */
-- Dias = dias antes de hoy en que se creo la orden
DECLARE @ops TABLE (N INT PRIMARY KEY, Dias INT, Cant INT, Aprob INT, Rech INT, Estado NVARCHAR(20), Id INT NULL, Fin DATETIME2 NULL);
INSERT @ops (N, Dias, Cant, Aprob, Rech, Estado) VALUES
    ( 1, 56, 30, 29, 1, N'Completada'),
    ( 2, 50, 20, 18, 2, N'Completada'),
    ( 3, 43, 35, 33, 2, N'Completada'),
    ( 4, 38, 15, 13, 2, N'Completada'),
    ( 5, 13, 25, 24, 1, N'Completada'),
    ( 6, 12, 40, 37, 3, N'Completada'),
    ( 7, 11, 20, 17, 3, N'Completada'),
    ( 8, 10, 30, 29, 1, N'Completada'),
    ( 9,  9, 35, 35, 0, N'Completada'),
    (10,  8, 25, 21, 4, N'Completada'),
    (11,  7, 30, 28, 2, N'Completada'),
    (12,  6, 40, 38, 2, N'Completada'),
    (13,  5, 20, 19, 1, N'Completada'),
    (14,  4, 45, 41, 4, N'Completada'),
    (15,  3, 30, 29, 1, N'Completada'),
    (16,  2, 25, 22, 3, N'Completada'),
    (17,  1, 30, 29, 1, N'Completada'),
    (18,  1, 20,  0, 0, N'Cancelada'),
    (19,  0, 30,  0, 0, N'Pendiente');

INSERT dbo.OrdenProduccion (NumeroOP, ProductoId, CantidadAProducir, Estado, OrdenCompraId, FechaCreacion, FechaInicio, FechaFin,
    DuracionEstimadaMinutos, FechaPausa, TiempoPausadoMinutos, CantidadAprobada, CantidadRechazada, ObservacionesInspeccion, PruebasAprobadas, PruebasRechazadas)
SELECT N'WO-' + CONVERT(NVARCHAR(8), DATEADD(DAY, -o.Dias, @hoy), 112) + N'-' + UPPER(LEFT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N''), 6)),
       @p1,
       o.Cant, o.Estado, NULL,
       DATEADD(MINUTE, 7 * 60 + o.N * 4, DATEADD(DAY, -o.Dias, @hoy)),
       CASE WHEN o.Estado = N'Completada' THEN DATEADD(MINUTE, 8 * 60, DATEADD(DAY, -o.Dias, @hoy)) END,
       CASE WHEN o.Estado = N'Completada' THEN DATEADD(MINUTE, 8 * 60 + o.Cant * 4 + 60, DATEADD(DAY, -o.Dias, @hoy)) END,
       o.Cant * 0.5, NULL, 0, o.Aprob, o.Rech, NULL, o.Aprob, 0
FROM @ops o ORDER BY o.N;

-- recuperar el Id de cada orden (la fecha de creacion es unica por renglon: dia + N * 4 minutos)
UPDATE o SET Id = op.Id, Fin = op.FechaFin
FROM @ops o JOIN dbo.OrdenProduccion op
  ON op.FechaCreacion = DATEADD(MINUTE, 7 * 60 + o.N * 4, DATEADD(DAY, -o.Dias, @hoy))
 AND op.CantidadAProducir = o.Cant AND op.NumeroOP LIKE N'WO-' + CONVERT(NVARCHAR(8), DATEADD(DAY, -o.Dias, @hoy), 112) + N'-%';

INSERT dbo.OrdenProduccionDetalle (OrdenProduccionId, ComponenteId, CantidadRequerida, CantidadConsumida)
SELECT o.Id, pc.ComponenteId, pc.CantidadRequerida * o.Cant, CASE WHEN o.Estado = N'Completada' THEN pc.CantidadRequerida * o.Cant ELSE 0 END
FROM @ops o JOIN dbo.ProductoComponentes pc ON pc.ProductoId = @p1
WHERE o.Estado <> N'Cancelada';

/* ---------- scrap: una fila por pieza rechazada ----------
   Los motivos tienen peso distinto para que el Pareto se vea como en planta
   (pocos motivos explican la mayoria del scrap). */
DECLARE @motivos TABLE (Desde INT, Hasta INT, Motivo NVARCHAR(100), Maq NVARCHAR(10), Auto BIT);
INSERT @motivos VALUES
    ( 0, 31, N'Defecto visual',              N'MAQ-03', 0),
    (32, 53, N'Velocidad baja (RPM)',        N'MAQ-05', 1),
    (54, 69, N'Ensamble incorrecto',         N'MAQ-01', 0),
    (70, 81, N'Aspas dañadas',               N'MAQ-02', 0),
    (82, 90, N'Falla de temperatura',        N'MAQ-05', 1),
    (91, 96, N'Falla de batería o cableado', N'MAQ-04', 0),
    (97, 99, N'Motor defectuoso',            N'MAQ-01', 0);

DECLARE @scr TABLE (OpId INT, Fin DATETIME2, i INT, Motivo NVARCHAR(100), Maq NVARCHAR(10), Auto BIT);
INSERT @scr
SELECT o.Id, o.Fin, n.n, m.Motivo, m.Maq, m.Auto
FROM @ops o JOIN @num n ON n.n <= o.Rech
JOIN @motivos m ON (o.N * 37 + n.n * 23) % 100 BETWEEN m.Desde AND m.Hasta
WHERE o.Estado = N'Completada';

INSERT dbo.RegistrosScrap (OrdenProduccionId, MaquinaId, MotivoRechazo, Cantidad, Fecha, Observaciones, Automatico)
SELECT s.OpId, mq.Id, s.Motivo, 1, DATEADD(MINUTE, -30 - s.i * 7, s.Fin),
       CASE WHEN s.Auto = 1 THEN N'Generado por Pruebas de Sensores. Falló: ' + s.Motivo END, s.Auto
FROM @scr s JOIN dbo.Maquinas mq ON mq.Codigo = s.Maq;

-- las piezas que rechazo el banco de pruebas cuentan como pruebas rechazadas
UPDATE op SET PruebasRechazadas = x.n
FROM dbo.OrdenProduccion op JOIN (SELECT OpId, COUNT(*) n FROM @scr WHERE Auto = 1 GROUP BY OpId) x ON x.OpId = op.Id;

/* ---------- paros (downtime) ---------- */
DECLARE @causas TABLE (Desde INT, Hasta INT, Causa NVARCHAR(100), Maq NVARCHAR(10));
INSERT @causas VALUES
    ( 0, 27, N'Cambio de modelo (setup)', N'MAQ-01'),
    (28, 47, N'Falla mecánica',           N'MAQ-02'),
    (48, 63, N'Falta de material',        N'MAQ-03'),
    (64, 75, N'Falla eléctrica',          N'MAQ-04'),
    (76, 85, N'Ajuste de calidad',        N'MAQ-05'),
    (86, 93, N'Mantenimiento programado', N'MAQ-06'),
    (94, 99, N'Falta de operador',        N'MAQ-01');

INSERT dbo.RegistrosDowntime (MaquinaId, Causa, HoraInicio, HoraFin, DuracionMinutos, DuracionEstimadaMinutos, Estado, Simulado, Observaciones, OrdenProduccionId)
SELECT mq.Id, c.Causa, x.Inicio, DATEADD(MINUTE, x.Minutos, x.Inicio), x.Minutos, 0, N'Finalizado', 0, NULL, x.Id
FROM (
    SELECT o.Id, o.N, k.n AS k,
           DATEADD(MINUTE, 8 * 60 + 25 + k.n * 70, DATEADD(DAY, -o.Dias, @hoy)) AS Inicio,
           6 + (o.N * 17 + k.n * 11) % 38 AS Minutos,
           (o.N * 13 + k.n * 29) % 100 AS Pick
    FROM @ops o JOIN @num k ON k.n <= 1 + o.N % 2
    WHERE o.Estado = N'Completada'
) x
JOIN @causas c ON x.Pick BETWEEN c.Desde AND c.Hasta
JOIN dbo.Maquinas mq ON mq.Codigo = c.Maq;

/* ---------- pruebas de sensores: una por pieza probada ---------- */
DECLARE @pru TABLE (HistId INT, OpId INT, Ok BIT, Falla NVARCHAR(100) NULL, Inicio DATETIME2);

INSERT dbo.HistorialPruebasSensores (OrdenProduccionId, FechaInicio, FechaFin, ResultadoFinal, Observaciones, DuracionSegundos)
OUTPUT inserted.Id, inserted.OrdenProduccionId, CASE WHEN inserted.ResultadoFinal = N'APROBADO' THEN 1 ELSE 0 END,
       CASE WHEN inserted.ResultadoFinal = N'RECHAZADO' THEN inserted.Observaciones END, inserted.FechaInicio INTO @pru
SELECT t.OpId, t.Inicio, DATEADD(SECOND, t.Dur, t.Inicio), t.Res, t.Obs, t.Dur
FROM (
    SELECT o.Id AS OpId, DATEADD(MINUTE, -n.n * 4, DATEADD(MINUTE, -20, o.Fin)) AS Inicio,
           N'APROBADO' AS Res, N'Pruebas completadas exitosamente' AS Obs, 220 + (n.n * 7) % 40 AS Dur
    FROM @ops o JOIN @num n ON n.n <= o.Aprob WHERE o.Estado = N'Completada'
    UNION ALL
    SELECT s.OpId, DATEADD(MINUTE, -2, DATEADD(MINUTE, -30 - s.i * 7, s.Fin)), N'RECHAZADO', s.Motivo, 90 + s.i * 10
    FROM @scr s WHERE s.Auto = 1
) t;

-- 4 lecturas por prueba: RPM, temperatura y humedad, vibracion, ruido
INSERT dbo.DetalleLecturasSensores (HistorialId, FechaHora, Rpm, Temperatura, Humedad, Vibracion, Ruido, Estado)
SELECT p.HistId, DATEADD(SECOND, s.n * 50, p.Inicio),
       CASE WHEN s.n = 1 THEN ROUND(CASE WHEN p.Falla = N'Velocidad baja (RPM)' THEN 1500 + r.v * 350 ELSE 2600 + r.v * 900 END, 0) END,
       CASE WHEN s.n = 2 THEN ROUND(CASE WHEN p.Falla = N'Falla de temperatura' THEN 30 + r.v * 2 ELSE 21.5 + r.v * 4 END, 1) END,
       CASE WHEN s.n = 2 THEN ROUND(55 + r.v * 20, 1) END,
       CASE WHEN s.n = 3 THEN ROUND(0.9 + r.v * 0.35, 2) END,
       CASE WHEN s.n = 4 THEN ROUND(22 + r.v * 8, 1) END,
       CASE WHEN (s.n = 1 AND p.Falla = N'Velocidad baja (RPM)') OR (s.n = 2 AND p.Falla = N'Falla de temperatura') THEN N'FUERA_DE_RANGO' ELSE N'OK' END
FROM @pru p
JOIN @num s ON s.n <= 4
CROSS APPLY (SELECT (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0 AS v) r;

/* ---------- producto terminado: un lote por orden completada ----------
   Las dos ordenes mas recientes quedan con pallets por ubicar. */
INSERT dbo.LotesProductoTerminado (NumeroLote, OrdenProduccionId, ProductoId, Cantidad, TipoEmpaque, FechaRecepcion, RecibidoPor, Estado, Observaciones)
SELECT N'LPT-' + CONVERT(NVARCHAR(8), o.Fin, 112) + N'-1' + RIGHT(N'0' + CAST(o.N AS NVARCHAR(2)), 2),
       o.Id, @p1, o.Aprob,
       CASE WHEN o.Aprob >= 30 THEN N'Pallet' ELSE N'Caja' END,
       DATEADD(MINUTE, 45, o.Fin), @alm,
       CASE WHEN o.Dias <= 2 THEN N'PorUbicar' ELSE N'Almacenado' END, NULL
FROM @ops o WHERE o.Estado = N'Completada' AND o.Aprob > 0;

-- unidades de 20 piezas (pallet) o 10 piezas (caja)
INSERT dbo.PalletsProductoTerminado (CodigoPallet, LoteId, Cantidad, FechaEtiqueta, UbicacionId, FechaUbicacion, UbicadoPor, Estado)
SELECT l.NumeroLote + N'-' + CASE l.TipoEmpaque WHEN N'Caja' THEN N'C' ELSE N'P' END + RIGHT(N'0' + CAST(n.n AS NVARCHAR(2)), 2),
       l.Id,
       CASE WHEN n.n * x.PorUnidad <= l.Cantidad THEN x.PorUnidad ELSE l.Cantidad - (n.n - 1) * x.PorUnidad END,
       DATEADD(MINUTE, 5, l.FechaRecepcion),
       CASE WHEN l.Estado = N'Almacenado' THEN (SELECT Id FROM dbo.Ubicaciones WHERE Codigo = CHOOSE((l.Id + n.n) % 4 + 1, N'PT-01', N'PT-02', N'PT-03', N'PT-04')) END,
       CASE WHEN l.Estado = N'Almacenado' THEN DATEADD(MINUTE, 25 + n.n * 15 + (l.Id % 5) * 20, l.FechaRecepcion) END,
       CASE WHEN l.Estado = N'Almacenado' THEN @alm END,
       l.Estado
FROM dbo.LotesProductoTerminado l
JOIN @ops o ON o.Id = l.OrdenProduccionId
CROSS APPLY (SELECT CASE l.TipoEmpaque WHEN N'Caja' THEN 10 ELSE 20 END AS PorUnidad) x
JOIN @num n ON n.n <= CEILING(l.Cantidad / CAST(x.PorUnidad AS FLOAT));

-- lo almacenado entra al stock del producto
UPDATE pr SET Cantidad = pr.Cantidad + x.Total
FROM dbo.Productos pr
JOIN (SELECT l.ProductoId, SUM(p.Cantidad) Total
      FROM dbo.PalletsProductoTerminado p JOIN dbo.LotesProductoTerminado l ON l.Id = p.LoteId JOIN @ops o ON o.Id = l.OrdenProduccionId
      WHERE p.Estado = N'Almacenado' GROUP BY l.ProductoId) x ON x.ProductoId = pr.Id;

/* ---------- asistencias de las ultimas dos semanas ----------
   Un registro por empleado y dia que le toca segun su asignacion de turno; ~1 de cada 12 es ausencia. */
INSERT dbo.Asistencias (EmpleadoId, Fecha, Entrada, Salida, Tipo, Observaciones)
SELECT d.EmpleadoId, d.Fecha,
       CASE WHEN d.Tipo = N'Asistencia' THEN DATEADD(MINUTE, -8 + d.Pick % 20, DATEADD(SECOND, DATEDIFF(SECOND, 0, CAST(d.HoraEntrada AS DATETIME)), d.Fecha)) END,
       CASE WHEN d.Tipo = N'Asistencia' THEN DATEADD(MINUTE, d.Pick % 15, DATEADD(SECOND, DATEDIFF(SECOND, 0, CAST(d.HoraSalida AS DATETIME)), d.Fecha)) END,
       d.Tipo,
       CASE d.Tipo WHEN N'Permiso' THEN N'Trámite personal' WHEN N'Incapacidad' THEN N'Incapacidad IMSS' END
FROM (
    SELECT a.EmpleadoId, f.Fecha, t.HoraEntrada, t.HoraSalida,
           (a.EmpleadoId * 31 + DATEDIFF(DAY, 0, f.Fecha) * 17) % 100 AS Pick,
           CASE WHEN (a.EmpleadoId * 31 + DATEDIFF(DAY, 0, f.Fecha) * 17) % 100 < 5 THEN N'Falta'
                WHEN (a.EmpleadoId * 31 + DATEDIFF(DAY, 0, f.Fecha) * 17) % 100 < 7 THEN N'Permiso'
                WHEN (a.EmpleadoId * 31 + DATEDIFF(DAY, 0, f.Fecha) * 17) % 100 < 8 THEN N'Incapacidad'
                ELSE N'Asistencia' END AS Tipo,
           ROW_NUMBER() OVER (PARTITION BY a.EmpleadoId, f.Fecha ORDER BY a.Id) AS rn
    FROM dbo.AsignacionesTurno a
    JOIN dbo.Empleados e ON e.Id = a.EmpleadoId AND e.Activo = 1
    JOIN dbo.Turnos t ON t.Id = a.TurnoId
    CROSS APPLY (SELECT DATEADD(DAY, -n.n, @hoy) AS Fecha FROM @num n WHERE n.n <= 13) f
    WHERE a.FechaInicio <= f.Fecha AND (a.FechaFin IS NULL OR a.FechaFin >= f.Fecha)
      AND N',' + a.Dias + N',' LIKE N'%,' + SUBSTRING(N'LMXJVSD', DATEPART(WEEKDAY, f.Fecha), 1) + N',%'
) d
WHERE d.rn = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.Asistencias x WHERE x.EmpleadoId = d.EmpleadoId AND x.Fecha = d.Fecha);

COMMIT;

PRINT 'Datos de Indicadores cargados.';
