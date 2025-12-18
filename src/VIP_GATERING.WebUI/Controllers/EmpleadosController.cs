using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;
using VIP_GATERING.Infrastructure.Services;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.WebUI.Services;

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
    private readonly ISubsidioService _subsidios;
    public EmpleadosController(AppDbContext db, ICurrentUserService currentUser, IEmpleadoUsuarioService empUserService, UserManager<ApplicationUser> userManager, IMenuService menuService, IEncuestaCierreService cierre, ISubsidioService subsidios)
    { _db = db; _currentUser = currentUser; _empUserService = empUserService; _userManager = userManager; _menuService = menuService; _cierre = cierre; _subsidios = subsidios; }

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
        if (sucursalId != null) query = query.Where(e => e.SucursalId == sucursalId || e.SucursalesAsignadas.Any(a => a.SucursalId == sucursalId));
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
        ViewBag.SucursalesAsignadasIds = new List<Guid>();
        return View(new Empleado());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Empleado model, [FromForm] List<Guid> sucursalesAsignadas)
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
            ViewBag.SucursalesAsignadasIds = sucursalesAsignadas ?? new List<Guid>();
            return View(model);
        }
        // Seguridad: Empresa solo puede crear en sus sucursales
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            var sucEmpresaId = await _db.Sucursales.Where(s => s.Id == model.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
            if (empresaId == null || sucEmpresaId != empresaId) return Forbid();
        }

        var extras = (sucursalesAsignadas ?? new List<Guid>())
            .Where(x => x != Guid.Empty && x != model.SucursalId)
            .Distinct()
            .ToList();
        if (extras.Count > 0)
        {
            var empresaPrimaria = await _db.Sucursales.Where(s => s.Id == model.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
            var empresasExtra = await _db.Sucursales.Where(s => extras.Contains(s.Id)).Select(s => s.EmpresaId).Distinct().ToListAsync();
            if (empresasExtra.Any(e => e != empresaPrimaria))
            {
                TempData["Error"] = "Todas las sucursales asignadas deben pertenecer al mismo cliente.";
                return RedirectToAction(nameof(Create));
            }
            if (User.IsInRole("Empresa") && _currentUser.EmpresaId != null && empresaPrimaria != _currentUser.EmpresaId)
                return Forbid();
        }

        await _db.Empleados.AddAsync(model);
        await _db.SaveChangesAsync();

        if (extras.Count > 0)
        {
            var rows = extras.Select(sid => new EmpleadoSucursal { EmpleadoId = model.Id, SucursalId = sid }).ToList();
            await _db.EmpleadosSucursales.AddRangeAsync(rows);
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "Empleado creado.";
        return RedirectToAction(nameof(Index), new { sucursalId = model.SucursalId });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var ent = await _db.Empleados.Include(e => e.Sucursal).FirstOrDefaultAsync(e => e.Id == id);
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
        ViewBag.SucursalesAsignadasIds = await _db.EmpleadosSucursales.Where(es => es.EmpleadoId == ent.Id).Select(es => es.SucursalId).ToListAsync();
        var user = await _db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.EmpleadoId == ent.Id);
        ViewBag.UsuarioExiste = user != null;
        ViewBag.UsuarioEmail = user?.Email;
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Empleado model, [FromForm] List<Guid> sucursalesAsignadas)
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
            ViewBag.SucursalesAsignadasIds = sucursalesAsignadas ?? new List<Guid>();
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
        ent.EsSubsidiado = model.EsSubsidiado;

        var extras = (sucursalesAsignadas ?? new List<Guid>())
            .Where(x => x != Guid.Empty && x != ent.SucursalId)
            .Distinct()
            .ToList();
        if (extras.Count > 0)
        {
            var empresaPrimaria = await _db.Sucursales.Where(s => s.Id == ent.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
            var empresasExtra = await _db.Sucursales.Where(s => extras.Contains(s.Id)).Select(s => s.EmpresaId).Distinct().ToListAsync();
            if (empresasExtra.Any(e => e != empresaPrimaria))
            {
                TempData["Error"] = "Todas las sucursales asignadas deben pertenecer al mismo cliente.";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        var actuales = await _db.EmpleadosSucursales.Where(es => es.EmpleadoId == ent.Id).ToListAsync();
        var actualesSet = actuales.Select(a => a.SucursalId).ToHashSet();
        var nuevosSet = extras.ToHashSet();
        var toRemove = actuales.Where(a => !nuevosSet.Contains(a.SucursalId)).ToList();
        if (toRemove.Count > 0) _db.EmpleadosSucursales.RemoveRange(toRemove);
        var toAdd = nuevosSet.Except(actualesSet).Select(sid => new EmpleadoSucursal { EmpleadoId = ent.Id, SucursalId = sid }).ToList();
        if (toAdd.Count > 0) await _db.EmpleadosSucursales.AddRangeAsync(toAdd);

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

        var opcionIds = model.Dias.Select(d => d.OpcionMenuId).ToList();
        var respuestasActuales = await _db.RespuestasFormulario
            .Where(r => r.EmpleadoId == empleadoId && opcionIds.Contains(r.OpcionMenuId))
            .ToListAsync();
        bool removals = false;
        foreach (var d in model.Dias)
        {
            if (d.Seleccion is not ('A' or 'B' or 'C' or 'D' or 'E'))
            {
                var existente = respuestasActuales.FirstOrDefault(r => r.OpcionMenuId == d.OpcionMenuId);
                if (existente != null)
                {
                    _db.RespuestasFormulario.Remove(existente);
                    removals = true;
                }
            }
        }
        if (removals) await _db.SaveChangesAsync();

        var sucursalesAsignadas = await _db.EmpleadosSucursales
            .AsNoTracking()
            .Where(es => es.EmpleadoId == empleadoId)
            .Select(es => es.SucursalId)
            .ToListAsync();
        var sucursalesPermitidas = sucursalesAsignadas.ToHashSet();
        sucursalesPermitidas.Add(empleado.SucursalId);

        var menuIds = await _db.OpcionesMenu
            .AsNoTracking()
            .Where(om => opcionIds.Contains(om.Id))
            .Select(om => om.MenuId)
            .Distinct()
            .ToListAsync();
        var menuId = menuIds.Count == 1 ? menuIds[0] : model.MenuId;
        var adicionalesPermitidos = await _db.MenusAdicionales
            .AsNoTracking()
            .Where(a => a.MenuId == menuId)
            .Select(a => a.OpcionId)
            .ToListAsync();
        var setAdicionales = adicionalesPermitidos.ToHashSet();

        var respuestasPorOpcion = respuestasActuales
            .GroupBy(r => r.OpcionMenuId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var d in model.Dias)
        {
            if (d.Seleccion is 'A' or 'B' or 'C' or 'D' or 'E')
            {
                respuestasPorOpcion.TryGetValue(d.OpcionMenuId, out var existente);
                var sucursalEntregaId = existente?.SucursalEntregaId ?? empleado.SucursalId;
                if (!sucursalesPermitidas.Contains(sucursalEntregaId))
                    sucursalEntregaId = empleado.SucursalId;

                Guid? adicionalOpcionId = existente?.AdicionalOpcionId;
                if (adicionalOpcionId != null && !setAdicionales.Contains(adicionalOpcionId.Value))
                    adicionalOpcionId = null;

                await _menuService.RegistrarSeleccionAsync(empleadoId, d.OpcionMenuId, d.Seleccion.Value, sucursalEntregaId, adicionalOpcionId);
            }
        }

        TempData["Success"] = "Selecciones actualizadas para el empleado jefe.";
        return RedirectToAction(nameof(Semana), new { id = empleadoId });
    }

    // Crear usuario de Identity para un empleado (solo Admin o Empresa dentro de su empresa)
    [HttpPost]
    [ValidateAntiForgeryToken]
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
            var tempPassword = IdentityDefaults.GetDefaultPassword();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                EmpleadoId = empleado.Id,
                EmpresaId = empleado.Sucursal!.EmpresaId
            };
            var res = await _userManager.CreateAsync(user, tempPassword);
            if (res.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Empleado");
                await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("must_change_password", "1"));
                TempData["Success"] = "Usuario creado con contraseña temporal segura. Se pedirá cambio al iniciar sesión.";
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
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["Error"] = "La nueva contraseña es requerida.";
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
        var validationError = await ValidatePasswordAsync(user, newPassword);
        if (validationError != null)
        {
            TempData["Error"] = validationError;
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

    private async Task<string?> ValidatePasswordAsync(ApplicationUser user, string newPassword)
    {
        var allErrors = new List<IdentityError>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var res = await validator.ValidateAsync(_userManager, user, newPassword);
            if (!res.Succeeded) allErrors.AddRange(res.Errors);
        }
        return allErrors.Count > 0 ? string.Join("; ", allErrors.Select(e => e.Description)) : null;
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
                EmpresaNombre = e.Sucursal!.Empresa!.Nombre,
                e.EsSubsidiado,
                SucursalSubsidia = e.Sucursal!.SubsidiaEmpleados,
                SucursalTipo = e.Sucursal!.SubsidioTipo,
                SucursalValor = e.Sucursal!.SubsidioValor,
                EmpresaSubsidia = e.Sucursal!.Empresa!.SubsidiaEmpleados,
                EmpresaTipo = e.Sucursal!.Empresa!.SubsidioTipo,
                EmpresaValor = e.Sucursal!.Empresa!.SubsidioValor
            })
            .FirstOrDefaultAsync();
        if (info == null) return null;

        var fechas = new FechaServicio();
        var (inicio, fin) = fechas.RangoSemanaSiguiente();

        // Si el empleado ya tiene respuestas para la semana, inferir el menA§ y la sucursal de entrega desde esas respuestas
        var respuestaSemana = await _db.RespuestasFormulario
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Where(r => r.EmpleadoId == empleadoId && r.OpcionMenu!.Menu!.FechaInicio == inicio && r.OpcionMenu.Menu!.FechaTermino == fin)
            .FirstOrDefaultAsync();

        var menu = respuestaSemana?.OpcionMenu?.Menu
            ?? await _menuService.GetEffectiveMenuForSemanaAsync(inicio, fin, info.EmpresaId, info.SucursalId);

        var sucursalEntregaId = respuestaSemana?.SucursalEntregaId ?? info.SucursalId;
        var sucursalEntrega = await _db.Sucursales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sucursalEntregaId);
        var fechaCierreAuto = _cierre.GetFechaCierreAutomatica(menu);
        var encuestaCerrada = _cierre.EstaCerrada(menu);
        var opciones = await _db.OpcionesMenu
            .Include(o => o.OpcionA).Include(o => o.OpcionB).Include(o => o.OpcionC)
            .Include(o => o.OpcionD).Include(o => o.OpcionE)
            .Include(o => o.Horario)
            .Where(o => o.MenuId == menu.Id)
            .OrderBy(o => o.DiaSemana)
            .ToListAsync();
        if (opciones.Count == 0)
        {
            var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            foreach (var d in dias)
                await _db.OpcionesMenu.AddAsync(new OpcionMenu { MenuId = menu.Id, DiaSemana = d });
            await _db.SaveChangesAsync();
            opciones = await _db.OpcionesMenu
                .Include(o => o.OpcionA).Include(o => o.OpcionB).Include(o => o.OpcionC)
                .Include(o => o.OpcionD).Include(o => o.OpcionE)
                .Include(o => o.Horario)
                .Where(o => o.MenuId == menu.Id).OrderBy(o => o.DiaSemana).ToListAsync();
        }
        var opcionIds = opciones.Select(o => o.Id).ToList();
        var respuestas = await _db.RespuestasFormulario
            .Include(r => r.AdicionalOpcion)
            .Where(r => r.EmpleadoId == empleadoId && opcionIds.Contains(r.OpcionMenuId))
            .ToListAsync();
        var totalEmpleado = 0m;
        var totalEmpresa = 0m;
        foreach (var r in respuestas)
        {
            var om = opciones.FirstOrDefault(o => o.Id == r.OpcionMenuId);
            var opcion = GetOpcionSeleccionada(om, r.Seleccion);
            if (opcion == null) continue;
            var ctx = new SubsidioContext(opcion.EsSubsidiado, info.EsSubsidiado, info.EmpresaSubsidia, info.EmpresaTipo, info.EmpresaValor, info.SucursalSubsidia, info.SucursalTipo, info.SucursalValor);
            var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
            totalEmpleado += precio;
            totalEmpresa += opcion.Costo;

            if (r.AdicionalOpcionId != null && r.AdicionalOpcion != null)
            {
                var adicionalPrecio = r.AdicionalOpcion.Precio ?? r.AdicionalOpcion.Costo;
                totalEmpleado += adicionalPrecio;
                totalEmpresa += r.AdicionalOpcion.Costo;
            }
        }

        return new SemanaEmpleadoVM
        {
            EmpleadoId = info.Id,
            EmpleadoNombre = info.Nombre,
            MenuId = menu.Id,
            FechaInicio = menu.FechaInicio,
            FechaTermino = menu.FechaTermino,
            SucursalEntregaId = sucursalEntregaId,
            Bloqueado = encuestaCerrada,
            MensajeBloqueo = encuestaCerrada ? $"La encuesta está cerrada desde {fechaCierreAuto:dd/MM/yyyy}." : null,
            RespuestasCount = respuestas.Count,
            TotalDias = opciones.Count,
            OrigenMenu = menu.SucursalId != null ? "Dependiente" : "Cliente",
            EmpresaNombre = info.EmpresaNombre,
            SucursalNombre = info.SucursalNombre,
            SucursalEntregaNombre = sucursalEntrega?.Nombre ?? info.SucursalNombre,
            EsJefe = info.EsJefe,
            EsVistaAdministrador = paraAdministrador,
            TotalEmpleado = totalEmpleado,
            TotalEmpresa = paraAdministrador ? totalEmpresa : null,
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
                D = o.OpcionD?.Nombre,
                E = o.OpcionE?.Nombre,
                ImagenD = o.OpcionD?.ImagenUrl,
                ImagenE = o.OpcionE?.ImagenUrl,
                OpcionesMaximas = o.OpcionesMaximas == 0 ? 3 : o.OpcionesMaximas,
                AdicionalOpcionId = respuestas.FirstOrDefault(r => r.OpcionMenuId == o.Id)?.AdicionalOpcionId,
                Seleccion = respuestas.FirstOrDefault(r => r.OpcionMenuId == o.Id)?.Seleccion
            }).ToList()
        };
    }

    private static Opcion? GetOpcionSeleccionada(OpcionMenu opcionMenu, char seleccion)
    {
        var max = opcionMenu.OpcionesMaximas == 0 ? 3 : Math.Clamp(opcionMenu.OpcionesMaximas, 1, 5);
        return seleccion switch
        {
            'A' when max >= 1 => opcionMenu.OpcionA,
            'B' when max >= 2 => opcionMenu.OpcionB,
            'C' when max >= 3 => opcionMenu.OpcionC,
            'D' when max >= 4 => opcionMenu.OpcionD,
            'E' when max >= 5 => opcionMenu.OpcionE,
            _ => null
        };
    }
}
