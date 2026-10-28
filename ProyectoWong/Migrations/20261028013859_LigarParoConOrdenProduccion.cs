using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class LigarParoConOrdenProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrdenProduccionId",
                table: "RegistrosDowntime",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosDowntime_OrdenProduccionId",
                table: "RegistrosDowntime",
                column: "OrdenProduccionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosDowntime_OrdenProduccion_OrdenProduccionId",
                table: "RegistrosDowntime",
                column: "OrdenProduccionId",
                principalTable: "OrdenProduccion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosDowntime_OrdenProduccion_OrdenProduccionId",
                table: "RegistrosDowntime");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosDowntime_OrdenProduccionId",
                table: "RegistrosDowntime");

            migrationBuilder.DropColumn(
                name: "OrdenProduccionId",
                table: "RegistrosDowntime");
        }
    }
}
