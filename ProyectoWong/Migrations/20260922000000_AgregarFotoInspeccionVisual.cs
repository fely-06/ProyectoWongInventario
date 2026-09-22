using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFotoInspeccionVisual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "FotoInspeccion",
                table: "OrdenProduccion",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFotoInspeccion",
                table: "OrdenProduccion",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoInspeccion",
                table: "OrdenProduccion");

            migrationBuilder.DropColumn(
                name: "FechaFotoInspeccion",
                table: "OrdenProduccion");
        }
    }
}
