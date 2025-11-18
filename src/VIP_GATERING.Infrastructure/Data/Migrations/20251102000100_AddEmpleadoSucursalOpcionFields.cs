using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20251102000100_AddEmpleadoSucursalOpcionFields")]
    public partial class AddEmpleadoSucursalOpcionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "Empleados",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Estado",
                table: "Empleados",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Sucursales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Precio",
                table: "Opciones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsSubsidiado",
                table: "Opciones",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LlevaItbis",
                table: "Opciones",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "Precio",
                table: "Opciones");

            migrationBuilder.DropColumn(
                name: "EsSubsidiado",
                table: "Opciones");

            migrationBuilder.DropColumn(
                name: "LlevaItbis",
                table: "Opciones");
        }
    }
}
