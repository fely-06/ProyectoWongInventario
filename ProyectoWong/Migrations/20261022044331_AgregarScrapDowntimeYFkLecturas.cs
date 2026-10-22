using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class AgregarScrapDowntimeYFkLecturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetalleLecturasSensores_HistorialPruebasSensores_HistorialPruebaSensorId",
                table: "DetalleLecturasSensores");

            migrationBuilder.DropIndex(
                name: "IX_DetalleLecturasSensores_HistorialPruebaSensorId",
                table: "DetalleLecturasSensores");

            migrationBuilder.DropColumn(
                name: "HistorialPruebaSensorId",
                table: "DetalleLecturasSensores");

            // Las tablas de Scrap y Downtime ya pueden existir en las bases donde se corrió
            // Database/scrap_downtime.sql a mano, por eso se crean solo si faltan.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Maquinas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Maquinas (
        Id      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Maquinas PRIMARY KEY,
        Codigo  NVARCHAR(20)  NOT NULL,
        Nombre  NVARCHAR(100) NOT NULL,
        Activa  BIT           NOT NULL
    );
END");

            migrationBuilder.Sql(@"
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
END");

            migrationBuilder.Sql(@"
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
END");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleLecturasSensores_HistorialId",
                table: "DetalleLecturasSensores",
                column: "HistorialId");

            // Lecturas huérfanas (sin historial) impedirían crear la llave foránea
            migrationBuilder.Sql(@"
DELETE FROM dbo.DetalleLecturasSensores
WHERE HistorialId NOT IN (SELECT Id FROM dbo.HistorialPruebasSensores);");

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleLecturasSensores_HistorialPruebasSensores_HistorialId",
                table: "DetalleLecturasSensores",
                column: "HistorialId",
                principalTable: "HistorialPruebasSensores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetalleLecturasSensores_HistorialPruebasSensores_HistorialId",
                table: "DetalleLecturasSensores");

            migrationBuilder.DropTable(
                name: "RegistrosDowntime");

            migrationBuilder.DropTable(
                name: "RegistrosScrap");

            migrationBuilder.DropTable(
                name: "Maquinas");

            migrationBuilder.DropIndex(
                name: "IX_DetalleLecturasSensores_HistorialId",
                table: "DetalleLecturasSensores");

            migrationBuilder.AddColumn<int>(
                name: "HistorialPruebaSensorId",
                table: "DetalleLecturasSensores",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetalleLecturasSensores_HistorialPruebaSensorId",
                table: "DetalleLecturasSensores",
                column: "HistorialPruebaSensorId");

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleLecturasSensores_HistorialPruebasSensores_HistorialPruebaSensorId",
                table: "DetalleLecturasSensores",
                column: "HistorialPruebaSensorId",
                principalTable: "HistorialPruebasSensores",
                principalColumn: "Id");
        }
    }
}
