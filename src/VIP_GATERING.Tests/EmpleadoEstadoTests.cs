using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;

namespace VIP_GATERING.Tests;

public class EmpleadoEstadoTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    public EmpleadoEstadoTests(TestWebAppFactory factory) { _factory = factory; }

    [Fact]
    public async Task Empleado_no_habilitado_ve_mensaje_y_no_puede_seleccionar()
    {
        // Arrange: set empleado demo to Suspendido
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var empleadoUser = (await userManager.GetUsersInRoleAsync("Empleado")).First();
            var emp = await db.Empleados.FirstAsync(e => e.Id == empleadoUser.EmpleadoId);
            emp.Estado = EmpleadoEstado.Suspendido;
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        // Login with anti-forgery token
        var loginPage = await client.GetStringAsync("/Account/Login?returnUrl=%2FEmpleado%2FMiSemana");
        var m = System.Text.RegularExpressions.Regex.Match(loginPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        var token = m.Success ? m.Groups[1].Value : string.Empty;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var empleadoUser = (await userManager.GetUsersInRoleAsync("Empleado")).First();
            var userName = empleadoUser.UserName ?? string.Empty;
            await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("UserName", userName),
                new KeyValuePair<string,string>("Password", userName),
                new KeyValuePair<string,string>("__RequestVerificationToken", token),
                new KeyValuePair<string,string>("ReturnUrl","/Empleado/MiSemana")
            }));
        }

        // Act
        var resp = await client.GetStringAsync("/Empleado/MiSemana");

        // Assert: warning message and inputs disabled
        resp.Should().Contain("habilitada para seleccionar opciones");
        resp.Should().Contain("Guardar seleccion");
        resp.Should().Contain("disabled");
    }
}
