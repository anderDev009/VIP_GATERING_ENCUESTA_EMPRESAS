using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    public partial class AddCierreFacturacionCampos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CierreNomina",
                table: "RespuestasFormulario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCierreNomina",
                table: "RespuestasFormulario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Facturado",
                table: "RespuestasFormulario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFacturado",
                table: "RespuestasFormulario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroFactura",
                table: "RespuestasFormulario",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CierreNomina",
                table: "RespuestasFormulario");

            migrationBuilder.DropColumn(
                name: "FechaCierreNomina",
                table: "RespuestasFormulario");

            migrationBuilder.DropColumn(
                name: "Facturado",
                table: "RespuestasFormulario");

            migrationBuilder.DropColumn(
                name: "FechaFacturado",
                table: "RespuestasFormulario");

            migrationBuilder.DropColumn(
                name: "NumeroFactura",
                table: "RespuestasFormulario");
        }
    }
}
