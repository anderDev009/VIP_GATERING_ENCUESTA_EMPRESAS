using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VIP_GATERING.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: true),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesMenu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PermitirEdicionSemanaActual = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DiasAnticipoSemanaActual = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    HoraLimiteEdicion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 432000000000L),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActualizadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesMenu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Rnc = table.Column<string>(type: "text", nullable: true),
                    ContactoNombre = table.Column<string>(type: "text", nullable: false),
                    ContactoTelefono = table.Column<string>(type: "text", nullable: false),
                    Direccion = table.Column<string>(type: "text", nullable: false),
                    SubsidiaEmpleados = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SubsidioTipo = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SubsidioValor = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 75m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Horarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Borrado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Horarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Opciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    Costo = table.Column<decimal>(type: "numeric", nullable: false),
                    Precio = table.Column<decimal>(type: "numeric", nullable: true),
                    EsSubsidiado = table.Column<bool>(type: "boolean", nullable: false),
                    LlevaItbis = table.Column<bool>(type: "boolean", nullable: false),
                    ImagenUrl = table.Column<string>(type: "text", nullable: true),
                    Borrado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sucursales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Direccion = table.Column<string>(type: "text", nullable: true),
                    Borrado = table.Column<bool>(type: "boolean", nullable: false),
                    SubsidiaEmpleados = table.Column<bool>(type: "boolean", nullable: true),
                    SubsidioTipo = table.Column<int>(type: "integer", nullable: true),
                    SubsidioValor = table.Column<decimal>(type: "numeric", nullable: true),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sucursales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sucursales_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpcionesHorarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OpcionId = table.Column<int>(type: "integer", nullable: false),
                    HorarioId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcionesHorarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpcionesHorarios_Horarios_HorarioId",
                        column: x => x.HorarioId,
                        principalTable: "Horarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OpcionesHorarios_Opciones_OpcionId",
                        column: x => x.OpcionId,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Empleados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: true),
                    Borrado = table.Column<bool>(type: "boolean", nullable: false),
                    SucursalId = table.Column<int>(type: "integer", nullable: false),
                    EsSubsidiado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SubsidioTipo = table.Column<int>(type: "integer", nullable: true),
                    SubsidioValor = table.Column<decimal>(type: "numeric", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    EsJefe = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empleados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Empleados_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Localizaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    SucursalId = table.Column<int>(type: "integer", nullable: false),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Direccion = table.Column<string>(type: "text", nullable: false),
                    IndicacionesEntrega = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Localizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Localizaciones_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Localizaciones_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaTermino = table.Column<DateOnly>(type: "date", nullable: false),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    SucursalId = table.Column<int>(type: "integer", nullable: true),
                    EncuestaCerradaManualmente = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCierreManual = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EncuestaReabiertaManualmente = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Menus_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Menus_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SucursalesHorarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SucursalId = table.Column<int>(type: "integer", nullable: false),
                    HorarioId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SucursalesHorarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SucursalesHorarios_Horarios_HorarioId",
                        column: x => x.HorarioId,
                        principalTable: "Horarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SucursalesHorarios_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmpleadosSucursales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    SucursalId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpleadosSucursales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmpleadosSucursales_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmpleadosSucursales_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    ContrasenaHash = table.Column<string>(type: "text", nullable: false),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmpleadosLocalizaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    LocalizacionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpleadosLocalizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmpleadosLocalizaciones_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmpleadosLocalizaciones_Localizaciones_LocalizacionId",
                        column: x => x.LocalizacionId,
                        principalTable: "Localizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenusAdicionales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    OpcionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenusAdicionales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenusAdicionales_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenusAdicionales_Opciones_OpcionId",
                        column: x => x.OpcionId,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpcionesMenu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OpcionIdA = table.Column<int>(type: "integer", nullable: true),
                    OpcionIdB = table.Column<int>(type: "integer", nullable: true),
                    OpcionIdC = table.Column<int>(type: "integer", nullable: true),
                    OpcionIdD = table.Column<int>(type: "integer", nullable: true),
                    OpcionIdE = table.Column<int>(type: "integer", nullable: true),
                    OpcionesMaximas = table.Column<int>(type: "integer", nullable: false),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HorarioId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcionesMenu", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpcionesMenu_Horarios_HorarioId",
                        column: x => x.HorarioId,
                        principalTable: "Horarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpcionesMenu_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OpcionesMenu_Opciones_OpcionIdA",
                        column: x => x.OpcionIdA,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpcionesMenu_Opciones_OpcionIdB",
                        column: x => x.OpcionIdB,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpcionesMenu_Opciones_OpcionIdC",
                        column: x => x.OpcionIdC,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpcionesMenu_Opciones_OpcionIdD",
                        column: x => x.OpcionIdD,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpcionesMenu_Opciones_OpcionIdE",
                        column: x => x.OpcionIdE,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolesUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RolId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolesUsuario_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolesUsuario_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RespuestasFormulario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    OpcionMenuId = table.Column<int>(type: "integer", nullable: false),
                    SucursalEntregaId = table.Column<int>(type: "integer", nullable: false),
                    LocalizacionEntregaId = table.Column<int>(type: "integer", nullable: true),
                    AdicionalOpcionId = table.Column<int>(type: "integer", nullable: true),
                    Seleccion = table.Column<char>(type: "character(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespuestasFormulario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespuestasFormulario_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RespuestasFormulario_Localizaciones_LocalizacionEntregaId",
                        column: x => x.LocalizacionEntregaId,
                        principalTable: "Localizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RespuestasFormulario_OpcionesMenu_OpcionMenuId",
                        column: x => x.OpcionMenuId,
                        principalTable: "OpcionesMenu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RespuestasFormulario_Opciones_AdicionalOpcionId",
                        column: x => x.AdicionalOpcionId,
                        principalTable: "Opciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RespuestasFormulario_Sucursales_SucursalEntregaId",
                        column: x => x.SucursalEntregaId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_EmpleadoId",
                table: "AspNetUsers",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_EmpresaId",
                table: "AspNetUsers",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_SucursalId",
                table: "Empleados",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosLocalizaciones_EmpleadoId_LocalizacionId",
                table: "EmpleadosLocalizaciones",
                columns: new[] { "EmpleadoId", "LocalizacionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosLocalizaciones_LocalizacionId",
                table: "EmpleadosLocalizaciones",
                column: "LocalizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosSucursales_EmpleadoId_SucursalId",
                table: "EmpleadosSucursales",
                columns: new[] { "EmpleadoId", "SucursalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosSucursales_SucursalId",
                table: "EmpleadosSucursales",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_Localizaciones_EmpresaId",
                table: "Localizaciones",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Localizaciones_SucursalId_Nombre",
                table: "Localizaciones",
                columns: new[] { "SucursalId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Menus_EmpresaId",
                table: "Menus",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_SucursalId",
                table: "Menus",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_MenusAdicionales_MenuId_OpcionId",
                table: "MenusAdicionales",
                columns: new[] { "MenuId", "OpcionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenusAdicionales_OpcionId",
                table: "MenusAdicionales",
                column: "OpcionId");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesHorarios_HorarioId",
                table: "OpcionesHorarios",
                column: "HorarioId");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesHorarios_OpcionId_HorarioId",
                table: "OpcionesHorarios",
                columns: new[] { "OpcionId", "HorarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_HorarioId",
                table: "OpcionesMenu",
                column: "HorarioId");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_MenuId",
                table: "OpcionesMenu",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_OpcionIdA",
                table: "OpcionesMenu",
                column: "OpcionIdA");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_OpcionIdB",
                table: "OpcionesMenu",
                column: "OpcionIdB");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_OpcionIdC",
                table: "OpcionesMenu",
                column: "OpcionIdC");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_OpcionIdD",
                table: "OpcionesMenu",
                column: "OpcionIdD");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesMenu_OpcionIdE",
                table: "OpcionesMenu",
                column: "OpcionIdE");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasFormulario_AdicionalOpcionId",
                table: "RespuestasFormulario",
                column: "AdicionalOpcionId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasFormulario_EmpleadoId",
                table: "RespuestasFormulario",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasFormulario_LocalizacionEntregaId",
                table: "RespuestasFormulario",
                column: "LocalizacionEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasFormulario_OpcionMenuId",
                table: "RespuestasFormulario",
                column: "OpcionMenuId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasFormulario_SucursalEntregaId",
                table: "RespuestasFormulario",
                column: "SucursalEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_RolesUsuario_RolId",
                table: "RolesUsuario",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_RolesUsuario_UsuarioId",
                table: "RolesUsuario",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_EmpresaId",
                table: "Sucursales",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_SucursalesHorarios_HorarioId",
                table: "SucursalesHorarios",
                column: "HorarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SucursalesHorarios_SucursalId_HorarioId",
                table: "SucursalesHorarios",
                columns: new[] { "SucursalId", "HorarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpleadoId",
                table: "Usuarios",
                column: "EmpleadoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ConfiguracionesMenu");

            migrationBuilder.DropTable(
                name: "EmpleadosLocalizaciones");

            migrationBuilder.DropTable(
                name: "EmpleadosSucursales");

            migrationBuilder.DropTable(
                name: "MenusAdicionales");

            migrationBuilder.DropTable(
                name: "OpcionesHorarios");

            migrationBuilder.DropTable(
                name: "RespuestasFormulario");

            migrationBuilder.DropTable(
                name: "RolesUsuario");

            migrationBuilder.DropTable(
                name: "SucursalesHorarios");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Localizaciones");

            migrationBuilder.DropTable(
                name: "OpcionesMenu");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Horarios");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "Opciones");

            migrationBuilder.DropTable(
                name: "Empleados");

            migrationBuilder.DropTable(
                name: "Sucursales");

            migrationBuilder.DropTable(
                name: "Empresas");
        }
    }
}
