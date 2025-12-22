using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContactAndLocationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Localizaciones",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IndicacionesEntrega",
                table: "Localizaciones",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactoNombre",
                table: "Empresas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactoTelefono",
                table: "Empresas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Empresas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Localizaciones");

            migrationBuilder.DropColumn(
                name: "IndicacionesEntrega",
                table: "Localizaciones");

            migrationBuilder.DropColumn(
                name: "ContactoNombre",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "ContactoTelefono",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Empresas");
        }
    }
}
