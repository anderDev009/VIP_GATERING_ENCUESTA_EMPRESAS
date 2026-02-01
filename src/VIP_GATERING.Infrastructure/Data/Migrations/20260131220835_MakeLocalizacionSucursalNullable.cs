using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeLocalizacionSucursalNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Localizaciones_Sucursales_SucursalId",
                table: "Localizaciones");

            migrationBuilder.DropIndex(
                name: "IX_Localizaciones_EmpresaId",
                table: "Localizaciones");

            migrationBuilder.DropIndex(
                name: "IX_Localizaciones_SucursalId_Nombre",
                table: "Localizaciones");

            migrationBuilder.AlterColumn<int>(
                name: "SucursalId",
                table: "Localizaciones",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql(
                "DELETE FROM \"Localizaciones\" a USING \"Localizaciones\" b " +
                "WHERE a.\"EmpresaId\" = b.\"EmpresaId\" " +
                "AND a.\"Nombre\" = b.\"Nombre\" " +
                "AND a.\"Id\" > b.\"Id\";");

            migrationBuilder.CreateIndex(
                name: "IX_Localizaciones_EmpresaId_Nombre",
                table: "Localizaciones",
                columns: new[] { "EmpresaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Localizaciones_SucursalId",
                table: "Localizaciones",
                column: "SucursalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Localizaciones_Sucursales_SucursalId",
                table: "Localizaciones",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Localizaciones_Sucursales_SucursalId",
                table: "Localizaciones");

            migrationBuilder.DropIndex(
                name: "IX_Localizaciones_EmpresaId_Nombre",
                table: "Localizaciones");

            migrationBuilder.DropIndex(
                name: "IX_Localizaciones_SucursalId",
                table: "Localizaciones");

            migrationBuilder.AlterColumn<int>(
                name: "SucursalId",
                table: "Localizaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Localizaciones_EmpresaId",
                table: "Localizaciones",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Localizaciones_SucursalId_Nombre",
                table: "Localizaciones",
                columns: new[] { "SucursalId", "Nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Localizaciones_Sucursales_SucursalId",
                table: "Localizaciones",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
