using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VIP_GATERING.Infrastructure.Identity;

namespace VIP_GATERING.Tests;

public class PasswordFlowTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    public PasswordFlowTests(TestWebAppFactory factory) { _factory = factory; }

    [Fact]
    public async Task Empleado_First_Login_Redirects_To_ChangePassword()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var empleadoUser = (await userManager.GetUsersInRoleAsync("Empleado")).First();
        var userName = empleadoUser.UserName ?? string.Empty;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var m = System.Text.RegularExpressions.Regex.Match(loginPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        var token = m.Success ? m.Groups[1].Value : string.Empty;
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]{
            new KeyValuePair<string,string>("UserName", userName),
            new KeyValuePair<string,string>("Password", userName),
            new KeyValuePair<string,string>("__RequestVerificationToken", token)
        }) );
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        login.Headers.Location!.ToString().Should().Contain("/Account/ChangePassword");
    }
}

