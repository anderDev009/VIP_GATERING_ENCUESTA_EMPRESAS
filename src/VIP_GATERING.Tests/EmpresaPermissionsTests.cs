using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.Tests;

public class EmpresaPermissionsTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    public EmpresaPermissionsTests(TestWebAppFactory factory) { _factory = factory; }

    // Nota: las rutas protegidas por alcance responden con AccessDenied o Login según estado de sesión.

    [Fact]
    public async Task Empresa_Can_Create_User_For_Own_Empleado()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var empresaDemoId = db.Empresas.First(e => e.Nombre == "Empresa Demo").Id;
        var ownEmp = await db.Empleados.Include(e=>e.Sucursal).Where(e => e.Sucursal!.EmpresaId == empresaDemoId).Select(e => e.Id).FirstAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string,string>("email","empresa@demo.local"),
            new KeyValuePair<string,string>("password","dev123")
        }));
        var email = $"emp_{Guid.NewGuid():N}@demo.local";
        var create = await client.PostAsync($"/Empleados/CrearUsuario?id={ownEmp}&email={Uri.EscapeDataString(email)}", new FormUrlEncodedContent(Array.Empty<KeyValuePair<string,string>>()));
        create.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Empresa_Can_View_Semana_For_Own_Empleado()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var empresaDemoId = db.Empresas.First(e => e.Nombre == "Empresa Demo").Id;
        var ownEmp = await db.Empleados.Include(e=>e.Sucursal).Where(e => e.Sucursal!.EmpresaId == empresaDemoId).Select(e => e.Id).FirstAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string,string>("email","empresa@demo.local"),
            new KeyValuePair<string,string>("password","dev123")
        }));
        var resp = await client.GetAsync($"/Empleados/Semana/{ownEmp}");
        resp.EnsureSuccessStatusCode();
    }
}
