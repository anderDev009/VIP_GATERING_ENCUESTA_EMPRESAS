using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VIP_GATERING.Infrastructure.Data;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251217192000_AddEmpleadoSucursalesEntregaYAdicionales")]
    public partial class AddEmpleadoSucursalesEntregaYAdicionales : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmpleadosSucursales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmpleadoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SucursalId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpleadosSucursales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmpleadosSucursales_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmpleadosSucursales_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosSucursales_EmpleadoId_SucursalId",
                table: "EmpleadosSucursales",
                columns: new[] { "EmpleadoId", "SucursalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosSucursales_SucursalId",
                table: "EmpleadosSucursales",
                column: "SucursalId");

            migrationBuilder.CreateTable(
                name: "MenusAdicionales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MenuId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpcionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenusAdicionales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenusAdicionales_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenusAdicionales_Opciones_OpcionId",
                        column: x => x.OpcionId,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenusAdicionales_MenuId_OpcionId",
                table: "MenusAdicionales",
                columns: new[] { "MenuId", "OpcionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenusAdicionales_OpcionId",
                table: "MenusAdicionales",
                column: "OpcionId");

            migrationBuilder.AddColumn<Guid>(
                name: "SucursalEntregaId",
                table: "RespuestasFormulario",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "AdicionalOpcionId",
                table: "RespuestasFormulario",
                type: "TEXT",
                nullable: true);

            // Backfill: si ya existían respuestas, las asignamos a la sucursal principal del empleado
            migrationBuilder.Sql(
                "UPDATE RespuestasFormulario SET SucursalEntregaId = (SELECT SucursalId FROM Empleados WHERE Empleados.Id = RespuestasFormulario.EmpleadoId)");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasFormulario_AdicionalOpcionId",
                table: "RespuestasFormulario",
                column: "AdicionalOpcionId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasFormulario_SucursalEntregaId",
                table: "RespuestasFormulario",
                column: "SucursalEntregaId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmpleadosSucursales");

            migrationBuilder.DropTable(
                name: "MenusAdicionales");

            migrationBuilder.DropIndex(
                name: "IX_RespuestasFormulario_AdicionalOpcionId",
                table: "RespuestasFormulario");

            migrationBuilder.DropIndex(
                name: "IX_RespuestasFormulario_SucursalEntregaId",
                table: "RespuestasFormulario");

            migrationBuilder.DropColumn(
                name: "AdicionalOpcionId",
                table: "RespuestasFormulario");

            migrationBuilder.DropColumn(
                name: "SucursalEntregaId",
                table: "RespuestasFormulario");
        }
    }
}
