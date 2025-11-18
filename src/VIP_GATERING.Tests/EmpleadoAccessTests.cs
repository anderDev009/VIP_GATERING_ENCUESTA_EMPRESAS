using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VIP_GATERING.Tests;

public class EmpleadoAccessTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    public EmpleadoAccessTests(TestWebAppFactory factory)
    { _factory = factory; }

    [Fact]
    public async Task MiSemana_Requires_Login()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.GetAsync("/Empleado/MiSemana");
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/Account/Login");
    }
}
