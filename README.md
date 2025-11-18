VIP_GATERING – Encuestas de Menú Semanal

Resumen
- ASP.NET Core MVC (.NET 8), arquitectura por capas: Domain, Application, Infrastructure, WebUI.
- EF Core 8 + SQLite (listo para migrar a PostgreSQL luego).
- Patrones: Repository + Unit of Work; servicios de aplicación; DI limpia.
- UI en Razor con Tailwind (configurado mediante npm scripts). Idioma: español.
- Datos seed de ejemplo (empresa, empleados, usuarios, roles, opciones). Flujo temporal de selección de usuario sin login.

Estructura
- src/VIP_GATERING.Domain: Entidades de dominio (Empresa, Sucursal, Empleado, Usuario, Rol, RolUsuario, Opcion, Menu, OpcionMenu, RespuestaFormulario).
- src/VIP_GATERING.Application: Abstracciones (IRepository, IUnitOfWork), servicios (MenuService, FechaServicio) y DI.
- src/VIP_GATERING.Infrastructure: DbContext (AppDbContext), repositorio EF, UnitOfWork, seed de datos y DI.
- src/VIP_GATERING.WebUI: Proyecto MVC. Controladores: Home, Empleado (MiSemana), Menu (SemanaSiguiente), Usuarios (Seleccionar).
- src/VIP_GATERING.Tests: xUnit con pruebas para MenuService (SQLite en memoria).

Requisitos
- .NET SDK 8.x
- Node 18+ (para compilar Tailwind; opcional para ejecutar la app).

Configuración rápida
1) Restaurar y compilar
   - `dotnet build`
2) Base de datos
   - Las migraciones están incluidas. Al iniciar la app, se aplican (`Database.Migrate`) y se insertan datos de ejemplo.
   - ConnectionString: `appsettings.json` → `ConnectionStrings:DefaultConnection` (SQLite `app.db`).
3) UI (Tailwind)
   - Instalar dependencias: `cd src/VIP_GATERING.WebUI && npm i`
   - Generar CSS una vez: `npm run build:css`
   - Desarrollo (watch): `npm run watch:css`
4) Ejecutar
   - `dotnet run --project src/VIP_GATERING.WebUI`
   - Navegar a `/Usuarios/Seleccionar` para escoger un usuario de prueba.
   - Empleado: `/Empleado/MiSemana` muestra el menú de la próxima semana y permite seleccionar A/B/C por día.
   - Admin simple: `/Menu/SemanaSiguiente` para asignar opciones por día.

Notas de diseño
- Autenticación/Autorización: aún no se integra Identity. Se incluye un servicio temporal de “usuario actual” mediante cookie (`uid`) para poder trabajar roles y flujos sin login. Se reemplazará por ASP.NET Identity (roles y claims) cuando definamos el flujo de inicio de sesión y restablecimiento.
- Días de menú: lunes a viernes. `FechaServicio` calcula rango de la semana siguiente (inicio: lunes; fin: viernes).
- Migración a PostgreSQL: bastará con agregar el proveedor `Npgsql.EntityFrameworkCore.PostgreSQL`, cambiar el `UseSqlite(...)` por `UseNpgsql(...)` y crear nueva migración si el esquema cambia.

Pruebas
- Ejecutar: `dotnet test`.
- Prueba incluida: creación automática del menú de la semana siguiente y generación de 5 registros de `OpcionMenu` (uno por día laboral).

Siguientes pasos propuestos
- Integrar ASP.NET Identity con roles y proteger vistas admin.
- Validaciones con FluentValidation en acciones (asignación de opciones, selección del empleado).
- Reportes solicitados (por empleado, por día, costos por semana/mes, etc.).
- API mínima para permitir un front SPA si se desea.

