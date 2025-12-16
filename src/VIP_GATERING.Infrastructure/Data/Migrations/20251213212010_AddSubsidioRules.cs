using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubsidioRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SubsidiaEmpleados",
                table: "Sucursales",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubsidioTipo",
                table: "Sucursales",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubsidioValor",
                table: "Sucursales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubsidiaEmpleados",
                table: "Empresas",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SubsidioTipo",
                table: "Empresas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SubsidioValor",
                table: "Empresas",
                type: "TEXT",
                nullable: false,
                defaultValue: 75m);

            migrationBuilder.AddColumn<bool>(
                name: "EsSubsidiado",
                table: "Empleados",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubsidiaEmpleados",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "SubsidioTipo",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "SubsidioValor",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "SubsidiaEmpleados",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "SubsidioTipo",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "SubsidioValor",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "EsSubsidiado",
                table: "Empleados");
        }
    }
}
