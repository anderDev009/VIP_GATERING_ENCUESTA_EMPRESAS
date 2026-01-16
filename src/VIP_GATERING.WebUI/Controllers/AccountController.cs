using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Infrastructure.Identity;
using System.Linq;
using VIP_GATERING.WebUI.Models.Account;

namespace VIP_GATERING.WebUI.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VIP_GATERING.Infrastructure.Data.AppDbContext _db;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, VIP_GATERING.Infrastructure.Data.AppDbContext db)
    { _signInManager = signInManager; _userManager = userManager; _db = db; }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginVM { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByNameAsync(model.UserName);
        if (user != null)
        {
            var res = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);
            if (res.Succeeded)
            {
                if (user.EmpleadoId == null && !string.IsNullOrWhiteSpace(user.UserName))
                {
                    var username = user.UserName.Trim().ToUpperInvariant();
                    var empleado = await _db.Empleados
                        .Where(e => e.Codigo != null && e.Codigo.ToUpper() == username)
                        .Select(e => new { e.Id, e.SucursalId })
                        .FirstOrDefaultAsync();
                    if (empleado != null)
                    {
                        var empresaId = await _db.Sucursales
                            .Where(s => s.Id == empleado.SucursalId)
                            .Select(s => (int?)s.EmpresaId)
                            .FirstOrDefaultAsync();
                        user.EmpleadoId = empleado.Id;
                        user.EmpresaId = empresaId;
                        await _userManager.UpdateAsync(user);
                    }
                }
                var claims = await _userManager.GetClaimsAsync(user);
                var mustChange = claims.Any(c => c.Type == "must_change_password" && c.Value == "1");
                if (mustChange)
                    return RedirectToAction("ChangePassword", new { returnUrl = model.ReturnUrl });
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return Redirect(model.ReturnUrl);
                return Redirect(Url.Action("Index", "Home")!);
            }
            if (res.IsLockedOut)
            {
                ModelState.AddModelError("", "Tu cuenta está bloqueada temporalmente por intentos fallidos. Inténtalo en unos minutos o solicita un restablecimiento.");
                return View(model);
            }
        }
        ModelState.AddModelError("", "Usuario o contraseña inválidos");
        return View(model);
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return View("Exit");
    }

    [HttpGet]
    public IActionResult Denied()
    {
        return View();
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword(string? returnUrl = null)
    {
        return View(new ChangePasswordVM { ReturnUrl = returnUrl });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword ?? string.Empty, model.NewPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", string.Join("; ", result.Errors.Select(e => e.Description)));
            return View(model);
        }
        await _userManager.UpdateAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var mc = claims.FirstOrDefault(c => c.Type == "must_change_password");
        if (mc != null) await _userManager.RemoveClaimAsync(user, mc);
        TempData["Success"] = "Contraseña actualizada.";
        return Redirect(model.ReturnUrl ?? Url.Action("Index", "Home")!);
    }
}
