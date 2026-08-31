using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoWong.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFlujoRecepcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Componentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroPieza = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    MinimoInventario = table.Column<int>(type: "int", nullable: false),
                    MaximoInventario = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Proveedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnidadMedida = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NumeroLote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FechaCaducidad = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NumeroSerie = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ubicacion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Imagen = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Componentes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrdenesCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroOC = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    Proveedor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaEsperada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenesCompra", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ubicaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Zona = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CapacidadMaxima = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ubicaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    ExpUsuarioId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreCompleto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.ExpUsuarioId);
                });

            migrationBuilder.CreateTable(
                name: "OrdenCompraDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrdenCmpraId = table.Column<int>(type: "int", nullable: false),
                    ComponenteId = table.Column<int>(type: "int", nullable: false),
                    CantidadEsperada = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenCompraDetalle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdenCompraDetalle_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrdenCompraDetalle_OrdenesCompra_OrdenCmpraId",
                        column: x => x.OrdenCmpraId,
                        principalTable: "OrdenesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recepciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrdenCompraId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    FechaRecepcion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recepciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recepciones_OrdenesCompra_OrdenCompraId",
                        column: x => x.OrdenCompraId,
                        principalTable: "OrdenesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recepciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "ExpUsuarioId");
                });

            migrationBuilder.CreateTable(
                name: "RecepcionDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecepcionId = table.Column<int>(type: "int", nullable: false),
                    ComponenteId = table.Column<int>(type: "int", nullable: false),
                    NumeroLote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaCaducidad = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CantidadRecibida = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecepcionDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecepcionDetalles_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecepcionDetalles_Recepciones_RecepcionId",
                        column: x => x.RecepcionId,
                        principalTable: "Recepciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspeccionesQA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecepcionDetalleId = table.Column<int>(type: "int", nullable: false),
                    EmpaqueOk = table.Column<bool>(type: "bit", nullable: false),
                    EtiquetaOk = table.Column<bool>(type: "bit", nullable: false),
                    MaterialOk = table.Column<bool>(type: "bit", nullable: false),
                    CertificadoDisponible = table.Column<bool>(type: "bit", nullable: false),
                    InspeccionadoPor = table.Column<int>(type: "int", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comentarios = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaInspeccion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspeccionesQA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspeccionesQA_RecepcionDetalles_RecepcionDetalleId",
                        column: x => x.RecepcionDetalleId,
                        principalTable: "RecepcionDetalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspeccionesQA_Usuarios_InspeccionadoPor",
                        column: x => x.InspeccionadoPor,
                        principalTable: "Usuarios",
                        principalColumn: "ExpUsuarioId");
                });

            migrationBuilder.CreateTable(
                name: "Pallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecepcionDetalleId = table.Column<int>(type: "int", nullable: false),
                    CodigoPallet = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FechaImpresionEtiqueta = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pallets_RecepcionDetalles_RecepcionDetalleId",
                        column: x => x.RecepcionDetalleId,
                        principalTable: "RecepcionDetalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PalletId = table.Column<int>(type: "int", nullable: false),
                    UbicacionId = table.Column<int>(type: "int", nullable: false),
                    TipoMovimiento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FechaMovimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RealizadoPor = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalTable: "Pallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Ubicaciones_UbicacionId",
                        column: x => x.UbicacionId,
                        principalTable: "Ubicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Usuarios_RealizadoPor",
                        column: x => x.RealizadoPor,
                        principalTable: "Usuarios",
                        principalColumn: "ExpUsuarioId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspeccionesQA_InspeccionadoPor",
                table: "InspeccionesQA",
                column: "InspeccionadoPor");

            migrationBuilder.CreateIndex(
                name: "IX_InspeccionesQA_RecepcionDetalleId",
                table: "InspeccionesQA",
                column: "RecepcionDetalleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_PalletId",
                table: "MovimientosInventario",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_RealizadoPor",
                table: "MovimientosInventario",
                column: "RealizadoPor");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_UbicacionId",
                table: "MovimientosInventario",
                column: "UbicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenCompraDetalle_ComponenteId",
                table: "OrdenCompraDetalle",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenCompraDetalle_OrdenCmpraId",
                table: "OrdenCompraDetalle",
                column: "OrdenCmpraId");

            migrationBuilder.CreateIndex(
                name: "IX_Pallets_RecepcionDetalleId",
                table: "Pallets",
                column: "RecepcionDetalleId");

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionDetalles_ComponenteId",
                table: "RecepcionDetalles",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionDetalles_RecepcionId",
                table: "RecepcionDetalles",
                column: "RecepcionId");

            migrationBuilder.CreateIndex(
                name: "IX_Recepciones_OrdenCompraId",
                table: "Recepciones",
                column: "OrdenCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_Recepciones_UsuarioId",
                table: "Recepciones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspeccionesQA");

            migrationBuilder.DropTable(
                name: "MovimientosInventario");

            migrationBuilder.DropTable(
                name: "OrdenCompraDetalle");

            migrationBuilder.DropTable(
                name: "Pallets");

            migrationBuilder.DropTable(
                name: "Ubicaciones");

            migrationBuilder.DropTable(
                name: "RecepcionDetalles");

            migrationBuilder.DropTable(
                name: "Componentes");

            migrationBuilder.DropTable(
                name: "Recepciones");

            migrationBuilder.DropTable(
                name: "OrdenesCompra");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
