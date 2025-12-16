using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpcionesExtendidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OpcionIdD",
                table: "OpcionesMenu",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpcionIdE",
                table: "OpcionesMenu",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpcionesMaximas",
                table: "OpcionesMenu",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_OpcionIdD",
                table: "OpcionesMenu",
                column: "OpcionIdD");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_OpcionIdE",
                table: "OpcionesMenu",
                column: "OpcionIdE");

            migrationBuilder.AddForeignKey(
                name: "FK_OpcionesMenu_Opciones_OpcionIdD",
                table: "OpcionesMenu",
                column: "OpcionIdD",
                principalTable: "Opciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OpcionesMenu_Opciones_OpcionIdE",
                table: "OpcionesMenu",
                column: "OpcionIdE",
                principalTable: "Opciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpcionesMenu_Opciones_OpcionIdD",
                table: "OpcionesMenu");

            migrationBuilder.DropForeignKey(
                name: "FK_OpcionesMenu_Opciones_OpcionIdE",
                table: "OpcionesMenu");

            migrationBuilder.DropIndex(
                name: "IX_OpcionesMenu_OpcionIdD",
                table: "OpcionesMenu");

            migrationBuilder.DropIndex(
                name: "IX_OpcionesMenu_OpcionIdE",
                table: "OpcionesMenu");

            migrationBuilder.DropColumn(
                name: "OpcionIdD",
                table: "OpcionesMenu");

            migrationBuilder.DropColumn(
                name: "OpcionIdE",
                table: "OpcionesMenu");

            migrationBuilder.DropColumn(
                name: "OpcionesMaximas",
                table: "OpcionesMenu");
        }
    }
}
