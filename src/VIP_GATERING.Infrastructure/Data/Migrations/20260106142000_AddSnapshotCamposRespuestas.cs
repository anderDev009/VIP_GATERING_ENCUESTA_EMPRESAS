using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    public partial class AddSnapshotCamposRespuestas : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ItbisSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmpresaPagaSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmpleadoPagaSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ItbisEmpresaSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ItbisEmpleadoSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdicionalBaseSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdicionalItbisSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdicionalTotalSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdicionalEmpresaPagaSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdicionalEmpleadoPagaSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdicionalItbisEmpresaSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdicionalItbisEmpleadoSnapshot",
                table: "RespuestasFormulario",
                type: "numeric",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BaseSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "ItbisSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "TotalSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "EmpresaPagaSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "EmpleadoPagaSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "ItbisEmpresaSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "ItbisEmpleadoSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "AdicionalBaseSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "AdicionalItbisSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "AdicionalTotalSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "AdicionalEmpresaPagaSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "AdicionalEmpleadoPagaSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "AdicionalItbisEmpresaSnapshot", table: "RespuestasFormulario");
            migrationBuilder.DropColumn(name: "AdicionalItbisEmpleadoSnapshot", table: "RespuestasFormulario");
        }
    }
}
