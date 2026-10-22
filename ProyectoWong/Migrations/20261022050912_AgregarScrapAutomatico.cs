using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class AgregarScrapAutomatico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Automatico",
                table: "RegistrosScrap",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Automatico",
                table: "RegistrosScrap");
        }
    }
}
