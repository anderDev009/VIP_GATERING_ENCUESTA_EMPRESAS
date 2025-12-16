using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using VIP_GATERING.Infrastructure.Identity;

namespace VIP_GATERING.Tests;

public class PasswordFlowTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly string _seedPassword = IdentityDefaults.GetDefaultPassword();
    public PasswordFlowTests(TestWebAppFactory factory) { _factory = factory; }

    [Fact]
    public async Task Empleado_First_Login_Redirects_To_ChangePassword()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var m = System.Text.RegularExpressions.Regex.Match(loginPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        var token = m.Success ? m.Groups[1].Value : string.Empty;
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]{
            new KeyValuePair<string,string>("Email","empleado@demo.local"),
            new KeyValuePair<string,string>("Password",_seedPassword),
            new KeyValuePair<string,string>("__RequestVerificationToken", token)
        }) );
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        login.Headers.Location!.ToString().Should().Contain("/Account/ChangePassword");
    }
}


