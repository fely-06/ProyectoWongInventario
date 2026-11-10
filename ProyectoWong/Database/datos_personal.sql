/* Datos de prueba del modulo de Personal.
   Correr en SSMS sobre la base del proyecto (MONITOREO).
   Las fechas se calculan a partir del dia en que se corre. */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Empleados', N'U') IS NULL
BEGIN
    RAISERROR(N'No existe la tabla Empleados, falta correr "dotnet ef database update".', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM dbo.Empleados WHERE NumeroEmpleado = N'1001')
BEGIN
    PRINT 'Los datos de Personal ya estaban cargados.';
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
DECLARE @ahora DATETIME2 = SYSDATETIME();

-- turnos
IF NOT EXISTS (SELECT 1 FROM dbo.Turnos WHERE Nombre = N'Matutino')
    INSERT dbo.Turnos (Nombre, HoraEntrada, HoraSalida) VALUES (N'Matutino', '06:00', '14:00');
IF NOT EXISTS (SELECT 1 FROM dbo.Turnos WHERE Nombre = N'Vespertino')
    INSERT dbo.Turnos (Nombre, HoraEntrada, HoraSalida) VALUES (N'Vespertino', '14:00', '22:00');
IF NOT EXISTS (SELECT 1 FROM dbo.Turnos WHERE Nombre = N'Nocturno')
    INSERT dbo.Turnos (Nombre, HoraEntrada, HoraSalida) VALUES (N'Nocturno', '22:00', '06:00');
IF NOT EXISTS (SELECT 1 FROM dbo.Turnos WHERE Nombre = N'Fin de semana')
    INSERT dbo.Turnos (Nombre, HoraEntrada, HoraSalida) VALUES (N'Fin de semana', '07:00', '19:00');

DECLARE @emp TABLE (
    Num NVARCHAR(20), Nombre NVARCHAR(100), Puesto NVARCHAR(30), Activo BIT,
    Turno NVARCHAR(50), Maquina NVARCHAR(20), Linea NVARCHAR(50), Dias NVARCHAR(20),
    Inicio INT,                         -- dias atras que empezo su asignacion actual
    TurnoAnt NVARCHAR(50) NULL          -- si cambio de turno, el que tenia antes
);

INSERT @emp VALUES
 (N'1001', N'José Luis Ramírez Ochoa',      N'Supervisor',  1, N'Matutino',      NULL,     N'Línea 1', N'L,M,X,J,V',     120, NULL),
 (N'1002', N'María Fernanda López Castro',  N'Operador',    1, N'Matutino',      N'MAQ-01', N'Línea 1', N'L,M,X,J,V',     90,  NULL),
 (N'1003', N'Juan Carlos Valenzuela Ruiz',  N'Operador',    1, N'Matutino',      N'MAQ-02', N'Línea 1', N'L,M,X,J,V',     25,  N'Vespertino'),
 (N'1004', N'Ana Sofía Moreno Quintero',    N'Operador',    1, N'Matutino',      N'MAQ-03', N'Línea 1', N'L,M,X,J,V',     75,  NULL),
 (N'1005', N'Luis Alberto Gámez Soto',      N'Técnico',     1, N'Matutino',      N'MAQ-05', N'Línea 1', N'L,M,X,J,V',     60,  NULL),
 (N'1006', N'Karla Yesenia Duarte León',    N'Almacenista', 1, N'Matutino',      NULL,     NULL,       N'L,M,X,J,V,S',   100, NULL),
 (N'1007', N'Ricardo Méndez Acosta',        N'Supervisor',  1, N'Vespertino',    NULL,     N'Línea 2', N'L,M,X,J,V',     120, NULL),
 (N'1008', N'Daniela Ibarra Félix',         N'Operador',    1, N'Vespertino',    N'MAQ-04', N'Línea 2', N'L,M,X,J,V',     80,  NULL),
 (N'1009', N'Óscar Iván Torres Leyva',      N'Operador',    1, N'Vespertino',    N'MAQ-06', N'Línea 2', N'L,M,X,J,V',     30,  N'Nocturno'),
 (N'1010', N'Paola Guadalupe Romero Islas', N'Operador',    1, N'Vespertino',    N'MAQ-01', N'Línea 2', N'L,M,X,J,V',     70,  NULL),
 (N'1011', N'Miguel Ángel Corrales Peña',   N'Técnico',     1, N'Nocturno',      N'MAQ-05', N'Línea 2', N'L,M,X,J,V',     95,  NULL),
 (N'1012', N'Brenda Lizeth A. Navarro',     N'Operador',    1, N'Nocturno',      N'MAQ-02', N'Línea 2', N'L,M,X,J,V',     50,  NULL),
 (N'1013', N'Héctor Manuel Salazar Cota',   N'Operador',    1, N'Fin de semana', N'MAQ-03', N'Línea 1', N'S,D',           85,  NULL),
 (N'1014', N'Jesús Alonso Bojórquez Lugo',  N'Operador',    0, N'Matutino',      N'MAQ-04', N'Línea 1', N'L,M,X,J,V',     150, NULL);

