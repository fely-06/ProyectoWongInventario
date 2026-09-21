using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposInspeccionVisual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CantidadAprobada",
                table: "OrdenProduccion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CantidadRechazada",
                table: "OrdenProduccion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesInspeccion",
                table: "OrdenProduccion",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CantidadAprobada",
                table: "OrdenProduccion");

            migrationBuilder.DropColumn(
                name: "CantidadRechazada",
                table: "OrdenProduccion");

            migrationBuilder.DropColumn(
                name: "ObservacionesInspeccion",
                table: "OrdenProduccion");
        }
    }
}
