Integración de ASP.NET Identity – Plan Técnico

Contexto actual
- Autenticación temporal basada en cookie `uid` manejada por `ICurrentUserService`, con selección manual de usuario.
- Modelos propios: `Usuario` , `Rol`, `RolUsuario` para roles/permisos iniciales.
- Menús y reglas de negocio ya implementadas (alcance por cliente/sucursal, bloqueo por encuestas, clonación de menús, etc.).

Objetivo de la migración
- Reemplazar el mecanismo temporal por ASP.NET Core Identity con almacenamiento en EF Core.
- Mapear usuarios existentes (`Usuario`) a `IdentityUser` y roles a `IdentityRole`.
- Mantener compatibilidad con reglas de negocio (empleado → usuario, roles administrativos).

Alcance
1) Infraestructura
   - Agregar paquetes: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity.UI`.
   - Extender `AppDbContext` para Identity: heredar de `IdentityDbContext` o incluir `AddIdentityCore` con tablas personalizadas.
   - Migración de esquema para tablas de Identity.

2) Usuarios y roles
   - Crear `ApplicationUser : IdentityUser` con FK opcional a `EmpleadoId`.
   - Crear `ApplicationRole : IdentityRole`.
   - Seed de roles: `Admin`, `Gestor`, `Empleado`.
   - Estrategia de mapeo:
     - Si existe `Usuario` por `EmpleadoId`, crear `ApplicationUser` con mismo nombre de usuario y marcar `EmpleadoId`.
     - Asignar roles equivalentes basados en `RolUsuario`.
   - Generar contraseñas temporales y obligar cambio en primer inicio si aplica.

3) Autenticación/Autorización
   - Reemplazar `CurrentUserService` por `IHttpContextAccessor` → `UserManager` y `SignInManager`.
   - Autorizar áreas críticas (Administración de menús, clonación, CRUD maestros) con `[Authorize(Roles="Admin,Gestor")]`.
   - Empleados: `[Authorize(Roles="Empleado,Admin,Gestor")]` para vistas de selección semanal.

4) Flujos
   - Login/Logout/Registro (si aplica) con Identity UI o vistas propias.
   - Restablecimiento de contraseña por email: configurar `IEmailSender` (SMTP o proveedor externo).
   - Impersonación para admins (opcional): permitir “Ver semana como” usando `UserManager` + `SignInManager` de forma segura, registrado en bitácora.

5) Migración de datos
   - Comando de migración y script de seed que:
     - Lee `Usuarios` y crea `ApplicationUser`/`ApplicationRole`/`UserRoles`.
     - Vincula `EmpleadoId` en `ApplicationUser`.
   - Mantener tablas antiguas como legado inicialmente; eliminar en fase posterior.

6) Seguridad y buenas prácticas
   - En producción, encriptación de cookies, `DataProtection` y `IEmailSender` con proveedor seguro.
   - Políticas de contraseña y bloqueo de cuentas.

7) Plan de despliegue incremental
   - Rama feature/identity.
   - Migración de esquema y seed en entorno de prueba.
   - Pruebas E2E: login, autorización por rol, flujos de menú.
   - Congelar `CurrentUserService` solo para desarrollo, deshabilitar en producción.

Tareas pendientes para la integración
- [ ] Agregar paquetes Identity y extender `AppDbContext`.
- [ ] Crear `ApplicationUser`/`ApplicationRole` y migraciones.
- [ ] Servicios de `UserManager/SignInManager` en DI.
- [ ] Vistas de Login + flujo de restablecimiento.
- [ ] Seed de roles/usuarios base.
- [ ] Reemplazar `CurrentUserService` en controladores.
- [ ] Pruebas de autorización por rol.

Notas
- El atajo actual “Ver semana” en Empleados usa un servicio que garantiza `Usuario` por `Empleado` y setea la cookie `uid`. Con Identity real, se reemplazará por una función de impersonación para administradores.

