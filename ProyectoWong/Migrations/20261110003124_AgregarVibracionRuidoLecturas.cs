using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVibracionRuidoLecturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Ruido",
                table: "DetalleLecturasSensores",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Vibracion",
                table: "DetalleLecturasSensores",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ruido",
                table: "DetalleLecturasSensores");

            migrationBuilder.DropColumn(
                name: "Vibracion",
                table: "DetalleLecturasSensores");
        }
    }
}