INSERT dbo.Empleados (NumeroEmpleado, Nombre, Puesto, Activo)
SELECT Num, Nombre, Puesto, Activo FROM @emp;

-- asignaciones anteriores (cambios de turno) y la del empleado dado de baja
INSERT dbo.AsignacionesTurno (EmpleadoId, TurnoId, MaquinaId, Linea, Dias, FechaInicio, FechaFin)
SELECT e.Id, t.Id, m.Id, x.Linea, x.Dias, DATEADD(DAY, -120, @hoy), DATEADD(DAY, -x.Inicio, @hoy)
FROM @emp x
JOIN dbo.Empleados e ON e.NumeroEmpleado = x.Num
JOIN dbo.Turnos t ON t.Nombre = x.TurnoAnt
LEFT JOIN dbo.Maquinas m ON m.Codigo = x.Maquina
WHERE x.TurnoAnt IS NOT NULL;

INSERT dbo.AsignacionesTurno (EmpleadoId, TurnoId, MaquinaId, Linea, Dias, FechaInicio, FechaFin)
SELECT e.Id, t.Id, m.Id, x.Linea, x.Dias, DATEADD(DAY, -x.Inicio, @hoy),
       CASE WHEN x.Activo = 0 THEN DATEADD(DAY, -10, @hoy) END
FROM @emp x
JOIN dbo.Empleados e ON e.NumeroEmpleado = x.Num
JOIN dbo.Turnos t ON t.Nombre = x.Turno
LEFT JOIN dbo.Maquinas m ON m.Codigo = x.Maquina;

-- asistencia de las ultimas 3 semanas (sin contar hoy)
;WITH dias AS (
    SELECT TOP (21) DATEADD(DAY, -ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), @hoy) AS Fecha
    FROM sys.objects
),
programados AS (
    SELECT e.Id AS EmpleadoId, x.Num, x.Activo, d.Fecha, t.HoraEntrada, t.HoraSalida,
           ABS(CHECKSUM(x.Num, d.Fecha)) % 100 AS r,
           ABS(CHECKSUM(d.Fecha, x.Nombre)) % 23 AS minutos
    FROM @emp x
    JOIN dbo.Empleados e ON e.NumeroEmpleado = x.Num
    JOIN dbo.Turnos t ON t.Nombre = x.Turno
    CROSS JOIN dias d
    WHERE CHARINDEX(SUBSTRING(N'DLMXJVS', DATEDIFF(DAY, '19000107', d.Fecha) % 7 + 1, 1), x.Dias) > 0
      AND (x.Activo = 1 OR d.Fecha < DATEADD(DAY, -10, @hoy))
)
INSERT dbo.Asistencias (EmpleadoId, Fecha, Entrada, Salida, Tipo, Observaciones)
SELECT EmpleadoId, Fecha,
       CASE WHEN tipo = N'Asistencia' THEN entrada END,
       CASE WHEN tipo = N'Asistencia' THEN DATEADD(MINUTE, 480 + minutos % 15, entrada) END,
       tipo, obs
