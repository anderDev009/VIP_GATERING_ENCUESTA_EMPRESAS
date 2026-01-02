using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;

namespace VIP_GATERING.Tests;

internal static class TestDataHelpers
{
    private const string DemoEmpresaName = "Empresa Demo";
    private const string DemoFilialName = "Filial Demo";
    private const string DemoEmpleadoName = "Empleado Demo";
    private const string EmpresaUserName = "EmpresaDemo";
    private const string EmpleadoUserName = "EmpleadoDemo";
    private const string MandatoryClaimType = "must_change_password";

    public static async Task EnsureDemoDataAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        await EnsureRoleAsync(roleManager, "Empresa");
        await EnsureRoleAsync(roleManager, "Empleado");

        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Nombre == DemoEmpresaName);
        if (empresa == null)
        {
            empresa = new Empresa
            {
                Nombre = DemoEmpresaName,
                ContactoNombre = $"{DemoEmpresaName} - Contacto principal",
                ContactoTelefono = "000-000-0000",
                Direccion = $"{DemoEmpresaName} - Oficina central",
                SubsidiaEmpleados = true,
                SubsidioTipo = SubsidioTipo.Porcentaje,
                SubsidioValor = 75m
            };
            db.Empresas.Add(empresa);
            await db.SaveChangesAsync();
        }

        var sucursal = await db.Sucursales.FirstOrDefaultAsync(s => s.Nombre == DemoFilialName && s.EmpresaId == empresa.Id);
        if (sucursal == null)
        {
            sucursal = new Sucursal
            {
                Nombre = DemoFilialName,
                EmpresaId = empresa.Id,
                SubsidiaEmpleados = true,
                SubsidioTipo = SubsidioTipo.Porcentaje,
                SubsidioValor = empresa.SubsidioValor
            };
            db.Sucursales.Add(sucursal);
            await db.SaveChangesAsync();
        }

        var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.Nombre == DemoEmpleadoName);
        if (empleado == null)
        {
            empleado = new Empleado
            {
                Nombre = DemoEmpleadoName,
                Codigo = "EMPDEMO",
                SucursalId = sucursal.Id
            };
            db.Empleados.Add(empleado);
            await db.SaveChangesAsync();
        }

        await EnsureUserAsync(userManager, EmpresaUserName, "Empresa", empresaId: empresa.Id);
        var employeeUser = await EnsureUserAsync(userManager, EmpleadoUserName, "Empleado", empresaId: empresa.Id, empleadoId: empleado.Id);
        await EnsureClaimAsync(userManager, employeeUser, MandatoryClaimType, "1");
    }

    private static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string roleName)
    {
        if (await roleManager.FindByNameAsync(roleName) != null) return;
        await roleManager.CreateAsync(new ApplicationRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() });
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string userName, string role, int? empresaId = null, int? empleadoId = null)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = userName,
                Email = null,
                EmailConfirmed = true,
                EmpresaId = empresaId,
                EmpleadoId = empleadoId
            };
            await userManager.CreateAsync(user);
            await EnsurePasswordAsync(userManager, user, userName);
        }
        else
        {
            var changed = false;
            if (user.EmpresaId != empresaId)
            {
                user.EmpresaId = empresaId;
                changed = true;
            }
            if (user.EmpleadoId != empleadoId)
            {
                user.EmpleadoId = empleadoId;
                changed = true;
            }
            if (changed)
            {
                await userManager.UpdateAsync(user);
            }
            await EnsurePasswordAsync(userManager, user, userName);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static async Task EnsurePasswordAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string expectedPassword)
    {
        var hasPassword = await userManager.HasPasswordAsync(user);
        var validPassword = hasPassword && await userManager.CheckPasswordAsync(user, expectedPassword);
        if (validPassword) return;
        if (hasPassword)
        {
            await userManager.RemovePasswordAsync(user);
        }
        await userManager.AddPasswordAsync(user, expectedPassword);
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
    }

    private static async Task EnsureClaimAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string claimType, string claimValue)
    {
        var claims = await userManager.GetClaimsAsync(user);
        if (claims.Any(c => c.Type == claimType && c.Value == claimValue)) return;
        await userManager.AddClaimAsync(user, new Claim(claimType, claimValue));
    }
}
