using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251104034500_AddBorradoFlags")]
    public partial class AddBorradoFlags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "Borrado", table: "Empleados", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "Borrado", table: "Sucursales", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "Borrado", table: "Opciones", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "Borrado", table: "Horarios", type: "INTEGER", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Borrado", table: "Empleados");
            migrationBuilder.DropColumn(name: "Borrado", table: "Sucursales");
            migrationBuilder.DropColumn(name: "Borrado", table: "Opciones");
            migrationBuilder.DropColumn(name: "Borrado", table: "Horarios");
        }
    }
}

