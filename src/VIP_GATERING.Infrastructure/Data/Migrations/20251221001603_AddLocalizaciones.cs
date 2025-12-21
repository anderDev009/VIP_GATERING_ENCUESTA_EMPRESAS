using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocalizacionEntregaId",
                table: "RespuestasFormulario",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Localizaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    SucursalId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Localizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Localizaciones_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmpleadosLocalizaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmpleadoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalizacionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpleadosLocalizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmpleadosLocalizaciones_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmpleadosLocalizaciones_Localizaciones_LocalizacionId",
                        column: x => x.LocalizacionId,
                        principalTable: "Localizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasFormulario_LocalizacionEntregaId",
                table: "RespuestasFormulario",
                column: "LocalizacionEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosLocalizaciones_EmpleadoId_LocalizacionId",
                table: "EmpleadosLocalizaciones",
                columns: new[] { "EmpleadoId", "LocalizacionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosLocalizaciones_LocalizacionId",
                table: "EmpleadosLocalizaciones",
                column: "LocalizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Localizaciones_SucursalId_Nombre",
                table: "Localizaciones",
                columns: new[] { "SucursalId", "Nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RespuestasFormulario_Localizaciones_LocalizacionEntregaId",
                table: "RespuestasFormulario",
                column: "LocalizacionEntregaId",
                principalTable: "Localizaciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RespuestasFormulario_Localizaciones_LocalizacionEntregaId",
                table: "RespuestasFormulario");

            migrationBuilder.DropTable(
                name: "EmpleadosLocalizaciones");

            migrationBuilder.DropTable(
                name: "Localizaciones");

            migrationBuilder.DropIndex(
                name: "IX_RespuestasFormulario_LocalizacionEntregaId",
                table: "RespuestasFormulario");

            migrationBuilder.DropColumn(
                name: "LocalizacionEntregaId",
                table: "RespuestasFormulario");
        }
    }
}
