using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Infrastructure.Data;

public static class SeedData
{
    public static async Task EnsureSeedAsync(AppDbContext db, string? contentRootPath = null)
    {
        // Horarios por defecto
        var horariosPermitidos = new (string nombre, int orden)[]
        {
            ("Desayuno", 1),
            ("Almuerzo", 2),
            ("Cena", 3)
        };
        var nombresPermitidos = horariosPermitidos.Select(h => h.nombre).ToArray();

        foreach (var h in horariosPermitidos)
        {
            var existente = await db.Horarios.FirstOrDefaultAsync(x => x.Nombre == h.nombre);
            if (existente == null)
                await db.Horarios.AddAsync(new Horario { Nombre = h.nombre, Orden = h.orden, Activo = true });
            else
            {
                existente.Orden = h.orden;
                existente.Activo = true;
            }
        }

        // Desactivar otros horarios no permitidos
        var otros = await db.Horarios.Where(x => !nombresPermitidos.Contains(x.Nombre)).ToListAsync();
        foreach (var h in otros) h.Activo = false;

        await db.SaveChangesAsync();

        // ConfiguraciÃ³n global de menÃº (Ãºnica)
        if (!await db.ConfiguracionesMenu.AnyAsync())
        {
            await db.ConfiguracionesMenu.AddAsync(new MenuConfiguracion());
            await db.SaveChangesAsync();
        }

        var etlData = LoadMenuEtlData(contentRootPath);

        // Evitar reinsertar data si ya existe una empresa (seed solo una vez),
        // pero permitir sembrar localizaciones ETL y menus puntuales.
        if (await db.Empresas.AnyAsync())
        {
            if (etlData != null)
                await EnsureEtlLocalizacionesAsync(db, etlData);
            return;
        }

        if (etlData?.Empresas != null && etlData.Empresas.Count > 0)
        {
            foreach (var item in etlData.Empresas)
            {
                if (string.IsNullOrWhiteSpace(item.Empresa) || string.IsNullOrWhiteSpace(item.Filial))
                    continue;

                var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Nombre == item.Empresa);
                if (empresa == null)
                {
                    empresa = new Empresa
                    {
                        Nombre = item.Empresa.Trim(),
                        SubsidiaEmpleados = true,
                        SubsidioTipo = SubsidioTipo.Porcentaje,
                        SubsidioValor = item.SubsidioEmpresaPct ?? 75m,
                        ContactoNombre = $"{item.Empresa.Trim()} - Contacto principal",
                        ContactoTelefono = "000-000-0000",
                        Direccion = $"Oficina central de {item.Empresa.Trim()}"
                    };
                    db.Empresas.Add(empresa);
                    await db.SaveChangesAsync();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(empresa.ContactoNombre))
                        empresa.ContactoNombre = $"{empresa.Nombre} - Contacto principal";
                    if (string.IsNullOrWhiteSpace(empresa.ContactoTelefono))
                        empresa.ContactoTelefono = "000-000-0000";
                    if (string.IsNullOrWhiteSpace(empresa.Direccion))
                        empresa.Direccion = $"Oficina central de {empresa.Nombre}";

                    if (item.SubsidioEmpresaPct.HasValue && empresa.SubsidioValor <= 0)
                    {
                        empresa.SubsidiaEmpleados = true;
                        empresa.SubsidioTipo = SubsidioTipo.Porcentaje;
                        empresa.SubsidioValor = item.SubsidioEmpresaPct.Value;
                    }
                }

                var suc = await db.Sucursales.FirstOrDefaultAsync(s => s.Nombre == item.Filial && s.EmpresaId == empresa.Id);
                if (suc == null)
                {
                    suc = new Sucursal
                    {
                        Nombre = item.Filial.Trim(),
                        EmpresaId = empresa.Id,
                        SubsidiaEmpleados = true,
                        SubsidioTipo = SubsidioTipo.Porcentaje,
                        SubsidioValor = item.SubsidioEmpresaPct ?? empresa.SubsidioValor
                    };
                    db.Sucursales.Add(suc);
                }
                else if (item.SubsidioEmpresaPct.HasValue && (suc.SubsidioValor == null || suc.SubsidioValor <= 0))
                {
                    suc.SubsidiaEmpleados = true;
                    suc.SubsidioTipo = SubsidioTipo.Porcentaje;
                    suc.SubsidioValor = item.SubsidioEmpresaPct.Value;
                }
            }
            await db.SaveChangesAsync();
        }

        // Localizaciones por empresa (desde ETL si existe)
        if (etlData != null)
            await EnsureEtlLocalizacionesAsync(db, etlData);

        // Semilla legacy mÃ­nima de roles/usuarios de dominio (sin duplicar)
        if (!await db.Roles.AnyAsync(r => r.Nombre == "Admin"))
            db.Roles.Add(new Rol { Nombre = "Admin" });
        if (!await db.Roles.AnyAsync(r => r.Nombre == "Empleado"))
            db.Roles.Add(new Rol { Nombre = "Empleado" });
        if (!await db.Roles.AnyAsync(r => r.Nombre == "RRHH"))
            db.Roles.Add(new Rol { Nombre = "RRHH" });
        await db.SaveChangesAsync();

        await EnsureDemoSucursalHorariosAsync(db);
        await EnsureDemoMenusAndResponsesAsync(db);
    }

    private static MenuEtlData? LoadMenuEtlData(string? contentRootPath)
    {
        var basePath = string.IsNullOrWhiteSpace(contentRootPath) ? AppContext.BaseDirectory : contentRootPath;
        var filePath = Path.Combine(basePath, "App_Data", "menu_etl_data.json");
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<MenuEtlData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static readonly DayOfWeek[] DemoWeekDays = new[]
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    };

    private static readonly string[] DemoHorarioNames = new[] { "Desayuno", "Almuerzo" };

    private static async Task EnsureDemoSucursalHorariosAsync(AppDbContext db)
    {
        var horarios = await db.Horarios
            .AsNoTracking()
            .Where(h => h.Nombre == "Desayuno" || h.Nombre == "Almuerzo")
            .ToListAsync();
        if (horarios.Count == 0)
            return;

        var sucursales = await db.Sucursales.AsNoTracking().Select(s => s.Id).ToListAsync();
        if (sucursales.Count == 0)
            return;

        var existentes = await db.SucursalesHorarios
            .AsNoTracking()
            .Select(sh => new { sh.SucursalId, sh.HorarioId })
            .ToListAsync();
        var existentesSet = new HashSet<string>(
            existentes.Select(e => $"{e.SucursalId:N}|{e.HorarioId:N}"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var sucursalId in sucursales)
        {
            foreach (var horario in horarios)
            {
                var key = $"{sucursalId:N}|{horario.Id:N}";
                if (existentesSet.Contains(key)) continue;
                db.SucursalesHorarios.Add(new SucursalHorario
                {
                    SucursalId = sucursalId,
                    HorarioId = horario.Id
                });
                existentesSet.Add(key);
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoMenusAndResponsesAsync(AppDbContext db)
    {
        var sucursales = await db.Sucursales.ToListAsync();
        var horarios = await db.Horarios
            .Where(h => h.Activo && (h.Nombre == "Desayuno" || h.Nombre == "Almuerzo"))
            .OrderBy(h => h.Orden)
            .ToListAsync();
        if (!horarios.Any())
            return;

        var opciones = await db.Opciones.Where(o => !o.Borrado).OrderBy(o => o.Nombre).Take(20).ToListAsync();
        if (opciones.Count < 3)
            return;

        var empleados = await db.Empleados.ToListAsync();
        if (!empleados.Any())
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentMonday = GetWeekStart(today);
        var nextMonday = currentMonday.AddDays(7);
        var weekStarts = new[] { currentMonday.AddDays(-14), currentMonday.AddDays(-7), currentMonday, nextMonday };

        var menuCache = new Dictionary<(int sucursalId, DateOnly inicio), Menu>();
        foreach (var weekStart in weekStarts)
        {
            var weekEnd = weekStart.AddDays(4);
            foreach (var suc in sucursales)
            {
                var menu = await EnsureMenuWithOptionsAsync(db, suc, weekStart, weekEnd, horarios, opciones);
                menuCache[(suc.Id, weekStart)] = menu;
            }
        }

        var adicionalesBase = opciones
            .OrderBy(o => o.Nombre)
            .Take(3)
            .ToList();
        if (adicionalesBase.Count > 0)
        {
            foreach (var menu in menuCache.Values.Distinct())
            {
                foreach (var adicional in adicionalesBase)
                {
                    var existe = await db.MenusAdicionales
                        .AnyAsync(a => a.MenuId == menu.Id && a.OpcionId == adicional.Id);
                    if (!existe)
                    {
                        db.MenusAdicionales.Add(new MenuAdicional
                        {
                            MenuId = menu.Id,
                            OpcionId = adicional.Id
                        });
                    }
                }
            }
            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync();
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }

    private static async Task EnsureEtlLocalizacionesAsync(AppDbContext db, MenuEtlData etlData)
    {
        if (etlData.Localizaciones == null || etlData.Localizaciones.Count == 0)
            return;

        var nombresLoc = etlData.Localizaciones
            .Select(l => l?.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (nombresLoc.Count == 0)
            return;

        var empresas = await db.Empresas
            .AsNoTracking()
            .Select(e => e.Id)
            .ToListAsync();
        var existentes = await db.Localizaciones
            .AsNoTracking()
            .Select(l => new { l.EmpresaId, l.Nombre })
            .ToListAsync();
        var existentesSet = new HashSet<string>(
            existentes.Select(e => $"{e.EmpresaId:N}|{e.Nombre}"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var empresaId in empresas)
        {
            foreach (var nombre in nombresLoc)
            {
                var key = $"{empresaId:N}|{nombre}";
                if (existentesSet.Contains(key)) continue;
                db.Localizaciones.Add(new Localizacion
                {
                    Nombre = nombre,
                    EmpresaId = empresaId,
                    SucursalId = null,
                    Direccion = $"Direccion de empresa {empresaId}",
                    IndicacionesEntrega = $"Entrega en {nombre}"
                });
                existentesSet.Add(key);
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }


    private static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static async Task<Menu> EnsureMenuWithOptionsAsync(AppDbContext db, Sucursal sucursal, DateOnly inicio, DateOnly fin, IReadOnlyList<Horario> horarios, IReadOnlyList<Opcion> opciones)
    {
        var menu = await db.Menus
            .Include(m => m.OpcionesPorDia)
            .FirstOrDefaultAsync(m => m.SucursalId == sucursal.Id && m.FechaInicio == inicio && m.FechaTermino == fin);
        if (menu == null)
        {
            menu = new Menu
            {
                FechaInicio = inicio,
                FechaTermino = fin,
                EmpresaId = sucursal.EmpresaId,
                SucursalId = sucursal.Id
            };
            db.Menus.Add(menu);
        }

        var dias = DemoWeekDays;
        if (menu.OpcionesPorDia == null)
            menu.OpcionesPorDia = new List<OpcionMenu>();

        for (var dayIndex = 0; dayIndex < dias.Length; dayIndex++)
        {
            var day = dias[dayIndex];
            for (var horarioIndex = 0; horarioIndex < horarios.Count; horarioIndex++)
            {
                var horario = horarios[horarioIndex];
                var opcionMenu = menu.OpcionesPorDia.FirstOrDefault(o => o.DiaSemana == day && o.HorarioId == horario.Id);
                if (opcionMenu == null)
                {
                    opcionMenu = new OpcionMenu
                    {
                        Menu = menu,
                        DiaSemana = day,
                        HorarioId = horario.Id
                    };
                    menu.OpcionesPorDia.Add(opcionMenu);
                }

                var baseIndex = (dayIndex * horarios.Count + horarioIndex) * 3;
                opcionMenu.OpcionIdA = opciones[(baseIndex) % opciones.Count].Id;
                opcionMenu.OpcionIdB = opciones[(baseIndex + 1) % opciones.Count].Id;
                opcionMenu.OpcionIdC = opciones[(baseIndex + 2) % opciones.Count].Id;
                opcionMenu.OpcionesMaximas = 3;
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();

        return menu;
    }

    private static async Task AddOrUpdateDemoResponseAsync(AppDbContext db, Empleado empleado, Menu menu, DayOfWeek dia, string horarioNombre, char seleccion, int localizacionId, IReadOnlyList<Horario> horarios)
    {
        var horario = horarios.FirstOrDefault(h => string.Equals(h.Nombre, horarioNombre, StringComparison.OrdinalIgnoreCase)) ?? horarios.First();
        var opcionMenu = await db.OpcionesMenu.FirstOrDefaultAsync(o => o.MenuId == menu.Id && o.DiaSemana == dia && o.HorarioId == horario.Id);
        if (opcionMenu == null)
            return;

        var existing = await db.RespuestasFormulario.FirstOrDefaultAsync(r => r.EmpleadoId == empleado.Id && r.OpcionMenuId == opcionMenu.Id);
        if (existing == null)
        {
            db.RespuestasFormulario.Add(new RespuestaFormulario
            {
                EmpleadoId = empleado.Id,
                OpcionMenuId = opcionMenu.Id,
                Seleccion = seleccion,
                SucursalEntregaId = empleado.SucursalId,
                LocalizacionEntregaId = localizacionId,
                FechaSeleccion = DateTime.UtcNow
            });
        }
        else
        {
            existing.Seleccion = seleccion;
            existing.SucursalEntregaId = empleado.SucursalId;
            existing.LocalizacionEntregaId = localizacionId;
            if (existing.FechaSeleccion == null)
                existing.FechaSeleccion = DateTime.UtcNow;
        }
    }

    private sealed class MenuEtlData
    {
        public List<MenuEtlEmpresa> Empresas { get; set; } = new();
        public List<MenuEtlProducto> Productos { get; set; } = new();
        public List<string> Localizaciones { get; set; } = new();
    }

    private sealed class MenuEtlEmpresa
    {
        public string? Empresa { get; set; }
        public string? Filial { get; set; }
        public string? Departamento { get; set; }
        public string? Producto { get; set; }
        public decimal? PrecioDesayuno { get; set; }
        public decimal? PrecioCena { get; set; }
        public decimal? PrecioAlmuerzo { get; set; }
        public decimal? SubsidioEmpresaPct { get; set; }
        public decimal? SubsidioEmpleadoPct { get; set; }
    }

    private sealed class MenuEtlProducto
    {
        public string? Codigo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Precio { get; set; }
        public string? Categoria { get; set; }
    }

}


