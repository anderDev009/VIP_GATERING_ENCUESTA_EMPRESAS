using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

    public async Task<IActionResult> Index(int? empresaId, int? sucursalId, int? localizacionId, string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Empleados
            .Include(e => e.Sucursal).ThenInclude(s => s!.Empresa)
            .Include(e => e.LocalizacionesAsignadas).ThenInclude(l => l.Localizacion)
            .Where(e => !e.Borrado)
            .AsQueryable();
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
            query = query.Where(e =>
                (e.Nombre != null && e.Nombre.ToLower().Contains(ql))
                || (e.Codigo != null && e.Codigo.ToLower().Contains(ql)));
        }
        var localizacionFiltro = localizacionId != null
            ? await _db.Localizaciones
                .AsNoTracking()
                .Include(l => l.Sucursal)
                .FirstOrDefaultAsync(l => l.Id == localizacionId.Value)
            : null;
        if (localizacionId != null)
        {
            if (localizacionFiltro == null)
            {
                query = query.Where(e => false);
            }
            else if (sucursalId != null)
            {
                query = query.Where(e => e.LocalizacionesAsignadas.Any(l => l.LocalizacionId == localizacionId));
            }
            else
            {
                var nombreFiltro = localizacionFiltro.Nombre.Trim();
                var empresaFiltro = empresaId ?? localizacionFiltro.Sucursal?.EmpresaId;
                if (empresaFiltro != null)
                {
                    query = query.Where(e => e.LocalizacionesAsignadas.Any(l =>
                        l.Localizacion != null
                        && l.Localizacion.Sucursal != null
                        && l.Localizacion.Sucursal.EmpresaId == empresaFiltro
                        && l.Localizacion.Nombre.Equals(nombreFiltro, StringComparison.OrdinalIgnoreCase)));
                }
                else
                {
                    query = query.Where(e => e.LocalizacionesAsignadas.Any(l =>
                        l.Localizacion != null
                        && l.Localizacion.Nombre.Equals(nombreFiltro, StringComparison.OrdinalIgnoreCase)));
                }
            }
        }
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Sucursales = await _db.Sucursales.OrderBy(s => s.Nombre).ToListAsync();
        var localizacionesQuery = _db.Localizaciones.Include(l => l.Sucursal).AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaActual = _currentUser.EmpresaId;
            if (empresaActual != null)
                localizacionesQuery = localizacionesQuery.Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == empresaActual);
        }
        if (empresaId != null)
            localizacionesQuery = localizacionesQuery.Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == empresaId);
        if (sucursalId != null)
            localizacionesQuery = localizacionesQuery.Where(l => l.SucursalId == sucursalId);
        var localizacionesList = await localizacionesQuery.OrderBy(l => l.Nombre).ToListAsync();
        ViewBag.Localizaciones = DistinctLocalizaciones(localizacionesList);
        ViewBag.EmpresaId = empresaId; ViewBag.SucursalId = sucursalId; ViewBag.LocalizacionId = localizacionId; ViewBag.Q = q;
        var paged = await query.OrderBy(e => e.Nombre).ToPagedResultAsync(page, pageSize);
        // Usuario por empleado en pagina
        var ids = paged.Items.Select(i => i.Id).ToList();
        var usuarios = await _db.Set<ApplicationUser>()
            .Where(u => u.EmpleadoId != null && ids.Contains(u.EmpleadoId.Value))
            .Select(u => u.EmpleadoId!.Value)
            .ToListAsync();
        ViewBag.EmpleadoUsuarioIds = usuarios;
        return View(paged);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(int? empresaId, int? sucursalId, int? localizacionId, string? q)
    {
        var empleados = await BuildExportQuery(empresaId, sucursalId, localizacionId, q)
            .OrderBy(e => e.Nombre ?? e.Codigo)
            .ToListAsync();
        var headers = new[] { "Codigo", "Nombre", "Empresa", "Filial", "Estado", "Subsidio", "Localizacion" };
        var rows = empleados.Select(e => (IReadOnlyList<string>)new[]
        {
            e.Codigo ?? string.Empty,
            e.Nombre ?? string.Empty,
            e.Sucursal?.Empresa?.Nombre ?? string.Empty,
            e.Sucursal?.Nombre ?? string.Empty,
            e.Estado.ToString(),
            e.EsSubsidiado ? "Si" : "No",
            e.LocalizacionesAsignadas.FirstOrDefault()?.Localizacion?.Nombre ?? string.Empty
        }).ToList();
        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", "empleados.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(int? empresaId, int? sucursalId, int? localizacionId, string? q)
    {
        var empleados = await BuildExportQuery(empresaId, sucursalId, localizacionId, q)
            .OrderBy(e => e.Nombre ?? e.Codigo)
            .ToListAsync();
        var headers = new[] { "Codigo", "Nombre", "Empresa", "Filial", "Estado", "Subsidio", "Localizacion" };
        var rows = empleados.Select(e => (IReadOnlyList<string>)new[]
        {
            e.Codigo ?? string.Empty,
            e.Nombre ?? string.Empty,
            e.Sucursal?.Empresa?.Nombre ?? string.Empty,
            e.Sucursal?.Nombre ?? string.Empty,
            e.Estado.ToString(),
            e.EsSubsidiado ? "Si" : "No",
            e.LocalizacionesAsignadas.FirstOrDefault()?.Localizacion?.Nombre ?? string.Empty
        }).ToList();
        var bytes = ExportHelper.BuildExcel("Empleados", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "empleados.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(int? empresaId, int? sucursalId, int? localizacionId, string? q)
    {
        var empleados = await BuildExportQuery(empresaId, sucursalId, localizacionId, q)
            .OrderBy(e => e.Nombre ?? e.Codigo)
            .ToListAsync();
        var headers = new[] { "Codigo", "Nombre", "Empresa", "Filial", "Estado", "Subsidio", "Localizacion" };
        var rows = empleados.Select(e => (IReadOnlyList<string>)new[]
        {
            e.Codigo ?? string.Empty,
            e.Nombre ?? string.Empty,
            e.Sucursal?.Empresa?.Nombre ?? string.Empty,
            e.Sucursal?.Nombre ?? string.Empty,
            e.Estado.ToString(),
            e.EsSubsidiado ? "Si" : "No",
            e.LocalizacionesAsignadas.FirstOrDefault()?.Localizacion?.Nombre ?? string.Empty
        }).ToList();
        var pdf = ExportHelper.BuildPdf("Empleados", headers, rows);
        return File(pdf, "application/pdf", "empleados.pdf");
    }

    public async Task<IActionResult> Create(int? empresaId = null, int? sucursalId = null)
    {
        var empresas = _db.Empresas.AsQueryable();
        var sucursales = _db.Sucursales.AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _currentUser.EmpresaId;
            empresas = empresas.Where(e => e.Id == currentEmpresaId);
            sucursales = sucursales.Where(s => s.EmpresaId == currentEmpresaId);
        }
        if (!User.IsInRole("Empresa") && empresaId != null)
        {
            sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
        }
        if (!User.IsInRole("Empresa") && sucursalId != null && empresaId == null)
        {
            empresaId = await _db.Sucursales
                .Where(s => s.Id == sucursalId)
                .Select(s => (int?)s.EmpresaId)
                .FirstOrDefaultAsync();
            if (empresaId != null)
                sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
        }
        ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Sucursales = await sucursales.OrderBy(s => s.Nombre).ToListAsync();
        var localizacionesQuery = _db.Localizaciones.Include(l => l.Sucursal).AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _currentUser.EmpresaId;
            if (currentEmpresaId != null)
                localizacionesQuery = localizacionesQuery.Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == currentEmpresaId);
        }
        var localizacionesList = await localizacionesQuery.OrderBy(l => l.Nombre).ToListAsync();
        ViewBag.Localizaciones = DistinctLocalizaciones(localizacionesList);
        ViewBag.LocalizacionAsignadaId = null;
        ViewBag.EmpresaId = User.IsInRole("Empresa") ? _currentUser.EmpresaId : empresaId;
        return View(new Empleado { SucursalId = sucursalId ?? 0, EsSubsidiado = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Empleado model, [FromForm] int? localizacionAsignadaId)
    {
        ApplySubsidioEmpleadoFromForm(model);
        async Task<IActionResult> ReturnInvalidAsync()
        {
            var empresas = _db.Empresas.AsQueryable();
            var sucursales = _db.Sucursales.AsQueryable();
            var localizacionesQuery = _db.Localizaciones.Include(l => l.Sucursal).AsQueryable();
            if (User.IsInRole("Empresa"))
            {
                var empresaId = _currentUser.EmpresaId;
                empresas = empresas.Where(e => e.Id == empresaId);
                sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
                if (empresaId != null)
                    localizacionesQuery = localizacionesQuery.Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == empresaId);
            }
            ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Sucursales = await sucursales.OrderBy(s => s.Nombre).ToListAsync();
            var localizacionesList = await localizacionesQuery.OrderBy(l => l.Nombre).ToListAsync();
            ViewBag.Localizaciones = DistinctLocalizaciones(localizacionesList);
            ViewBag.LocalizacionAsignadaId = localizacionAsignadaId;
            ViewBag.EmpresaId = model.SucursalId != 0
                ? await _db.Sucursales.Where(s => s.Id == model.SucursalId).Select(s => (int?)s.EmpresaId).FirstOrDefaultAsync()
                : null;
            return View(model);
        }
        if (string.IsNullOrWhiteSpace(model.Codigo))
            ModelState.AddModelError(string.Empty, "Por favor ingresa el codigo del empleado.");
        else
            ValidateCodigoEmpleado(model.Codigo, ModelState);
        if (!ModelState.IsValid)
            return await ReturnInvalidAsync();
        // Seguridad: Empresa solo puede crear en sus sucursales
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            var sucEmpresaId = await _db.Sucursales.Where(s => s.Id == model.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
            if (empresaId == null || sucEmpresaId != empresaId) return Forbid();
        }
        if (!string.IsNullOrWhiteSpace(model.Codigo) && await CodigoEmpleadoEnUsoAsync(model.Codigo, model.SucursalId))
        {
            ModelState.AddModelError("Codigo", "No se puede agregar un empleado con ese codigo porque ya existe en la empresa.");
            return await ReturnInvalidAsync();
        }
        model.Estado = EmpleadoEstado.Habilitado;

        var localizacionId = localizacionAsignadaId.HasValue && localizacionAsignadaId.Value != 0
            ? localizacionAsignadaId.Value
            : (int?)null;
        if (localizacionId.HasValue)
        {
            var empresaPrimaria = await _db.Sucursales
                .Where(s => s.Id == model.SucursalId)
                .Select(s => s.EmpresaId)
                .FirstOrDefaultAsync();
            var loc = await _db.Localizaciones
                .AsNoTracking()
                .Include(l => l.Sucursal)
                .Where(l => l.Id == localizacionId.Value)
                .Select(l => new { l.Id, EmpresaId = l.Sucursal != null ? l.Sucursal.EmpresaId : 0 })
                .FirstOrDefaultAsync();
            if (loc == null || loc.EmpresaId != empresaPrimaria)
            {
                ModelState.AddModelError(string.Empty, "La localizacion por defecto debe pertenecer a la empresa del empleado.");
                return await ReturnInvalidAsync();
            }
        }

        await _db.Empleados.AddAsync(model);
        await _db.SaveChangesAsync();
        var codigoUsuario = model.Codigo?.Trim();
        string? passwordMessage = null;
        if (!string.IsNullOrWhiteSpace(codigoUsuario))
        {
            var result = await TryCreateIdentityUsuarioAsync(model.Id, codigoUsuario, model.SucursalId);
            if (!string.IsNullOrEmpty(result.Error))
            {
                passwordMessage = result.Error;
            }
            else
            {
                var defaultPassword = BuildDefaultPassword(null, codigoUsuario, model.Id);
                passwordMessage = $"Contrasena inicial creada automaticamente: {defaultPassword}.";
            }
        }
        if (localizacionId.HasValue)
        {
            await _db.EmpleadosLocalizaciones.AddAsync(new EmpleadoLocalizacion
            {
                EmpleadoId = model.Id,
                LocalizacionId = localizacionId.Value
            });
            await _db.SaveChangesAsync();
        }
        var empresas = _db.Empresas.AsQueryable();
        var sucursales = _db.Sucursales.AsQueryable();
        var localizacionesQuery = _db.Localizaciones.Include(l => l.Sucursal).AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            empresas = empresas.Where(e => e.Id == empresaId);
            sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
            if (empresaId != null)
                localizacionesQuery = localizacionesQuery.Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == empresaId);
        }
        ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Sucursales = await sucursales.OrderBy(s => s.Nombre).ToListAsync();
        var localizacionesList = await localizacionesQuery.OrderBy(l => l.Nombre).ToListAsync();
        ViewBag.Localizaciones = DistinctLocalizaciones(localizacionesList);
        ViewBag.LocalizacionAsignadaId = null;
        ViewBag.EmpresaId = await _db.Sucursales.Where(s => s.Id == model.SucursalId).Select(s => (int?)s.EmpresaId).FirstOrDefaultAsync();
        ViewBag.SuccessMessage = "Empleado agregado con exito.";
        ViewBag.PasswordMessage = passwordMessage;
        ViewBag.ShowPasswordModal = false;
        ModelState.Clear();
        return View(new Empleado { SucursalId = model.SucursalId, EsSubsidiado = true, Codigo = null, Nombre = null });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var ent = await _db.Empleados
            .Include(e => e.Sucursal).ThenInclude(s => s!.Empresa)
            .FirstOrDefaultAsync(e => e.Id == id);
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
        var localizacionesQuery = _db.Localizaciones.Include(l => l.Sucursal).AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            if (empresaId != null)
                localizacionesQuery = localizacionesQuery.Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == empresaId);
        }
        var localizacionAsignadaId = await _db.EmpleadosLocalizaciones
            .Where(el => el.EmpleadoId == ent.Id)
            .Select(el => (int?)el.LocalizacionId)
            .FirstOrDefaultAsync();
        var localizacionesList = await localizacionesQuery.OrderBy(l => l.Nombre).ToListAsync();
        ViewBag.Localizaciones = DistinctLocalizaciones(localizacionesList);
        ViewBag.LocalizacionAsignadaId = localizacionAsignadaId;
        var user = await _db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.EmpleadoId == ent.Id);
        ViewBag.UsuarioExiste = user != null;
        ViewBag.UsuarioNombre = user?.UserName;
        ViewBag.UsuarioSugerido = BuildUserName(ent.Sucursal?.Empresa?.Nombre ?? "Empresa", ent.Codigo, ent.Id);
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Empleado model, [FromForm] int? localizacionAsignadaId)
    {
        ApplySubsidioEmpleadoFromForm(model);
        async Task<IActionResult> ReturnInvalidAsync()
        {
            var empresas = _db.Empresas.AsQueryable();
            var sucursales = _db.Sucursales.AsQueryable();
            var localizacionesQuery = _db.Localizaciones.Include(l => l.Sucursal).AsQueryable();
            if (User.IsInRole("Empresa"))
            {
                var empresaId = _currentUser.EmpresaId;
                empresas = empresas.Where(e => e.Id == empresaId);
                sucursales = sucursales.Where(s => s.EmpresaId == empresaId);
                if (empresaId != null)
                    localizacionesQuery = localizacionesQuery.Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == empresaId);
            }
            ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Sucursales = await sucursales.OrderBy(s => s.Nombre).ToListAsync();
            var localizacionesList = await localizacionesQuery.OrderBy(l => l.Nombre).ToListAsync();
            ViewBag.Localizaciones = DistinctLocalizaciones(localizacionesList);
            ViewBag.LocalizacionAsignadaId = localizacionAsignadaId;
            var user = await _db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.EmpleadoId == model.Id);
            ViewBag.UsuarioExiste = user != null;
            ViewBag.UsuarioNombre = user?.UserName;
            var empresaNombre = await _db.Sucursales
                .Where(s => s.Id == model.SucursalId)
                .Select(s => s.Empresa != null ? s.Empresa.Nombre : "Empresa")
                .FirstOrDefaultAsync() ?? "Empresa";
            ViewBag.UsuarioSugerido = BuildUserName(empresaNombre, model.Codigo, model.Id);
            return View(model);
        }
        if (!ModelState.IsValid)
            return await ReturnInvalidAsync();
        var ent = await _db.Empleados.FindAsync(id);
        if (ent == null) return NotFound();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            var newSucEmpresaId = await _db.Sucursales.Where(s => s.Id == model.SucursalId).Select(s => s.EmpresaId).FirstOrDefaultAsync();
            if (empresaId == null || newSucEmpresaId != empresaId) return Forbid();
        }
        var codigoNuevo = model.Codigo?.Trim();
        var codigoAnterior = ent.Codigo?.Trim();
        var codigoCambio = !string.Equals(codigoAnterior, codigoNuevo, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(codigoNuevo))
            ValidateCodigoEmpleado(codigoNuevo, ModelState);
        if (!string.IsNullOrWhiteSpace(codigoNuevo) && await CodigoEmpleadoEnUsoAsync(codigoNuevo, model.SucursalId, ent.Id))
        {
            ModelState.AddModelError("Codigo", "Ya existe un empleado con ese codigo en la misma empresa.");
            return await ReturnInvalidAsync();
        }
        ent.Codigo = codigoNuevo;
        ent.Nombre = model.Nombre;
        ent.SucursalId = model.SucursalId;
        ent.Estado = EmpleadoEstado.Habilitado;
        ent.EsSubsidiado = model.EsSubsidiado;
        ent.SubsidioTipo = model.SubsidioTipo;
        ent.SubsidioValor = model.SubsidioValor;

        var localizacionId = localizacionAsignadaId.HasValue && localizacionAsignadaId.Value != 0
            ? localizacionAsignadaId.Value
            : (int?)null;
        if (localizacionId.HasValue)
        {
            var empresaPrimaria = await _db.Sucursales
                .Where(s => s.Id == ent.SucursalId)
                .Select(s => s.EmpresaId)
                .FirstOrDefaultAsync();
            var loc = await _db.Localizaciones
                .AsNoTracking()
                .Include(l => l.Sucursal)
                .Where(l => l.Id == localizacionId.Value)
                .Select(l => new { l.Id, EmpresaId = l.Sucursal != null ? l.Sucursal.EmpresaId : 0 })
                .FirstOrDefaultAsync();
            if (loc == null || loc.EmpresaId != empresaPrimaria)
            {
                ModelState.AddModelError(string.Empty, "La localizacion por defecto debe pertenecer a la empresa del empleado.");
                return await ReturnInvalidAsync();
            }
        }
        var actuales = await _db.EmpleadosSucursales.Where(es => es.EmpleadoId == ent.Id).ToListAsync();
        if (actuales.Count > 0) _db.EmpleadosSucursales.RemoveRange(actuales);

        var actualesLoc = await _db.EmpleadosLocalizaciones.Where(el => el.EmpleadoId == ent.Id).ToListAsync();
        if (actualesLoc.Count > 0) _db.EmpleadosLocalizaciones.RemoveRange(actualesLoc);
        if (localizacionId.HasValue)
        {
            await _db.EmpleadosLocalizaciones.AddAsync(new EmpleadoLocalizacion
            {
                EmpleadoId = ent.Id,
                LocalizacionId = localizacionId.Value
            });
        }

        await _db.SaveChangesAsync();
        if (codigoCambio)
        {
            var user = await _db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.EmpleadoId == ent.Id);
            if (user != null)
            {
                var empresaNombre = await _db.Sucursales
                    .Where(s => s.Id == ent.SucursalId)
                    .Select(s => s.Empresa != null ? s.Empresa.Nombre : "Empresa")
                    .FirstOrDefaultAsync() ?? "Empresa";
                var nuevoUsuario = BuildUserName(empresaNombre, ent.Codigo, ent.Id);
                if (!string.Equals(user.UserName, nuevoUsuario, StringComparison.OrdinalIgnoreCase))
                {
                    user.UserName = nuevoUsuario;
                    user.NormalizedUserName = _userManager.NormalizeName(nuevoUsuario);
                    var updateRes = await _userManager.UpdateAsync(user);
                    if (!updateRes.Succeeded)
                        TempData["Error"] = "No se pudo actualizar el nombre de usuario del empleado.";
                }
            }
        }
        TempData["Success"] = "Empleado actualizado.";
        return RedirectToAction(nameof(Index), new { sucursalId = model.SucursalId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
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

    private static List<Localizacion> DistinctLocalizaciones(List<Localizacion> localizaciones, IEnumerable<int>? seleccionadas = null)
    {
        if (localizaciones.Count == 0) return localizaciones;
        var selected = seleccionadas != null ? new HashSet<int>(seleccionadas) : new HashSet<int>();
        var grupos = localizaciones
            .GroupBy(l => new
            {
                EmpresaId = l.Sucursal?.EmpresaId ?? 0,
                Nombre = l.Nombre.Trim().ToUpperInvariant()
            })
            .ToList();

        var resultado = new List<Localizacion>();
        foreach (var g in grupos)
        {
            var elegido = g.FirstOrDefault(l => selected.Contains(l.Id)) ?? g.First();
            resultado.Add(elegido);
        }
        return resultado;
    }

    private IQueryable<Empleado> BuildExportQuery(int? empresaId, int? sucursalId, int? localizacionId, string? q)
    {
        var query = _db.Empleados
            .Include(e => e.Sucursal).ThenInclude(s => s!.Empresa)
            .Include(e => e.LocalizacionesAsignadas).ThenInclude(l => l.Localizacion)
            .Where(e => !e.Borrado)
            .AsQueryable();

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
            query = query.Where(e =>
                (e.Nombre != null && e.Nombre.ToLower().Contains(ql))
                || (e.Codigo != null && e.Codigo.ToLower().Contains(ql)));
        }

        if (localizacionId != null)
        {
            query = query.Where(e => e.LocalizacionesAsignadas.Any(l => l.LocalizacionId == localizacionId));
        }

        return query;
    }

    // Atajo: simular sesion del usuario del empleado y abrir "Mi semana"
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerSemana(int id)
    {
        var usuario = await _empUserService.EnsureUsuarioParaEmpleadoAsync(id);
        await _currentUser.SetUsuarioAsync(usuario.Id);
        return RedirectToAction("MiSemana", "Empleado");
    }

    // Vista de semana para Admin/Empresa (solo lectura) de un empleado especifico
    [HttpGet]
    public async Task<IActionResult> Semana(int id)
    {
        // Validar alcance para rol Empresa
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _currentUser.EmpresaId;
            var empEmpresaId = await _db.Empleados.Include(e => e.Sucursal).Where(e => e.Id == id)
                .Select(e => e.Sucursal!.EmpresaId).FirstOrDefaultAsync();
            if (empresaId == null || empEmpresaId != empresaId) return Forbid();
        }

        var modelo = await ConstruirSemanaEmpleadoAsync(id, false);
        if (modelo == null) return NotFound();
        return View("Semana", modelo);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> EditarSemana(int id)
    {
        var modelo = await ConstruirSemanaEmpleadoAsync(id, true);
        if (modelo == null) return NotFound();
        if (!modelo.EsJefe)
        {
            TempData["Error"] = "El empleado no esta marcado como jefe.";
            return RedirectToAction(nameof(Semana), new { id });
        }
        return View("EditarSemana", modelo);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarSemanaJefe(int empleadoId, SemanaEmpleadoVM model)
    {
        var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado == null) return NotFound();
        if (!empleado.EsJefe)
        {
            TempData["Error"] = "El empleado no esta marcado como jefe.";
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

        var empresaId = await _db.Sucursales
            .AsNoTracking()
            .Where(s => s.Id == empleado.SucursalId)
            .Select(s => s.EmpresaId)
            .FirstAsync();
        var sucursalesPermitidas = await _db.Sucursales
            .AsNoTracking()
            .Where(s => s.EmpresaId == empresaId)
            .Select(s => s.Id)
            .ToListAsync();
        var sucursalesPermitidasSet = sucursalesPermitidas.ToHashSet();

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
                if (!sucursalesPermitidasSet.Contains(sucursalEntregaId))
                    sucursalEntregaId = empleado.SucursalId;

                var localizacionEntregaId = existente?.LocalizacionEntregaId;
                int? adicionalOpcionId = existente?.AdicionalOpcionId;
                if (adicionalOpcionId != null && !setAdicionales.Contains(adicionalOpcionId.Value))
                    adicionalOpcionId = null;

                await _menuService.RegistrarSeleccionAsync(empleadoId, d.OpcionMenuId, d.Seleccion.Value, sucursalEntregaId, localizacionEntregaId, adicionalOpcionId);
            }
        }

        TempData["Success"] = "Selecciones actualizadas para el empleado jefe.";
        return RedirectToAction(nameof(Semana), new { id = empleadoId });
    }

    // Crear usuario de Identity para un empleado (solo Admin o Empresa dentro de su empresa)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearUsuario(int id)
    {
        var empleado = await _db.Empleados
            .Include(e => e.Sucursal).ThenInclude(s => s!.Empresa)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (empleado == null) return NotFound();
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _currentUser.EmpresaId;
            if (currentEmpresaId == null || empleado.Sucursal!.EmpresaId != currentEmpresaId) return Forbid();
        }

        var codigo = empleado.Codigo?.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            TempData["Error"] = "El codigo del empleado es requerido para generar el usuario.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var resultMessage = await TryCreateIdentityUsuarioAsync(empleado.Id, codigo, empleado.SucursalId);
        if (string.IsNullOrEmpty(resultMessage.Error))
        {
            var defaultPassword = BuildDefaultPassword(null, codigo, empleado.Id);
            TempData["Success"] = $"Usuario creado automaticamente ({codigo}). La contrasena inicial es {defaultPassword}.";
        }
        else
        {
            TempData["Error"] = resultMessage.Error;
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    // Reset de contrasena (Admin o Empresa sobre su empleado)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, string newPassword, string confirmPassword, bool stayOnCreate = false, int? empresaId = null, int? sucursalId = null)
    {
        var empleado = await _db.Empleados.Include(e => e.Sucursal).FirstOrDefaultAsync(e => e.Id == id);
        if (empleado == null) return NotFound();
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _currentUser.EmpresaId;
            if (currentEmpresaId == null || empleado.Sucursal!.EmpresaId != currentEmpresaId) return Forbid();
        }
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["Error"] = "La nueva contrasena es requerida.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "La confirmacion no coincide.";
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
        // Marcar para cambio en primer inicio (via claim)
        var claims = await _userManager.GetClaimsAsync(user);
        var mc = claims.FirstOrDefault(c => c.Type == "must_change_password");
        if (mc == null)
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("must_change_password", "1"));
        await _userManager.UpdateAsync(user);
        TempData["Success"] = "Contrasena asignada. Se pedira cambio al iniciar sesion.";
        if (stayOnCreate)
            return RedirectToAction(nameof(Create), new { empresaId, sucursalId });
        return RedirectToAction(nameof(Edit), new { id });
    }

    // Validacion remota: email disponible (para creacion de usuario)

    private void ApplySubsidioEmpleadoFromForm(Empleado target)
    {
        var scope = Request.Form["subsidioEmpleadoScope"].FirstOrDefault();
        if (!string.Equals(scope, "custom", StringComparison.OrdinalIgnoreCase))
        {
            target.SubsidioTipo = null;
            target.SubsidioValor = null;
            return;
        }

        if (Enum.TryParse<SubsidioTipo>(Request.Form["CustomEmpleadoSubsidioTipo"].FirstOrDefault(), out var tipo))
        {
            target.SubsidioTipo = tipo;
        }
        else
        {
            ModelState.AddModelError("SubsidioTipo", "Selecciona el tipo de subsidio.");
        }

        var valorStr = Request.Form["CustomEmpleadoSubsidioValor"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(valorStr) && decimal.TryParse(valorStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var valorInv))
        {
            target.SubsidioValor = valorInv;
        }
        else if (!string.IsNullOrWhiteSpace(valorStr) && decimal.TryParse(valorStr, NumberStyles.Number, CultureInfo.CurrentCulture, out var valorCult))
        {
            target.SubsidioValor = valorCult;
        }
        else
        {
            ModelState.AddModelError("SubsidioValor", "Valor de subsidio invalido.");
        }
    }

    private async Task<(string? Error, bool RequiresManualPassword)> TryCreateIdentityUsuarioAsync(int empleadoId, string username, int sucursalId)
    {
        if (string.IsNullOrWhiteSpace(username))
            return ("El codigo es requerido para crear el usuario.", false);
        username = username.Trim();

        if (await _db.Set<ApplicationUser>().AnyAsync(u => u.EmpleadoId == empleadoId))
            return ("El empleado ya tiene un usuario asignado.", false);

        if (await _userManager.FindByNameAsync(username) != null)
            return ($"Ya existe un usuario con el codigo {username}.", false);

        var sucursalData = await _db.Sucursales
            .Where(s => s.Id == sucursalId)
            .Select(s => new { s.EmpresaId, s.Nombre })
            .FirstOrDefaultAsync();
        var empresaId = sucursalData?.EmpresaId;
        var sucursalNombre = sucursalData?.Nombre;

        var usuario = new ApplicationUser
        {
            UserName = username,
            EmailConfirmed = true,
            EmpleadoId = empleadoId,
            EmpresaId = empresaId
        };

        var password = BuildDefaultPassword(null, username, empleadoId);
        var result = await _userManager.CreateAsync(usuario, password);
        if (!result.Succeeded)
            return ($"No se pudo crear el usuario: {string.Join("; ", result.Errors.Select(e => e.Description))}", false);

        if (!await _userManager.IsInRoleAsync(usuario, "Empleado"))
            await _userManager.AddToRoleAsync(usuario, "Empleado");

        return (null, false);
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

    private async Task<bool> CodigoEmpleadoEnUsoAsync(string codigo, int sucursalId, int? excluirEmpleadoId = null)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return false;
        var clave = codigo.Trim().ToUpperInvariant();
        var sucursalData = await _db.Sucursales
            .Where(s => s.Id == sucursalId)
            .Select(s => new { s.EmpresaId, s.Nombre })
            .FirstOrDefaultAsync();
        var empresaId = sucursalData?.EmpresaId;
        var sucursalNombre = sucursalData?.Nombre;
        if (empresaId == null) return false;

        var query = _db.Empleados
            .Where(e => !e.Borrado && e.Codigo != null && e.Codigo.ToUpper() == clave && e.Sucursal != null && e.Sucursal.EmpresaId == empresaId);
        if (excluirEmpleadoId.HasValue)
            query = query.Where(e => e.Id != excluirEmpleadoId.Value);
        return await query.AnyAsync();
    }

    private static void ValidateCodigoEmpleado(string codigo, ModelStateDictionary modelState)
    {
        var trimmed = codigo.Trim();
        if (trimmed.Length < 3)
            modelState.AddModelError("Codigo", "El codigo debe tener al menos 3 caracteres.");
        if (!trimmed.All(char.IsLetterOrDigit))
            modelState.AddModelError("Codigo", "El codigo solo puede contener letras y numeros sin espacios ni simbolos.");
    }


    private static string BuildUserName(string empresaNombre, string? empleadoCodigo, int empleadoId)
    {
        var codigo = ToToken(empleadoCodigo);
        if (string.IsNullOrWhiteSpace(codigo))
            codigo = empleadoId.ToString().PadLeft(6, '0');

        return EnsurePasswordCompliance(codigo);
    }

    private static string BuildDefaultPassword(string? sucursalNombre, string codigo, int empleadoId)
    {
        var codigoToken = ToToken(codigo);
        if (string.IsNullOrWhiteSpace(codigoToken))
            codigoToken = empleadoId.ToString().PadLeft(6, '0');
        var password = $"UNI{codigoToken}@";
        if (password.Length < 6)
            password = password.PadRight(6, '0');
        return password;
    }

    private static string ToTitleToken(string value)
    {
        var cleaned = RemoveDiacritics(value ?? string.Empty);
        var parts = Regex.Split(cleaned, "[^A-Za-z0-9]+");
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.Substring(1).ToLowerInvariant());
        }
        return sb.Length == 0 ? "Filial" : sb.ToString();
    }

    private static string ToToken(string? value)
    {
        var cleaned = RemoveDiacritics(value ?? string.Empty);
        var chars = cleaned.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToUpperInvariant();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string EnsurePasswordCompliance(string value)
    {
        var result = value;
        if (!result.Any(char.IsLower)) result += "a";
        if (!result.Any(char.IsDigit)) result += "1";
        if (!result.Any(ch => !char.IsLetterOrDigit(ch))) result += "_";
        if (result.Length < 8) result += new string('0', 8 - result.Length);
        return result;
    }


    private async Task<SemanaEmpleadoVM?> ConstruirSemanaEmpleadoAsync(int empleadoId, bool paraAdministrador)
    {
        var info = await _db.Empleados
            .Include(e => e.Sucursal).ThenInclude(s => s!.Empresa)
            .Where(e => e.Id == empleadoId)
            .Select(e => new
            {
                e.Id,
                e.Nombre,
                e.Codigo,
                e.EsJefe,
                e.SucursalId,
                SucursalNombre = e.Sucursal!.Nombre,
                EmpresaId = e.Sucursal!.EmpresaId,
                EmpresaNombre = e.Sucursal!.Empresa!.Nombre,
                e.EsSubsidiado,
                EmpleadoSubsidioTipo = e.SubsidioTipo,
                EmpleadoSubsidioValor = e.SubsidioValor,
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

        // Si el empleado ya tiene respuestas para la semana, inferir el menu y la sucursal de entrega desde esas respuestas
        var respuestaSemana = await _db.RespuestasFormulario
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Where(r => r.EmpleadoId == empleadoId
                && r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio == inicio
                && r.OpcionMenu.Menu.FechaTermino == fin)
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
            if (om == null) continue;
            var opcion = GetOpcionSeleccionada(om, r.Seleccion);
            if (opcion == null) continue;
            var ctx = new SubsidioContext(opcion.EsSubsidiado, info.EsSubsidiado, info.EmpresaSubsidia, info.EmpresaTipo, info.EmpresaValor, info.SucursalSubsidia, info.SucursalTipo, info.SucursalValor, info.EmpleadoSubsidioTipo, info.EmpleadoSubsidioValor);
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

        var empleadoNombre = GetEmpleadoDisplayName(info.Nombre, info.Codigo);
        return new SemanaEmpleadoVM
        {
            EmpleadoId = info.Id,
            EmpleadoNombre = empleadoNombre,
            EmpleadoCodigo = info.Codigo,
            MenuId = menu.Id,
            FechaInicio = menu.FechaInicio,
            FechaTermino = menu.FechaTermino,
            SucursalEntregaId = sucursalEntregaId,
            Bloqueado = encuestaCerrada,
            MensajeBloqueo = encuestaCerrada ? $"El menu esta cerrado desde {fechaCierreAuto:dd/MM/yyyy}." : null,
            RespuestasCount = respuestas.Count,
            TotalDias = opciones.Count,
            OrigenMenu = menu.SucursalId != null ? "Filial" : "Empresa",
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

    private static string GetEmpleadoDisplayName(string? nombre, string? codigo)
    {
        if (!string.IsNullOrWhiteSpace(nombre)) return nombre;
        if (!string.IsNullOrWhiteSpace(codigo)) return codigo;
        return "Sin nombre";
    }
}

