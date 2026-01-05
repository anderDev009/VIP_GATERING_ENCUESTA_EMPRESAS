using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Infrastructure;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;
using VIP_GATERING.Application;
using VIP_GATERING.WebUI.Services;
using System;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddControllersWithViews().AddViewLocalization();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 0;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.User.RequireUniqueEmail = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    var isDev = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
    options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.Strict;
    options.Cookie.Name = "VIPGATERING.AUTH";
});
builder.Services.AddApplication();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<VIP_GATERING.WebUI.Services.ICurrentUserService, VIP_GATERING.WebUI.Services.CurrentUserService>();
builder.Services.AddScoped<IEncuestaCierreService, EncuestaCierreService>();
builder.Services.AddScoped<IOptionImageService, OptionImageService>();
builder.Services.AddHostedService<OptionImageCleanupService>();
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "img-src 'self' data: blob:; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self'; " +
            "font-src 'self' data:; " +
            "object-src 'none'; " +
            "frame-ancestors 'self';";
        return Task.CompletedTask;
    });
    await next();
});
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Cultura por defecto: español
var supportedCultures = new[] { new System.Globalization.CultureInfo("es-DO") };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("es-DO"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
app.UseRequestLocalization(localizationOptions);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Migraciones automáticas y seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var applied = false;
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            var pending = await db.Database.GetPendingMigrationsAsync();
            if (pending.Any())
                db.Database.Migrate();
            applied = true;
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Migracion fallida (intento {attempt}/10): {ex.Message}");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
    if (!applied)
        throw new Exception("No se pudieron aplicar migraciones despues de varios intentos.");

    // Fallback defensivo: asegurar columnas nuevas si la BD quedo desfasada.
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"CierreNomina\" boolean NOT NULL DEFAULT false;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"FechaCierreNomina\" timestamp with time zone NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"Facturado\" boolean NOT NULL DEFAULT false;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"FechaFacturado\" timestamp with time zone NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"NumeroFactura\" text NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"BaseSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"ItbisSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"TotalSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"EmpresaPagaSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"EmpleadoPagaSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"ItbisEmpresaSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"ItbisEmpleadoSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"AdicionalBaseSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"AdicionalItbisSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"AdicionalTotalSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"AdicionalEmpresaPagaSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"AdicionalEmpleadoPagaSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"AdicionalItbisEmpresaSnapshot\" numeric NULL;");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS \"RespuestasFormulario\" " +
        "ADD COLUMN IF NOT EXISTS \"AdicionalItbisEmpleadoSnapshot\" numeric NULL;");
    await SeedData.EnsureSeedAsync(db, app.Environment.ContentRootPath);
    // Ensure Identity roles and demo users
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await VIP_GATERING.WebUI.Setup.IdentitySeeder.SeedAsync(db, roleMgr, userMgr, app.Environment);
}

app.Run();

namespace VIP_GATERING.WebUI
{
    public partial class Program { }
}
