using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class SolucionErrorCambiosDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialPruebasSensores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrdenProduccionId = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResultadoFinal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DuracionSegundos = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialPruebasSensores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialPruebasSensores_OrdenProduccion_OrdenProduccionId",
                        column: x => x.OrdenProduccionId,
                        principalTable: "OrdenProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetalleLecturasSensores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistorialId = table.Column<int>(type: "int", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rpm = table.Column<double>(type: "float", nullable: true),
                    Temperatura = table.Column<double>(type: "float", nullable: true),
                    Humedad = table.Column<double>(type: "float", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HistorialPruebaSensorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetalleLecturasSensores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetalleLecturasSensores_HistorialPruebasSensores_HistorialPruebaSensorId",
                        column: x => x.HistorialPruebaSensorId,
                        principalTable: "HistorialPruebasSensores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetalleLecturasSensores_HistorialPruebaSensorId",
                table: "DetalleLecturasSensores",
                column: "HistorialPruebaSensorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialPruebasSensores_OrdenProduccionId",
                table: "HistorialPruebasSensores",
                column: "OrdenProduccionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetalleLecturasSensores");

            migrationBuilder.DropTable(
                name: "HistorialPruebasSensores");
        }
    }
}
