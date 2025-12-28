using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizacionEmpresaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Localizaciones",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Localizaciones_EmpresaId",
                table: "Localizaciones",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Localizaciones_Empresas_EmpresaId",
                table: "Localizaciones",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Localizaciones_Empresas_EmpresaId",
                table: "Localizaciones");

            migrationBuilder.DropIndex(
                name: "IX_Localizaciones_EmpresaId",
                table: "Localizaciones");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Localizaciones");
        }
    }
}
