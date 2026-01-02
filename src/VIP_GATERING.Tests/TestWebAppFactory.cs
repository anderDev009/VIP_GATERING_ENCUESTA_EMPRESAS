using System;
using System.Linq;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.Tests;

public class TestWebAppFactory : WebApplicationFactory<VIP_GATERING.WebUI.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dbPath = $"Data Source=test_{Path.GetRandomFileName()}.db";
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = dbPath,
                ["ASPNETCORE_ENVIRONMENT"] = "Testing"
            };
            config.AddInMemoryCollection(dict!);
        });
        builder.ConfigureServices(services =>
        {
            services.AddLogging();
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            });
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(opts =>
            {
                opts.UseSqlite(dbPath);
            });
        });
    }
}


