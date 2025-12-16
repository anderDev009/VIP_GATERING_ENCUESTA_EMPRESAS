using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VIP_GATERING.Infrastructure.Data;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251216193000_AddMenuConfiguracion")]
    public partial class AddMenuConfiguracion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionesMenu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PermitirEdicionSemanaActual = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DiasAnticipoSemanaActual = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    HoraLimiteEdicion = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 432000000000L),
                    CreadoUtc = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActualizadoUtc = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesMenu", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesMenu");
        }
    }
}
