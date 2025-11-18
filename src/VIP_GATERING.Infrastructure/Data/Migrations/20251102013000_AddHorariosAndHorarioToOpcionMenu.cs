using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251102013000_AddHorariosAndHorarioToOpcionMenu")]
    public partial class AddHorariosAndHorarioToOpcionMenu : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Horarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Horarios", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "HorarioId",
                table: "OpcionesMenu",
                type: "TEXT",
                nullable: true);

            
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpcionesMenu_Horarios_HorarioId",
                table: "OpcionesMenu");

            migrationBuilder.DropIndex(
                name: "IX_OpcionesMenu_HorarioId",
                table: "OpcionesMenu");

            migrationBuilder.DropColumn(
                name: "HorarioId",
                table: "OpcionesMenu");

            migrationBuilder.DropTable(
                name: "Horarios");
        }
    }
}



