using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAlmacenProductoTerminado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotesProductoTerminado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroLote = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OrdenProduccionId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    TipoEmpaque = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FechaRecepcion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecibidoPor = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesProductoTerminado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotesProductoTerminado_OrdenProduccion_OrdenProduccionId",
                        column: x => x.OrdenProduccionId,
                        principalTable: "OrdenProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesProductoTerminado_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesProductoTerminado_Usuarios_RecibidoPor",
                        column: x => x.RecibidoPor,
                        principalTable: "Usuarios",
                        principalColumn: "ExpUsuarioId");
                });

            migrationBuilder.CreateTable(
                name: "PalletsProductoTerminado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoPallet = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    FechaEtiqueta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UbicacionId = table.Column<int>(type: "int", nullable: true),
                    FechaUbicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UbicadoPor = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PalletsProductoTerminado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PalletsProductoTerminado_LotesProductoTerminado_LoteId",
                        column: x => x.LoteId,
                        principalTable: "LotesProductoTerminado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PalletsProductoTerminado_Ubicaciones_UbicacionId",
                        column: x => x.UbicacionId,
                        principalTable: "Ubicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PalletsProductoTerminado_Usuarios_UbicadoPor",
                        column: x => x.UbicadoPor,
                        principalTable: "Usuarios",
                        principalColumn: "ExpUsuarioId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotesProductoTerminado_NumeroLote",
                table: "LotesProductoTerminado",
                column: "NumeroLote",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesProductoTerminado_OrdenProduccionId",
                table: "LotesProductoTerminado",
                column: "OrdenProduccionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesProductoTerminado_ProductoId",
                table: "LotesProductoTerminado",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesProductoTerminado_RecibidoPor",
                table: "LotesProductoTerminado",
                column: "RecibidoPor");

            migrationBuilder.CreateIndex(
                name: "IX_PalletsProductoTerminado_CodigoPallet",
                table: "PalletsProductoTerminado",
                column: "CodigoPallet",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PalletsProductoTerminado_LoteId",
                table: "PalletsProductoTerminado",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PalletsProductoTerminado_UbicacionId",
                table: "PalletsProductoTerminado",
                column: "UbicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_PalletsProductoTerminado_UbicadoPor",
                table: "PalletsProductoTerminado",
                column: "UbicadoPor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PalletsProductoTerminado");

            migrationBuilder.DropTable(
                name: "LotesProductoTerminado");
        }
    }
}
