using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Infrastructure.Identity;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin")]
public class SecurityController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;

    public SecurityController(UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles)
    { _users = users; _roles = roles; }

    public async Task<IActionResult> Index()
    {
        var allRoles = await _roles.Roles.OrderBy(r => r.Name!).ToListAsync();
        var list = await _users.Users.Select(u => new UserVM
        {
            Id = u.Id,
            Email = u.Email!,
            Roles = new List<string>()
        }).ToListAsync();
        foreach (var u in list)
        {
            var user = await _users.FindByIdAsync(u.Id.ToString());
            u.Roles = (await _users.GetRolesAsync(user!)).ToList();
        }
        ViewBag.Roles = allRoles;
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(Guid userId, string role)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user != null && await _roles.RoleExistsAsync(role))
        {
            if (!await _users.IsInRoleAsync(user, role))
                await _users.AddToRoleAsync(user, role);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(Guid userId, string role)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user != null && await _roles.RoleExistsAsync(role))
        {
            if (await _users.IsInRoleAsync(user, role))
                await _users.RemoveFromRoleAsync(user, role);
        }
        return RedirectToAction(nameof(Index));
    }

    public class UserVM
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}

