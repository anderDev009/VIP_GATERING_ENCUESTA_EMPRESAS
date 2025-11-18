using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Infrastructure;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;
using VIP_GATERING.Application;
using VIP_GATERING.WebUI.Services;

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
    options.Password.RequiredLength = 3;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
    options.SlidingExpiration = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
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
    if (db.Database.IsSqlite())
        db.Database.EnsureCreated();
    else
        db.Database.Migrate();
    await SeedData.EnsureSeedAsync(db);
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


