using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class CamposSimulacionDos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaPausa",
                table: "OrdenProduccion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TiempoPausadoMinutos",
                table: "OrdenProduccion",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaPausa",
                table: "OrdenProduccion");

            migrationBuilder.DropColumn(
                name: "TiempoPausadoMinutos",
                table: "OrdenProduccion");
        }
    }
}
