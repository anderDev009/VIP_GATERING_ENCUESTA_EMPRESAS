using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Services;
using VIP_GATERING.Infrastructure.Services;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.Infrastructure.Identity;
using VIP_GATERING.Application.Services;
using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin,Empresa")]
public class EmpleadosController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmpleadoUsuarioService _empUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMenuService _menuService;
    private readonly IEncuestaCierreService _cierre;
    public EmpleadosController(AppDbContext db, ICurrentUserService currentUser, IEmpleadoUsuarioService empUserService, UserManager<ApplicationUser> userManager, IMenuService menuService, IEncuestaCierreService cierre)
    { _db = db; _currentUser = currentUser; _empUserService = empUserService; _userManager = userManager; _menuService = menuService; _cierre = cierre; }

    public async Task<IActionResult> Index(Guid? empresaId, Guid? sucursalId, string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Empleados.Include(e => e.Sucursal)!.ThenInclude(s => s!.Empresa).Where(e => !e.Borrado).AsQueryable();
        // If Empresa role, limit to current Empresa
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _currentUser.EmpresaId;
            if (currentEmpresaId != null)
                query = query.Where(e => e.Sucursal!.EmpresaId == currentEmpresaId);
        }
        if (empresaId != null) query = query.Where(e => e.Sucursal!.EmpresaId == empresaId);
        if (sucursalId != null) query = query.Where(e => e.SucursalId == sucursalId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(e => e.Nombre.ToLower().Contains(ql) || (e.Codigo != null && e.Codigo.ToLower().Contains(ql)));
        }
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Sucursales = await _db.Sucursales.OrderBy(s => s.Nombre).ToListAsync();
        ViewBag.EmpresaId = empresaId; ViewBag.SucursalId = sucursalId; ViewBag.Q = q;
        var paged = await query.OrderBy(e => e.Nombre).ToPagedResultAsync(page, pageSize);
        // Usuario por empleado en página
        var ids = paged.Items.Select(i => i.Id).ToList();
        var usuarios = await _db.Set<ApplicationUser>()
            .Where(u => u.EmpleadoId != null && ids.Contains(u.EmpleadoId.Value))
            .Select(u => u.EmpleadoId!.Value)
            .ToListAsync();
        ViewBag.EmpleadoUsuarioIds = usuarios;
        return View(paged);
    }

    public async Task<IActionResult> Create()
    {
        var empresas = _db.Empresas.AsQueryable();
        var sucursales = _db.Sucursales.AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            empresas = empresas.Where(e => e.Id == empresaId);
            sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
        }
        ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Sucursales = await sucursales.OrderBy(s => s.Nombre).ToListAsync();
        return View(new Empleado());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Empleado model)
    {
        if (!ModelState.IsValid)
        {
            var empresas = _db.Empresas.AsQueryable();
            var sucursales = _db.Sucursales.AsQueryable();
            if (User.IsInRole("Empresa"))
            {
                var empresaId = _currentUser.EmpresaId;
                empresas = empresas.Where(e => e.Id == empresaId);
                sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
            }
            ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Sucursales = await sucursales.OrderBy(s => s.Nombre).ToListAsync();
            return View(model);
        }
        // Seguridad: Empresa solo puede crear en sus sucursales
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            var sucEmpresaId = await _db.Sucursales.Where(s => s.Id == model.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
            if (empresaId == null || sucEmpresaId != empresaId) return Forbid();
        }
        await _db.Empleados.AddAsync(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Empleado creado.";
        return RedirectToAction(nameof(Index), new { sucursalId = model.SucursalId });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var ent = await _db.Empleados.FindAsync(id);
        if (ent == null) return NotFound();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            var empEmpresaId = await _db.Sucursales.Where(s => s.Id == ent.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
            if (empresaId == null || empEmpresaId != empresaId) return Forbid();
        }
        var empresas = _db.Empresas.AsQueryable();
        var sucursales = _db.Sucursales.AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            empresas = empresas.Where(e => e.Id == empresaId);
            sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
        }
        ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Sucursales = await sucursales.OrderBy(s => s.Nombre).ToListAsync();
        var user = await _db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.EmpleadoId == ent.Id);
        ViewBag.UsuarioExiste = user != null;
        ViewBag.UsuarioEmail = user?.Email;
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Empleado model)
    {
        if (!ModelState.IsValid)
        {
            var empresas = _db.Empresas.AsQueryable();
            var sucursales = _db.Sucursales.AsQueryable();
            if (User.IsInRole("Empresa"))
            {
                var empresaId = _currentUser.EmpresaId;
                empresas = empresas.Where(e => e.Id == empresaId);
                sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
            }
            ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Sucursales = await sucursales.OrderBy(s => s.Nombre).ToListAsync();
            return View(model);
        }
        var ent = await _db.Empleados.FindAsync(id);
        if (ent == null) return NotFound();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            var newSucEmpresaId = await _db.Sucursales.Where(s => s.Id == model.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
            if (empresaId == null || newSucEmpresaId != empresaId) return Forbid();
        }
        ent.Codigo = model.Codigo;
        ent.Nombre = model.Nombre;
        ent.SucursalId = model.SucursalId;
        ent.Estado = model.Estado;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Empleado actualizado.";
        return RedirectToAction(nameof(Index), new { sucursalId = model.SucursalId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ent = await _db.Empleados.FindAsync(id);
        if (ent != null)
        {
            if (User.IsInRole("Empresa"))
            {
                var empresaId = _currentUser.EmpresaId;
                var empEmpresaId = await _db.Sucursales.Where(s => s.Id == ent.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
                if (empresaId == null || empEmpresaId != empresaId) return Forbid();
            }
            var sucId = ent.SucursalId;
            ent.Estado = EmpleadoEstado.Desactivado;
            ent.Borrado = true;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Empleado desactivado.";
            return RedirectToAction(nameof(Index), new { sucursalId = sucId });
        }
        return RedirectToAction(nameof(Index));
    }

    // Atajo: simular sesión del usuario del empleado y abrir "Mi semana"
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerSemana(Guid id)
    {
        var usuario = await _empUserService.EnsureUsuarioParaEmpleadoAsync(id);
        await _currentUser.SetUsuarioAsync(usuario.Id);
        return RedirectToAction("MiSemana", "Empleado");
    }

    // Vista de semana para Admin/Empresa (solo lectura) de un empleado específico
    [HttpGet]
    public async Task<IActionResult> Semana(Guid id)
    {
        // Validar alcance para rol Empresa
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            var empEmpresaId = await _db.Empleados.Include(e => e.Sucursal)!.Where(e => e.Id == id)
                .Select(e => e.Sucursal!.EmpresaId).FirstOrDefaultAsync();
            if (empresaId == null || empEmpresaId != empresaId) return Forbid();
        }

        var modelo = await ConstruirSemanaEmpleadoAsync(id, false);
        if (modelo == null) return NotFound();
        return View("Semana", modelo);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> EditarSemana(Guid id)
    {
        var modelo = await ConstruirSemanaEmpleadoAsync(id, true);
        if (modelo == null) return NotFound();
        if (!modelo.EsJefe)
        {
            TempData["Error"] = "El empleado no está marcado como jefe.";
            return RedirectToAction(nameof(Semana), new { id });
        }
        return View("EditarSemana", modelo);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarSemanaJefe(Guid empleadoId, SemanaEmpleadoVM model)
    {
        var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado == null) return NotFound();
        if (!empleado.EsJefe)
        {
            TempData["Error"] = "El empleado no está marcado como jefe.";
            return RedirectToAction(nameof(Semana), new { id = empleadoId });
        }

        if (model.Dias == null || model.Dias.Count == 0)
        {
            TempData["Info"] = "No se recibieron cambios.";
            return RedirectToAction(nameof(Semana), new { id = empleadoId });
        }

        foreach (var d in model.Dias)
        {
            if (d.Seleccion is 'A' or 'B' or 'C')
            {
                await _menuService.RegistrarSeleccionAsync(empleadoId, d.OpcionMenuId, d.Seleccion.Value);
            }
        }

        TempData["Success"] = "Selecciones actualizadas para el empleado jefe.";
        return RedirectToAction(nameof(Semana), new { id = empleadoId });
    }

    // Crear usuario de Identity para un empleado (solo Admin o Empresa dentro de su empresa)
    [HttpPost]
    public async Task<IActionResult> CrearUsuario(Guid id, string email)
    {
        var empleado = await _db.Empleados.Include(e => e.Sucursal).FirstOrDefaultAsync(e => e.Id == id);
        if (empleado == null) return NotFound();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            if (empresaId == null || empleado.Sucursal!.EmpresaId != empresaId) return Forbid();
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "El correo es requerido.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        var emailAttr = new EmailAddressAttribute();
        if (!emailAttr.IsValid(email))
        {
            TempData["Error"] = "Formato de correo inválido.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        var existingByEmail = await _userManager.FindByEmailAsync(email);
        if (existingByEmail != null)
        {
            TempData["Error"] = "Ya existe un usuario con ese correo.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        var exists = await _db.Set<ApplicationUser>().AnyAsync(u => u.EmpleadoId == id);
        if (!exists)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                EmpleadoId = empleado.Id,
                EmpresaId = empleado.Sucursal!.EmpresaId
            };
            var res = await _userManager.CreateAsync(user, "dev123");
            if (res.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Empleado");
                TempData["Success"] = "Usuario creado.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", res.Errors.Select(e => e.Description));
            }
        }
        else
        {
            TempData["Error"] = "Ya existe un usuario para este empleado.";
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    // Reset de contraseña (Admin o Empresa sobre su empleado)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Guid id, string newPassword, string confirmPassword)
    {
        var empleado = await _db.Empleados.Include(e => e.Sucursal).FirstOrDefaultAsync(e => e.Id == id);
        if (empleado == null) return NotFound();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            if (empresaId == null || empleado.Sucursal!.EmpresaId != empresaId) return Forbid();
        }
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 3)
        {
            TempData["Error"] = "La nueva contraseña es requerida (mínimo 3 caracteres).";
            return RedirectToAction(nameof(Edit), new { id });
        }
        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "La confirmación no coincide.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        var user = await _db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.EmpleadoId == id);
        if (user == null)
        {
            TempData["Error"] = "El empleado no tiene usuario.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        var hasPwd = await _userManager.HasPasswordAsync(user);
        if (hasPwd)
        {
            var rem = await _userManager.RemovePasswordAsync(user);
            if (!rem.Succeeded)
            {
                TempData["Error"] = string.Join("; ", rem.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Edit), new { id });
            }
        }
        var add = await _userManager.AddPasswordAsync(user, newPassword);
        if (!add.Succeeded)
        {
            TempData["Error"] = string.Join("; ", add.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Edit), new { id });
        }
        // Marcar para cambio en primer inicio (vía claim)
        var claims = await _userManager.GetClaimsAsync(user);
        var mc = claims.FirstOrDefault(c => c.Type == "must_change_password");
        if (mc == null)
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("must_change_password", "1"));
        await _userManager.UpdateAsync(user);
        TempData["Success"] = "Contraseña reiniciada. Se pedirá cambio al iniciar sesión.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // Validación remota: email disponible (para creación de usuario)
    [AcceptVerbs("Get", "Post")]
    public async Task<IActionResult> CheckEmailAvailable(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return Json("El correo es requerido.");
        var emailAttr = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        if (!emailAttr.IsValid(email)) return Json("Formato de correo inválido.");
        var exists = await _userManager.FindByEmailAsync(email);
        if (exists != null) return Json("Ya existe un usuario con ese correo.");
        return Json(true);
    }
    private async Task<SemanaEmpleadoVM?> ConstruirSemanaEmpleadoAsync(Guid empleadoId, bool paraAdministrador)
    {
        var info = await _db.Empleados
            .Include(e => e.Sucursal)!.ThenInclude(s => s!.Empresa)
            .Where(e => e.Id == empleadoId)
            .Select(e => new
            {
                e.Id,
                e.Nombre,
                e.EsJefe,
                e.SucursalId,
                SucursalNombre = e.Sucursal!.Nombre,
                EmpresaId = e.Sucursal!.EmpresaId,
                EmpresaNombre = e.Sucursal!.Empresa!.Nombre
            })
            .FirstOrDefaultAsync();
        if (info == null) return null;

        var fechas = new FechaServicio();
        var (inicio, fin) = fechas.RangoSemanaSiguiente();
        var menu = await _menuService.GetEffectiveMenuForSemanaAsync(inicio, fin, info.EmpresaId, info.SucursalId);
        var fechaCierreAuto = _cierre.GetFechaCierreAutomatica(menu);
        var encuestaCerrada = _cierre.EstaCerrada(menu);
        var opciones = await _db.OpcionesMenu
            .Include(o => o.OpcionA).Include(o => o.OpcionB).Include(o => o.OpcionC).Include(o => o.Horario)
            .Where(o => o.MenuId == menu.Id)
            .OrderBy(o => o.DiaSemana)
            .ToListAsync();
        if (opciones.Count == 0)
        {
            var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            foreach (var d in dias)
                await _db.OpcionesMenu.AddAsync(new OpcionMenu { MenuId = menu.Id, DiaSemana = d });
            await _db.SaveChangesAsync();
            opciones = await _db.OpcionesMenu.Include(o => o.OpcionA).Include(o => o.OpcionB).Include(o => o.OpcionC)
                .Where(o => o.MenuId == menu.Id).OrderBy(o => o.DiaSemana).ToListAsync();
        }
        var opcionIds = opciones.Select(o => o.Id).ToList();
        var respuestas = await _db.RespuestasFormulario
            .Where(r => r.EmpleadoId == empleadoId && opcionIds.Contains(r.OpcionMenuId))
            .ToListAsync();

        return new SemanaEmpleadoVM
        {
            EmpleadoId = info.Id,
            EmpleadoNombre = info.Nombre,
            MenuId = menu.Id,
            FechaInicio = menu.FechaInicio,
            FechaTermino = menu.FechaTermino,
            Bloqueado = encuestaCerrada,
            MensajeBloqueo = encuestaCerrada ? $"La encuesta está cerrada desde {fechaCierreAuto:dd/MM/yyyy}." : null,
            RespuestasCount = respuestas.Count,
            TotalDias = opciones.Count,
            OrigenMenu = menu.SucursalId != null ? "Sucursal" : "Cliente",
            EmpresaNombre = info.EmpresaNombre,
            SucursalNombre = info.SucursalNombre,
            EsJefe = info.EsJefe,
            EsVistaAdministrador = paraAdministrador,
            Dias = opciones.Select(o => new DiaEmpleadoVM
            {
                OpcionMenuId = o.Id,
                DiaSemana = o.DiaSemana,
                HorarioNombre = o.Horario?.Nombre,
                A = o.OpcionA?.Nombre,
                B = o.OpcionB?.Nombre,
                C = o.OpcionC?.Nombre,
                ImagenA = o.OpcionA?.ImagenUrl,
                ImagenB = o.OpcionB?.ImagenUrl,
                ImagenC = o.OpcionC?.ImagenUrl,
                Seleccion = respuestas.FirstOrDefault(r => r.OpcionMenuId == o.Id)?.Seleccion
            }).ToList()
        };
    }
}
