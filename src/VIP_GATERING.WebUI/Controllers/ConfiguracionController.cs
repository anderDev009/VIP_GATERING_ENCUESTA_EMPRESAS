using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIP_GATERING.Application.Services;
using VIP_GATERING.WebUI.Models;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin")]
public class ConfiguracionController : Controller
{
    private readonly IMenuConfiguracionService _menuConfig;

    public ConfiguracionController(IMenuConfiguracionService menuConfig)
    {
        _menuConfig = menuConfig;
    }

    [HttpGet]
    public async Task<IActionResult> Menu()
    {
        var cfg = await _menuConfig.ObtenerAsync();
        var vm = new MenuConfiguracionVM
        {
            PermitirEdicionSemanaActual = cfg.PermitirEdicionSemanaActual,
            DiasAnticipoSemanaActual = cfg.DiasAnticipoSemanaActual,
            HoraLimite = TimeOnly.FromTimeSpan(cfg.HoraLimiteEdicion).ToString("HH':'mm")
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Menu(MenuConfiguracionVM vm)
    {
        if (!TimeSpan.TryParse(vm.HoraLimite, out var horaLimite))
        {
            ModelState.AddModelError(nameof(vm.HoraLimite), "Formato de hora invalido (HH:mm).");
        }
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        await _menuConfig.ActualizarAsync(new MenuConfiguracionUpdate(vm.PermitirEdicionSemanaActual, vm.DiasAnticipoSemanaActual, horaLimite));
        TempData["Success"] = "Configuracion guardada.";
        return RedirectToAction(nameof(Menu));
    }
}
