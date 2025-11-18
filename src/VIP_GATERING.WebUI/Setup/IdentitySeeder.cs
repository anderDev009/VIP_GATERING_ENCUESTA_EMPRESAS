using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;

namespace VIP_GATERING.WebUI.Setup;

public static class IdentitySeeder
{
    public static async Task SeedAsync(AppDbContext db, RoleManager<ApplicationRole> roles, UserManager<ApplicationUser> users, IWebHostEnvironment env)
    {
        // Ensure database is migrated (skip in Testing where EnsureCreated is used)
        if (!env.IsEnvironment("Testing"))
        {
            await db.Database.MigrateAsync();
        }
        // Roles: Admin, Empresa, Empleado
        async Task EnsureRole(string name)
        {
            if (await roles.FindByNameAsync(name) == null)
            {
                try { await roles.CreateAsync(new ApplicationRole { Name = name, NormalizedName = name.ToUpperInvariant() }); }
                catch { /* ignore duplicate in concurrent seeding */ }
            }
        }

        await EnsureRole("Admin");
        await EnsureRole("Empresa");
        await EnsureRole("Empleado");
        await EnsureRole("Sucursal");
        await EnsureRole("Monitor");

        // Create or get base domain entities
        var empresa = await db.Empresas.FirstOrDefaultAsync();
        if (empresa == null)
        {
            empresa = new VIP_GATERING.Domain.Entities.Empresa { Nombre = "Empresa Demo", Rnc = "RNC-000" };
            db.Empresas.Add(empresa);
            await db.SaveChangesAsync();
        }
        var sucursal = await db.Sucursales.FirstOrDefaultAsync() ??
                       (await db.Sucursales.AddAsync(new VIP_GATERING.Domain.Entities.Sucursal { Nombre = "Principal", EmpresaId = empresa.Id })).Entity;
        await db.SaveChangesAsync();

        var empleado = await db.Empleados.FirstOrDefaultAsync() ??
                       (await db.Empleados.AddAsync(new VIP_GATERING.Domain.Entities.Empleado { Nombre = "Empleado Demo", SucursalId = sucursal.Id })).Entity;
        await db.SaveChangesAsync();

        // Users
        async Task<ApplicationUser> EnsureUser(string email, string role, Guid? empresaId = null, Guid? empleadoId = null)
        {
            var user = await users.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    EmpresaId = empresaId,
                    EmpleadoId = empleadoId
                };
                await users.CreateAsync(user, "dev123");
            }
            if (!await users.IsInRoleAsync(user, role))
            {
                await users.AddToRoleAsync(user, role);
            }
            return user;
        }

        await EnsureUser("admin@demo.local", "Admin");
        var empUser = await EnsureUser("empresa@demo.local", "Empresa", empresaId: empresa.Id);
        var empleadoUser = await EnsureUser("empleado@demo.local", "Empleado", empleadoId: empleado.Id);
        await EnsureUser("sucursal@demo.local", "Sucursal", empresaId: empresa.Id, empleadoId: empleado.Id);
        // Marcar empleado para cambio de contraseña en primer login (vía claim)
        var claims = await users.GetClaimsAsync(empleadoUser);
        if (!claims.Any(c => c.Type == "must_change_password" && c.Value == "1"))
            await users.AddClaimAsync(empleadoUser, new System.Security.Claims.Claim("must_change_password", "1"));
    }
}



