using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MenuScopeSucursalEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Menus",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SucursalId",
                table: "Menus",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Menus_EmpresaId",
                table: "Menus",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_SucursalId",
                table: "Menus",
                column: "SucursalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Menus_Empresas_EmpresaId",
                table: "Menus",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Menus_Sucursales_SucursalId",
                table: "Menus",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Menus_Empresas_EmpresaId",
                table: "Menus");

            migrationBuilder.DropForeignKey(
                name: "FK_Menus_Sucursales_SucursalId",
                table: "Menus");

            migrationBuilder.DropIndex(
                name: "IX_Menus_EmpresaId",
                table: "Menus");

            migrationBuilder.DropIndex(
                name: "IX_Menus_SucursalId",
                table: "Menus");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Menus");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Menus");
        }
    }
}
