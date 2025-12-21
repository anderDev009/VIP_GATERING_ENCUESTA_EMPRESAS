using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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

        // Roles: Admin, Empresa, Empleado, Sucursal, Monitor
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

        // Users
        async Task EnsurePasswordAsync(ApplicationUser user, string expectedPassword)
        {
            // Reasegurar contrasena demo y limpiar lockout para usuarios conocidos en Dev/Testing
            var hasPwd = await users.HasPasswordAsync(user);
            var validPwd = hasPwd && await users.CheckPasswordAsync(user, expectedPassword);
            if (!validPwd)
            {
                if (hasPwd)
                    await users.RemovePasswordAsync(user);
                await users.AddPasswordAsync(user, expectedPassword);
            }
            await users.ResetAccessFailedCountAsync(user);
            await users.SetLockoutEndDateAsync(user, null);
        }

        async Task<ApplicationUser> EnsureUser(string userName, string role, Guid? empresaId = null, Guid? empleadoId = null, string? legacyEmail = null)
        {
            var user = await users.FindByNameAsync(userName);
            if (user == null && !string.IsNullOrWhiteSpace(legacyEmail))
            {
                user = await users.FindByEmailAsync(legacyEmail);
            }
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
                await users.CreateAsync(user, userName);
            }
            else
            {
                var changed = false;
                if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
                {
                    user.UserName = userName;
                    user.NormalizedUserName = userName.ToUpperInvariant();
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    user.Email = null;
                    user.NormalizedEmail = null;
                    user.EmailConfirmed = true;
                    changed = true;
                }
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
                    await users.UpdateAsync(user);
            }

            // Si ya existe, asegurar contrasena y desbloqueo en entornos no productivos
            if (!env.IsProduction())
                await EnsurePasswordAsync(user, userName);

            if (!await users.IsInRoleAsync(user, role))
            {
                await users.AddToRoleAsync(user, role);
            }
            return user;
        }

        var adminUserName = EnsurePasswordCompliance("ADMIN");
        await EnsureUser(adminUserName, "Admin");
    }

    private static string BuildUserName(string filialNombre, string? empleadoCodigo, Guid empleadoId)
    {
        var filial = ToTitleToken(filialNombre);
        var codigo = ToToken(empleadoCodigo);
        if (string.IsNullOrWhiteSpace(codigo))
            codigo = empleadoId.ToString("N").Substring(0, 6).ToUpperInvariant();

        var baseUser = $"{filial}_{codigo}";
        return EnsurePasswordCompliance(baseUser);
    }

    private static string ToTitleToken(string value)
    {
        var cleaned = RemoveDiacritics(value ?? string.Empty);
        var parts = Regex.Split(cleaned, "[^A-Za-z0-9]+");
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.Substring(1).ToLowerInvariant());
        }
        return sb.Length == 0 ? "Filial" : sb.ToString();
    }

    private static string ToToken(string? value)
    {
        var cleaned = RemoveDiacritics(value ?? string.Empty);
        var chars = cleaned.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToUpperInvariant();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string EnsurePasswordCompliance(string value)
    {
        var result = value;
        if (!result.Any(char.IsLower)) result += "a";
        if (!result.Any(char.IsUpper)) result += "A";
        if (!result.Any(char.IsDigit)) result += "1";
        if (!result.Any(ch => !char.IsLetterOrDigit(ch))) result += "_";
        if (result.Length < 20) result += new string('0', 20 - result.Length);
        return result;
    }
}
