Identity Integration: Roles & Permissions

Summary
- Added ASP.NET Core Identity with Guid keys (`ApplicationUser`, `ApplicationRole`).
- Roles: `Admin`, `Empresa`, `Empleado`.
- Seeded demo users (password `dev123`):
  - admin@demo.local → Admin
  - empresa@demo.local → Empresa (linked to first Empresa)
  - empleado@demo.local → Empleado (linked to first Empleado)
- Replaced custom cookie-based user selection with Identity.
- Enforced role-based access across controllers.
- Added admin-only security maintenance UI to manage user roles.
- Added tests for auth guards and wire-up.

Key Files
- DbContext: `src/VIP_GATERING.Infrastructure/Data/AppDbContext.cs`
  - Now inherits from `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- Identity Entities:
  - `src/VIP_GATERING.Infrastructure/Identity/ApplicationUser.cs`
  - `src/VIP_GATERING.Infrastructure/Identity/ApplicationRole.cs`
- Identity DI & Cookie config: `src/VIP_GATERING.WebUI/Program.cs`
- Seeding: `src/VIP_GATERING.WebUI/Setup/IdentitySeeder.cs`
- Current user abstraction (claims → domain): `src/VIP_GATERING.WebUI/Services/CurrentUserService.cs`
- Admin security UI: `src/VIP_GATERING.WebUI/Controllers/SecurityController.cs` + view `Views/Security/Index.cshtml`
- Account (login/logout): `src/VIP_GATERING.WebUI/Controllers/AccountController.cs` + views `Views/Account/*.cshtml`

Auth Rules
- Admin: Acceso total. Controladores marcados con `[Authorize(Roles = "Admin")]` (por ej. `EmpresasController`, `OpcionesController`, `AdminController`, `SecurityController`).
- Empresa: Puede administrar sus empleados/sucursales.
  - `EmpleadosController` y `SucursalesController`: `[Authorize(Roles = "Admin,Empresa")]`
  - Filtro a nivel de consulta para limitar a `EmpresaId` del usuario.
- Empleado: Solo su menú.
  - `EmpleadoController`: `[Authorize(Roles = "Empleado")]` y usa `ICurrentUserService.EmpleadoId`.

Removed/Legacy
- Eliminado el flujo temporal de selección de usuario:
  - Removed: `Controllers/UsuariosController.cs`, `Views/Usuarios/Seleccionar.cshtml`.
- Entidades `Usuario`, `Rol`, `RolUsuario` (dominio) se mantienen para compatibilidad, pero no se usan para autorización.

Migrations
- Nueva migración: `AddIdentitySupport` que crea tablas de Identity.
- Program aplica `db.Database.Migrate()` al iniciar y ejecuta seeding.

Tests
- Proyecto: `src/VIP_GATERING.Tests`
- Nuevos tests:
  - `AuthorizationTests`: verifica redirección a login para `/Security` (protegido, admin-only).
  - `EmpleadoAccessTests`: verifica redirección a login para `/Empleado/MiSemana` (empleado-only).
- Infra for Web tests: `TestWebAppFactory` usa SQLite file temporal por ejecución.

How To Use
1) Ejecutar la app (`dotnet run` en `src/VIP_GATERING.WebUI`).
2) Login en `/Account/Login` con usuarios de demo (pwd `dev123`).
3) Navegación:
   - Admin: ver "Administrar" y "Seguridad" en la barra.
   - Empresa: acceder a Empleados/Sucursales limitadas a su empresa.
   - Empleado: acceder a "Mi semana".

Notes
- CurrentUserService expone `UserId`, `EmpleadoId` y `EmpresaId` desde Identity.
- Si agregas nuevos controladores de mantenimiento, protégelos con `[Authorize]` y aplica filtros por rol/empresa.