FROM (
    SELECT p.*,
           DATEADD(MINUTE, p.minutos - 10, DATEADD(SECOND, DATEDIFF(SECOND, '00:00', p.HoraEntrada), CAST(p.Fecha AS DATETIME2))) AS entrada,
           CASE
               WHEN p.Num = N'1004' AND p.Fecha >= DATEADD(DAY, -12, @hoy) AND p.Fecha <= DATEADD(DAY, -8, @hoy) THEN N'Vacaciones'
               WHEN p.Num = N'1010' AND p.Fecha >= DATEADD(DAY, -18, @hoy) AND p.Fecha <= DATEADD(DAY, -16, @hoy) THEN N'Incapacidad'
               WHEN p.r < 5 THEN N'Falta'
               WHEN p.r < 8 THEN N'Permiso'
               ELSE N'Asistencia'
           END AS tipo,
           CASE
               WHEN p.Num = N'1004' AND p.Fecha >= DATEADD(DAY, -12, @hoy) AND p.Fecha <= DATEADD(DAY, -8, @hoy) THEN N'Vacaciones de fin de año adelantadas'
               WHEN p.Num = N'1010' AND p.Fecha >= DATEADD(DAY, -18, @hoy) AND p.Fecha <= DATEADD(DAY, -16, @hoy) THEN N'Incapacidad IMSS por infección'
               WHEN p.r < 5 THEN CASE p.r % 2 WHEN 0 THEN NULL ELSE N'No avisó' END
               WHEN p.r < 8 THEN CASE p.r % 3 WHEN 0 THEN N'Cita médica' WHEN 1 THEN N'Trámite personal' ELSE N'Asunto familiar' END
           END AS obs
    FROM programados p
) a;

-- hoy: los que ya entraron segun la hora, y un par de ausencias
DECLARE @letraHoy NCHAR(1) = SUBSTRING(N'DLMXJVS', DATEDIFF(DAY, '19000107', @hoy) % 7 + 1, 1);

INSERT dbo.Asistencias (EmpleadoId, Fecha, Entrada, Salida, Tipo, Observaciones)
SELECT e.Id, @hoy,
       CASE WHEN x.Num IN (N'1003', N'1009') THEN NULL ELSE ent.Entrada END,
       CASE WHEN x.Num IN (N'1003', N'1009') THEN NULL
            WHEN t.HoraSalida > t.HoraEntrada AND DATEADD(MINUTE, 480, ent.Entrada) <= @ahora THEN DATEADD(MINUTE, 480 + ABS(CHECKSUM(x.Num)) % 12, ent.Entrada) END,
       CASE x.Num WHEN N'1003' THEN N'Permiso' WHEN N'1009' THEN N'Falta' ELSE N'Asistencia' END,
       CASE x.Num WHEN N'1003' THEN N'Cita en el SAT' END
FROM @emp x
JOIN dbo.Empleados e ON e.NumeroEmpleado = x.Num
JOIN dbo.Turnos t ON t.Nombre = x.Turno
CROSS APPLY (SELECT DATEADD(MINUTE, ABS(CHECKSUM(x.Nombre)) % 15 - 8, DATEADD(SECOND, DATEDIFF(SECOND, '00:00', t.HoraEntrada), CAST(@hoy AS DATETIME2))) AS Entrada) ent
WHERE x.Activo = 1
  AND CHARINDEX(@letraHoy, x.Dias) > 0
  AND (x.Num IN (N'1003', N'1009') OR ent.Entrada <= @ahora);

COMMIT;

PRINT 'Datos de Personal cargados.';
