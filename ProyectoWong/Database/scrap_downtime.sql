-- Tablas para los módulos Scrap y Downtime.
-- Ejecutar una sola vez en SSMS sobre la base de datos del proyecto (MONITOREO).
-- Es seguro correrlo más de una vez: solo crea lo que falta.

IF OBJECT_ID(N'dbo.Maquinas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Maquinas (
        Id      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Maquinas PRIMARY KEY,
        Codigo  NVARCHAR(20)  NOT NULL,
        Nombre  NVARCHAR(100) NOT NULL,
        Activa  BIT           NOT NULL
    );
END
GO

IF OBJECT_ID(N'dbo.RegistrosScrap', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosScrap (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrosScrap PRIMARY KEY,
        OrdenProduccionId INT           NOT NULL,
        MaquinaId         INT           NOT NULL,
        MotivoRechazo     NVARCHAR(100) NOT NULL,
        Cantidad          INT           NOT NULL,
        Fecha             DATETIME2     NOT NULL,
        Observaciones     NVARCHAR(300) NULL,
        CONSTRAINT FK_RegistrosScrap_OrdenProduccion_OrdenProduccionId
            FOREIGN KEY (OrdenProduccionId) REFERENCES dbo.OrdenProduccion (Id),
        CONSTRAINT FK_RegistrosScrap_Maquinas_MaquinaId
            FOREIGN KEY (MaquinaId) REFERENCES dbo.Maquinas (Id)
    );
    CREATE INDEX IX_RegistrosScrap_OrdenProduccionId ON dbo.RegistrosScrap (OrdenProduccionId);
    CREATE INDEX IX_RegistrosScrap_MaquinaId ON dbo.RegistrosScrap (MaquinaId);
END
GO

IF OBJECT_ID(N'dbo.RegistrosDowntime', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosDowntime (
        Id                      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrosDowntime PRIMARY KEY,
        MaquinaId               INT           NOT NULL,
        Causa                   NVARCHAR(100) NOT NULL,
        HoraInicio              DATETIME2     NOT NULL,
        HoraFin                 DATETIME2     NULL,
        DuracionMinutos         FLOAT         NOT NULL,
        DuracionEstimadaMinutos FLOAT         NOT NULL,
        Estado                  NVARCHAR(20)  NOT NULL,
        Simulado                BIT           NOT NULL,
        Observaciones           NVARCHAR(300) NULL,
        CONSTRAINT FK_RegistrosDowntime_Maquinas_MaquinaId
            FOREIGN KEY (MaquinaId) REFERENCES dbo.Maquinas (Id)
    );
    CREATE INDEX IX_RegistrosDowntime_MaquinaId ON dbo.RegistrosDowntime (MaquinaId);
END
GO
