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
        var defaultPrecioDesayuno = etlData?.Empresas?.Select(x => x.PrecioDesayuno).FirstOrDefault(x => x.HasValue);
        var defaultPrecioAlmuerzo = etlData?.Empresas?.Select(x => x.PrecioAlmuerzo).FirstOrDefault(x => x.HasValue);
        var defaultPrecioCena = etlData?.Empresas?.Select(x => x.PrecioCena).FirstOrDefault(x => x.HasValue);

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
        if (etlData?.Localizaciones != null && etlData.Localizaciones.Count > 0)
        {
            var nombresLoc = etlData.Localizaciones
                .Select(l => l?.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (nombresLoc.Count > 0)
            {
                var sucursales = await db.Sucursales
                    .AsNoTracking()
                    .Select(s => new { s.Id, s.EmpresaId, s.Nombre })
                    .ToListAsync();
                var existentes = await db.Localizaciones
                    .AsNoTracking()
                    .Select(l => new { l.SucursalId, l.EmpresaId, l.Nombre })
                    .ToListAsync();
                var existentesSet = new HashSet<string>(
                    existentes.Select(e => $"{e.SucursalId:N}|{e.EmpresaId:N}|{e.Nombre}"),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var suc in sucursales)
                {
                    if (suc.Id == Guid.Empty) continue;
                    foreach (var nombre in nombresLoc)
                    {
                        var key = $"{suc.Id:N}|{suc.EmpresaId:N}|{nombre}";
                        if (existentesSet.Contains(key)) continue;
                        var sucursalNombre = string.IsNullOrWhiteSpace(suc.Nombre) ? "filial" : suc.Nombre;
                        db.Localizaciones.Add(new Localizacion
                        {
                            Nombre = nombre,
                            EmpresaId = suc.EmpresaId,
                            SucursalId = suc.Id,
                            Direccion = $"Direccion de {sucursalNombre}",
                            IndicacionesEntrega = $"Entrega en {nombre}"
                        });
                        existentesSet.Add(key);
                    }
                }
                await db.SaveChangesAsync();
            }
        }

        if (etlData?.Productos != null && etlData.Productos.Count > 0)
        {
            var horariosActivos = await db.Horarios.Where(h => h.Activo).ToListAsync();
            var horarioMap = horariosActivos.ToDictionary(h => h.Nombre, h => h.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var prod in etlData.Productos)
            {
                if (string.IsNullOrWhiteSpace(prod.Descripcion))
                    continue;

                var codigo = string.IsNullOrWhiteSpace(prod.Codigo) ? null : prod.Codigo.Trim();
                var nombre = prod.Descripcion.Trim();

                var opcion = codigo == null
                    ? await db.Opciones.FirstOrDefaultAsync(o => o.Nombre == nombre)
                    : await db.Opciones.FirstOrDefaultAsync(o => o.Codigo == codigo || o.Nombre == nombre);

                if (opcion == null)
                {
                    opcion = new Opcion
                    {
                        Codigo = codigo,
                        Nombre = nombre,
                        Descripcion = nombre,
                        EsSubsidiado = true
                    };
                    db.Opciones.Add(opcion);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(opcion.Codigo) && codigo != null)
                        opcion.Codigo = codigo;
                    if (string.IsNullOrWhiteSpace(opcion.Descripcion))
                        opcion.Descripcion = nombre;
                    if (!opcion.EsSubsidiado)
                        opcion.EsSubsidiado = true;
                }

                var categorias = ParseCategorias(prod.Categoria);
                var precioBase = prod.Precio ?? ResolveDefaultPrecio(categorias, defaultPrecioDesayuno, defaultPrecioAlmuerzo, defaultPrecioCena);
                if (precioBase.HasValue && opcion.Costo <= 0)
                    opcion.Costo = precioBase.Value;
                if (precioBase.HasValue && opcion.Precio == null)
                    opcion.Precio = precioBase.Value;

                foreach (var cat in categorias)
                {
                    if (!horarioMap.TryGetValue(cat, out var horarioId))
                        continue;
                    var existe = await db.OpcionesHorarios.AnyAsync(oh => oh.OpcionId == opcion.Id && oh.HorarioId == horarioId);
                    if (!existe)
                        db.OpcionesHorarios.Add(new OpcionHorario { OpcionId = opcion.Id, HorarioId = horarioId });
                }
            }
            await db.SaveChangesAsync();
        }

        var opcionesNoSubsidiadas = await db.Opciones.Where(o => !o.EsSubsidiado).ToListAsync();
        if (opcionesNoSubsidiadas.Count > 0)
        {
            foreach (var opcion in opcionesNoSubsidiadas)
                opcion.EsSubsidiado = true;
            await db.SaveChangesAsync();
        }

        // Semilla legacy mÃ­nima de roles/usuarios de dominio (sin duplicar)
        if (!await db.Roles.AnyAsync(r => r.Nombre == "Admin"))
            db.Roles.Add(new Rol { Nombre = "Admin" });
        if (!await db.Roles.AnyAsync(r => r.Nombre == "Empleado"))
            db.Roles.Add(new Rol { Nombre = "Empleado" });
        await db.SaveChangesAsync();

        await EnsureDemoLocalizacionesAsync(db);
        await EnsureDemoEmployeesAsync(db);
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

    private static HashSet<string> ParseCategorias(string? raw)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var tokens = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var upper = token.Trim().ToUpperInvariant();
            if (upper == "A" || upper.Contains("ALMUERZO"))
                result.Add("Almuerzo");
            else if (upper == "D" || upper.Contains("DESAYUNO"))
                result.Add("Desayuno");
            else if (upper == "C" || upper.Contains("CENA"))
                result.Add("Cena");
        }
        return result;
    }

    private static decimal? ResolveDefaultPrecio(HashSet<string> categorias, decimal? desayuno, decimal? almuerzo, decimal? cena)
    {
        if (categorias.Contains("Almuerzo") && almuerzo.HasValue)
            return almuerzo;
        if (categorias.Contains("Desayuno") && desayuno.HasValue)
            return desayuno;
        if (categorias.Contains("Cena") && cena.HasValue)
            return cena;
        return null;
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

    private static async Task EnsureDemoLocalizacionesAsync(AppDbContext db)
    {
        var sucursales = await db.Sucursales.AsNoTracking().ToListAsync();
        var localizacionNombres = new[] { "Logistica", "Operaciones", "Mesa de control" };
        foreach (var suc in sucursales)
        {
            foreach (var nombre in localizacionNombres)
            {
                if (await db.Localizaciones.AnyAsync(l => l.SucursalId == suc.Id && l.Nombre == nombre))
                    continue;

                db.Localizaciones.Add(new Localizacion
                {
                    Nombre = nombre,
                    EmpresaId = suc.EmpresaId,
                    SucursalId = suc.Id,
                    Direccion = $"{suc.Nombre} - {nombre}",
                    IndicacionesEntrega = $"Entrega en {nombre}"
                });
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoEmployeesAsync(AppDbContext db)
    {
        var sucursales = await db.Sucursales.ToListAsync();
        var empleadosDemo = new[]
        {
            new { Codigo = "RIC001", Nombre = "Richard", Sucursal = "ADMINISTRACION UNIVERSAL" },
            new { Codigo = "ANDO001", Nombre = "Anderson", Sucursal = "ARS UNIVERSAL" },
            new { Codigo = "DAMIAN1", Nombre = "Damian", Sucursal = "ASISTENCIA UNIVERSAL" },
            new { Codigo = "ADONYS1", Nombre = "Adonys", Sucursal = "SUPLIDORA PROPARTES" }
        };

        foreach (var demo in empleadosDemo)
        {
            var sucursal = sucursales.FirstOrDefault(s => string.Equals(s.Nombre, demo.Sucursal, StringComparison.OrdinalIgnoreCase));
            if (sucursal == null)
                continue;

            var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.Codigo == demo.Codigo);
            if (empleado == null)
            {
                empleado = new Empleado
                {
                    Nombre = demo.Nombre,
                    Codigo = demo.Codigo,
                    SucursalId = sucursal.Id,
                    EsSubsidiado = true,
                    Estado = EmpleadoEstado.Habilitado
                };
                db.Empleados.Add(empleado);
            }
            else
            {
                empleado.Nombre = demo.Nombre;
                empleado.SucursalId = sucursal.Id;
                empleado.Estado = EmpleadoEstado.Habilitado;
                empleado.EsSubsidiado = true;
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoMenusAndResponsesAsync(AppDbContext db)
    {
        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Nombre == "SEGURO UNIVERSAL");
        if (empresa == null)
            return;

        var sucursales = await db.Sucursales.Where(s => s.EmpresaId == empresa.Id).ToListAsync();
        var horarios = await db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
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

        var menuCache = new Dictionary<(Guid sucursalId, DateOnly inicio), Menu>();
        foreach (var weekStart in weekStarts)
        {
            var weekEnd = weekStart.AddDays(4);
            foreach (var suc in sucursales)
            {
                var menu = await EnsureMenuWithOptionsAsync(db, suc, weekStart, weekEnd, horarios, opciones);
                menuCache[(suc.Id, weekStart)] = menu;
            }
        }

        var localizMap = await db.Localizaciones
            .GroupBy(l => l.SucursalId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList());

        var employeeMap = empleados
            .Where(e => !string.IsNullOrWhiteSpace(e.Codigo))
            .ToDictionary(e => e.Codigo!, StringComparer.OrdinalIgnoreCase);

        var historyCodes = new[] { "RIC001", "ANDO001", "DAMIAN1" };
        var currentCodes = new[] { "DAMIAN1", "ADONYS1" };
        var demoDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday };

        foreach (var weekStart in weekStarts.Take(2))
        {
            foreach (var code in historyCodes)
            {
                if (!employeeMap.TryGetValue(code, out var empleado))
                    continue;
                if (!menuCache.TryGetValue((empleado.SucursalId, weekStart), out var menu))
                    continue;
                if (!localizMap.TryGetValue(empleado.SucursalId, out var locs) || locs.Count == 0)
                    continue;

                for (var i = 0; i < demoDays.Length; i++)
                {
                    var day = demoDays[i];
                    var horario = DemoHorarioNames[i % DemoHorarioNames.Length];
                    var location = locs[i % locs.Count];
                    var selection = i % 2 == 0 ? 'A' : 'B';
                    await AddOrUpdateDemoResponseAsync(db, empleado, menu, day, horario, selection, location.Id, horarios);
                }
            }
        }

        foreach (var code in currentCodes)
        {
            if (!employeeMap.TryGetValue(code, out var empleado))
                continue;
            var weekStart = weekStarts.Last();
            if (!menuCache.TryGetValue((empleado.SucursalId, weekStart), out var menu))
                continue;
            if (!localizMap.TryGetValue(empleado.SucursalId, out var locs) || locs.Count == 0)
                continue;

            for (var i = 0; i < demoDays.Length; i++)
            {
                var day = demoDays[i];
                var horario = DemoHorarioNames[i % DemoHorarioNames.Length];
                var location = locs[(i + 1) % locs.Count];
                var selection = i % 2 == 0 ? 'A' : 'B';
                await AddOrUpdateDemoResponseAsync(db, empleado, menu, day, horario, selection, location.Id, horarios);
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

    private static async Task AddOrUpdateDemoResponseAsync(AppDbContext db, Empleado empleado, Menu menu, DayOfWeek dia, string horarioNombre, char seleccion, Guid localizacionId, IReadOnlyList<Horario> horarios)
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
                LocalizacionEntregaId = localizacionId
            });
        }
        else
        {
            existing.Seleccion = seleccion;
            existing.SucursalEntregaId = empleado.SucursalId;
            existing.LocalizacionEntregaId = localizacionId;
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


