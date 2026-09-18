using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class SincronizarModelo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "FK_MovimientosInventario_Ubicaciones_UbicacionId1",
            //    table: "MovimientosInventario");

            //migrationBuilder.DropIndex(
            //    name: "IX_MovimientosInventario_UbicacionId1",
            //    table: "MovimientosInventario");

            //migrationBuilder.DropColumn(
            //    name: "UbicacionId1",
            //    table: "MovimientosInventario");

            //migrationBuilder.CreateTable(
            //    name: "OrdenProduccion",
            //    columns: table => new
            //    {
            //        Id = table.Column<int>(type: "int", nullable: false)
            //            .Annotation("SqlServer:Identity", "1, 1"),
            //        NumeroOP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
            //        ProductoId = table.Column<int>(type: "int", nullable: false),
            //        CantidadAProducir = table.Column<int>(type: "int", nullable: false),
            //        Estado = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        OrdenCompraId = table.Column<int>(type: "int", nullable: true),
            //        FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
            //        FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
            //        FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_OrdenProduccion", x => x.Id);
            //        table.ForeignKey(
            //            name: "FK_OrdenProduccion_OrdenesCompra_OrdenCompraId",
            //            column: x => x.OrdenCompraId,
            //            principalTable: "OrdenesCompra",
            //            principalColumn: "Id");
            //        table.ForeignKey(
            //            name: "FK_OrdenProduccion_Productos_ProductoId",
            //            column: x => x.ProductoId,
            //            principalTable: "Productos",
            //            principalColumn: "Id",
            //            onDelete: ReferentialAction.Cascade);
            //    });

            //migrationBuilder.CreateTable(
            //    name: "OrdenProduccionDetalle",
            //    columns: table => new
            //    {
            //        Id = table.Column<int>(type: "int", nullable: false)
            //            .Annotation("SqlServer:Identity", "1, 1"),
            //        OrdenProduccionId = table.Column<int>(type: "int", nullable: false),
            //        ComponenteId = table.Column<int>(type: "int", nullable: false),
            //        CantidadRequerida = table.Column<int>(type: "int", nullable: false),
            //        CantidadConsumida = table.Column<int>(type: "int", nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_OrdenProduccionDetalle", x => x.Id);
            //        table.ForeignKey(
            //            name: "FK_OrdenProduccionDetalle_Componentes_ComponenteId",
            //            column: x => x.ComponenteId,
            //            principalTable: "Componentes",
            //            principalColumn: "Id",
            //            onDelete: ReferentialAction.Cascade);
            //        table.ForeignKey(
            //            name: "FK_OrdenProduccionDetalle_OrdenProduccion_OrdenProduccionId",
            //            column: x => x.OrdenProduccionId,
            //            principalTable: "OrdenProduccion",
            //            principalColumn: "Id",
            //            onDelete: ReferentialAction.Cascade);
            //    });

            //migrationBuilder.CreateIndex(
            //    name: "IX_OrdenProduccion_OrdenCompraId",
            //    table: "OrdenProduccion",
            //    column: "OrdenCompraId");

            //migrationBuilder.CreateIndex(
            //    name: "IX_OrdenProduccion_ProductoId",
            //    table: "OrdenProduccion",
            //    column: "ProductoId");

            //migrationBuilder.CreateIndex(
            //    name: "IX_OrdenProduccionDetalle_ComponenteId",
            //    table: "OrdenProduccionDetalle",
            //    column: "ComponenteId");

            //migrationBuilder.CreateIndex(
            //    name: "IX_OrdenProduccionDetalle_OrdenProduccionId",
            //    table: "OrdenProduccionDetalle",
            //    column: "OrdenProduccionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdenProduccionDetalle");

            migrationBuilder.DropTable(
                name: "OrdenProduccion");

            migrationBuilder.AddColumn<int>(
                name: "UbicacionId1",
                table: "MovimientosInventario",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_UbicacionId1",
                table: "MovimientosInventario",
                column: "UbicacionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosInventario_Ubicaciones_UbicacionId1",
                table: "MovimientosInventario",
                column: "UbicacionId1",
                principalTable: "Ubicaciones",
                principalColumn: "Id");
        }
    }
}
