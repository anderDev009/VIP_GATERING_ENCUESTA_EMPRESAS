using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIP_GATERING.Infrastructure.Identity;
using System.Linq;
using VIP_GATERING.WebUI.Models.Account;

namespace VIP_GATERING.WebUI.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    { _signInManager = signInManager; _userManager = userManager; }

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
            var res = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true, lockoutOnFailure: true);
            if (res.Succeeded)
            {
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
        return RedirectToAction("Index", "Home");
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
