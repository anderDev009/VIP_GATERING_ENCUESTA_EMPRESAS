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
            await EnsureMenuSemana20260112Async(db);
            return;
        }
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
        if (etlData != null)
            await EnsureEtlLocalizacionesAsync(db, etlData);

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

                    if (opcion.Id != 0)
                    {
                        var existe = await db.OpcionesHorarios.AnyAsync(oh => oh.OpcionId == opcion.Id && oh.HorarioId == horarioId);
                        if (!existe)
                            db.OpcionesHorarios.Add(new OpcionHorario { OpcionId = opcion.Id, HorarioId = horarioId });
                        continue;
                    }

                    db.OpcionesHorarios.Add(new OpcionHorario { Opcion = opcion, HorarioId = horarioId });
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

        var opciones = await db.Opciones.ToListAsync();
        if (opciones.Count > 0)
        {
            foreach (var opcion in opciones)
            {
                opcion.Costo = 240m;
                opcion.Precio = 240m;
            }
            await db.SaveChangesAsync();
        }

        var opcionesSinItbis = await db.Opciones.Where(o => !o.LlevaItbis).ToListAsync();
        if (opcionesSinItbis.Count > 0)
        {
            foreach (var opcion in opcionesSinItbis)
                opcion.LlevaItbis = true;
            await db.SaveChangesAsync();
        }

        // Semilla legacy mÃ­nima de roles/usuarios de dominio (sin duplicar)
        if (!await db.Roles.AnyAsync(r => r.Nombre == "Admin"))
            db.Roles.Add(new Rol { Nombre = "Admin" });
        if (!await db.Roles.AnyAsync(r => r.Nombre == "Empleado"))
            db.Roles.Add(new Rol { Nombre = "Empleado" });
        if (!await db.Roles.AnyAsync(r => r.Nombre == "RRHH"))
            db.Roles.Add(new Rol { Nombre = "RRHH" });
        await db.SaveChangesAsync();

        await EnsureDemoSucursalHorariosAsync(db);
        await EnsureExcelMenuProductosAsync(db);
        await EnsureMenuSemana20260112Async(db);
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
            if (suc.Id == 0) continue;
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

    private static readonly SeedProducto[] ExcelProductosSemana20260105 = new[]
    {
        new SeedProducto(null, "Arepa porcion", "Arepa porcion", 55.08m, "Adicional", "R0002048"),
        new SeedProducto(null, "Chocolate caliente", "Chocolate caliente", 63.55m, "Adicional", "R0002049"),
        new SeedProducto(null, "Croissant", "Croissant", 94.91m, "Adicional", "R0003628"),
        new SeedProducto(null, "Empanada", "Empanada", 60.34m, "Adicional", "R0002184"),
        new SeedProducto(null, "Refresco 16 onz", "Refresco 16 onz", 46.61m, "Adicional", "10117"),
        new SeedProducto(null, "Vivere+ compaña", "Vivere+ compaña", 175.00m, "Adicional", "R0003514"),
        new SeedProducto(null, "Vaso de jugo", "Vaso de jugo", 61.01m, "Adicional", "90035"),
        new SeedProducto("P0001", "Arroz Blanco + Habichuela Giras guisadas + Filete de Pechuga con Salsa Curry + Ens de Tomates al cilantro + Panes variados", "Arroz Blanco + Habichuela Giras guisadas + Filete de Pechuga con Salsa Curry + Ens de Tomates al cilantro", 240.00m, "Almuerzo", null),
        new SeedProducto("P0002", "Arroz con maiz y tocineta + Chuleta ahumada con salsa criolla + Ens de tomates al cilantro + Panes variados", "Arroz con maiz y tocineta + Chuleta ahumada con salsa criolla + Ens de tomates al cilantro + Panes variados", 240.00m, "Almuerzo", null),
        new SeedProducto("P0003", "Mangu de platano verde +Filete de pechuga con salsa de curry + Ens de tomates al cilantro + Panes variados", "Mangu de platano verde +Filete de pechuga con salsa de curry + Ens de tomates al cilantro + Panes variados", 240.00m, "Almuerzo", null),
        new SeedProducto("P0004", "Arroz blanco + Guandules guisados + Pollo guisado al vino + Ensalada de pasta + Frito maduro", "Arroz blanco + Guandules guisados + Pollo guisado al vino + Ensalada de pasta + Frito maduro", 240.00m, "Almuerzo", null),
        new SeedProducto("P0005", "Moro de habichuela  giras + Pollo guisado al vino + Ensalada de pasta + Frito maduro", "Moro de habichuela  giras + Pollo guisado al vino + Ensalada de pasta + Frito maduro", 240.00m, "Almuerzo", null),
        new SeedProducto("P0006", "Pure de yuca gratinado +Filete de  mero crocante + Frito maduro", "Pure de yuca gratinado +Filete de  mero crocante + Frito maduro", 240.00m, "Almuerzo", null),
        new SeedProducto("P0007", "Arroz blanco + Habichuela  Rojas guisadas + Pollo asado al limon +Ensalada de repollo variados + Arepita de maiz", "Arroz blanco + Habichuela  Rojas guisadas + Pollo asado al limon +Ensalada de repollo variados + Arepita de maiz", 240.00m, "Almuerzo", null),
        new SeedProducto("P0008", "Moro de habichuela negras + Escalopines de cerdo con vegetales + Ensalada de repollo variados + Arepita de maiz", "Moro de habichuela negras + Escalopines de cerdo con vegetales + Ensalada de repollo variados + Arepita de maiz", 240.00m, "Almuerzo", null),
        new SeedProducto("P0009", "Mangu de guineito + Pollo asado al limon + Ensalada de repollo variados + Arepitas de maiz", "Mangu de guineito + Pollo asado al limon + Ensalada de repollo variados + Arepitas de maiz", 240.00m, "Almuerzo", null),
        new SeedProducto("P0010", "Arros blanco + Lentejas guisadas + Pollo horneado con wasakaka + Enslada cesar + Arepita de maiz", "Arros blanco + Lentejas guisadas + pollo horneado con wasakaka + Enslada cesar + Arepita de maiz", 240.00m, "Almuerzo", null),
        new SeedProducto("P0011", "Arroz con fideos  y platano maduro + Chuleta ahumada con salsa criolla + Enslada cesar + Arepita de maiz", "Arroz con fideos  y platano maduro + Chuleta ahumada con salsa criolla + Enslada cesar + Arepita de maiz", 240.00m, "Almuerzo", null),
        new SeedProducto("P0012", "Mangu de platano + Pollo horneado con wasakaka + Enslada cesar + Arepita de maiz", "Mangu de platano + Pollo horneado con wasakaka + Enslada cesar + Arepita de maiz", 240.00m, "Almuerzo", null),
        new SeedProducto("P0013", "Arroz blanco + Habichuela  Negras guisadas + Pechurina en salsa rosada + Ensalada hervida + Yaniquequito", "Arroz blanco + Habichuela  Negras guisadas + Pechurina en salsa rosada + Ensalada hervida + Yaniquequito", 240.00m, "Almuerzo", null),
        new SeedProducto("P0014", "Arroz con vegetales + Escalopines de cerdo con salsa de hierbas + Ensalada hervida + Yaniquequito", "Arroz con vegetales + Escalopines de cerdo con salsa de hierbas + Ensalada hervida + Yaniquequito", 240.00m, "Almuerzo", null),
        new SeedProducto("P0015", "Mangu de guineito + Pechurina en salsa rosada + Ensalada hervida + Yaniquequito", "Mangu de guineito + Pechurina en salsa rosada + Ensalada hervida + Yaniquequito", 240.00m, "Almuerzo", null),
        new SeedProducto("P0016", "Arroz blanco + Habichuela  Rojas guisadas + Pollo asado +Ensalada de pasta Mexicana  + Frito maduro", "Arroz blanco + Habichuela  Rojas guisadas + Pollo asado +Ensalada de pasta Mexicana  + Frito maduro", 240.00m, "Almuerzo", null),
        new SeedProducto("P0017", "Moro de guandules + Pollo asado + Ensalada de pasta mexicana + Frito maduro", "Moro de guandules + Pollo asado + Ensalada de pasta mexicana + Frito maduro", 240.00m, "Almuerzo", null),
        new SeedProducto("P0018", "Mangu de maduro gratinado + Bastones de mero + Enslada de pasta Mexicana + Frito maduro", "Mangu de maduro gratinado + Bastones de mero + Enslada de pasta Mexicana + Frito maduro", 240.00m, "Almuerzo", null),
        new SeedProducto("P0019", "Arroz Blanco + Habichuela Giras guisadas + Filete de Pechuga a la plancha encebollada + Ensalada Verde fresca + Yuquita frita", "Arroz Blanco + Habichuela Giras guisadas + Filete de Pechuga a la plancha encebollada + Ensalada Verde fresca + Yuquita frita", 240.00m, "Almuerzo", null),
        new SeedProducto("P0020", "Arroz tres delicias + Ropa vieja a la criolla con vegetales + Ensalada Verde Fresca + Yuquita frita", "Arroz tres delicias + Ropa vieja a la criolla con vegetales + Ensalada Verde Fresca + Yuquita frita", 240.00m, "Almuerzo", null),
        new SeedProducto("P0021", "Hamburguesas + Papas fritas", "Hamburguesas + Papas fritas", 240.00m, "Almuerzo", null),
        new SeedProducto("P0022", "Ensalada", "Ensalada", 240.00m, "Almuerzo", null),
        new SeedProducto("P0023", "Chiken cesar wraps + jugo", "Chiken cesar wraps + jugo", 240.00m, "Almuerzo", null),
        new SeedProducto("P0024", "Mozzarella Capresa + jugo", "Mozzarella Capresa + jugo", 240.00m, "Almuerzo", null),
        new SeedProducto("P0025", "Ham am cheese (jamon y queso) + jugo", "Ham am cheese (jamon y queso) + jugo", 240.00m, "Almuerzo", null),
        new SeedProducto("P0026", "Baguette integral de pavo y mozzarella", "Baguette integral de pavo y mozzarella", 240.00m, "Almuerzo", null),
        new SeedProducto("P0027", "sand. Pavo y  pastrami + Jugo", "sand. Pavo y  pastrami + Jugo", 240.00m, "Almuerzo", null)
    };

    private sealed record SeedProducto(string? Codigo, string Nombre, string Descripcion, decimal Precio, string Categoria, string? CodigoDyn);

    private static async Task EnsureExcelMenuProductosAsync(AppDbContext db)
    {
        var almuerzoHorario = await db.Horarios.FirstOrDefaultAsync(h => h.Nombre == "Almuerzo");
        if (almuerzoHorario == null)
            return;

        var adicionales = new List<Opcion>();
        var almuerzos = new List<Opcion>();

        foreach (var prod in ExcelProductosSemana20260105)
        {
            var codigo = string.IsNullOrWhiteSpace(prod.Codigo) ? prod.CodigoDyn : prod.Codigo;
            var nombre = prod.Nombre.Trim();
            var descripcion = string.IsNullOrWhiteSpace(prod.Descripcion) ? nombre : prod.Descripcion.Trim();
            Opcion? opcion = null;
            if (!string.IsNullOrWhiteSpace(codigo))
                opcion = await db.Opciones.FirstOrDefaultAsync(o => o.Codigo == codigo);
            if (opcion == null)
                opcion = await db.Opciones.FirstOrDefaultAsync(o => o.Nombre == nombre);

            if (opcion == null)
            {
                opcion = new Opcion
                {
                    Codigo = codigo,
                    Nombre = nombre,
                    Descripcion = descripcion,
                    Costo = prod.Precio,
                    Precio = prod.Precio,
                    EsSubsidiado = true,
                    LlevaItbis = true
                };
                db.Opciones.Add(opcion);
            }
            else
            {
                opcion.Codigo = codigo;
                opcion.Nombre = nombre;
                opcion.Descripcion = descripcion;
                opcion.Costo = prod.Precio;
                opcion.Precio = prod.Precio;
                opcion.EsSubsidiado = true;
                opcion.LlevaItbis = true;
            }

            if (string.Equals(prod.Categoria, "Adicional", StringComparison.OrdinalIgnoreCase))
                adicionales.Add(opcion);
            else if (string.Equals(prod.Categoria, "Almuerzo", StringComparison.OrdinalIgnoreCase))
                almuerzos.Add(opcion);
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();

        foreach (var opcion in almuerzos)
        {
            var existe = await db.OpcionesHorarios.AnyAsync(oh => oh.OpcionId == opcion.Id && oh.HorarioId == almuerzoHorario.Id);
            if (!existe)
                db.OpcionesHorarios.Add(new OpcionHorario { OpcionId = opcion.Id, HorarioId = almuerzoHorario.Id });
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();

        var sucursales = await db.Sucursales.ToListAsync();
        if (!sucursales.Any())
            return;

        var week1Start = new DateOnly(2026, 1, 5);
        var week2Start = week1Start.AddDays(7);
        var weekStarts = new[] { week1Start, week2Start };
        var dayList = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        var almuerzoSeq = almuerzos.ToList();
        if (almuerzoSeq.Count == 0)
            return;

        var index = 0;
        List<(DayOfWeek day, int aId, int bId, int cId)> BuildWeekPlan()
        {
            var plan = new List<(DayOfWeek day, int aId, int bId, int cId)>();
            foreach (var day in dayList)
            {
                var a = almuerzoSeq[index % almuerzoSeq.Count]; index++;
                var b = almuerzoSeq[index % almuerzoSeq.Count]; index++;
                var c = almuerzoSeq[index % almuerzoSeq.Count]; index++;
                plan.Add((day, a.Id, b.Id, c.Id));
            }
            return plan;
        }

        foreach (var weekStart in weekStarts)
        {
            var weekEnd = weekStart.AddDays(4);
            var weekPlan = BuildWeekPlan();
            foreach (var suc in sucursales)
            {
                var menu = await db.Menus
                    .Include(m => m.OpcionesPorDia)
                    .FirstOrDefaultAsync(m => m.SucursalId == suc.Id && m.FechaInicio == weekStart && m.FechaTermino == weekEnd);
                if (menu == null)
                {
                    menu = new Menu
                    {
                        FechaInicio = weekStart,
                        FechaTermino = weekEnd,
                        EmpresaId = suc.EmpresaId,
                        SucursalId = suc.Id,
                        OpcionesPorDia = new List<OpcionMenu>()
                    };
                    db.Menus.Add(menu);
                }

                var existentes = menu.OpcionesPorDia.Where(o => o.HorarioId == almuerzoHorario.Id).ToList();
                if (existentes.Count > 0)
                    db.OpcionesMenu.RemoveRange(existentes);

                foreach (var row in weekPlan)
                {
                    menu.OpcionesPorDia.Add(new OpcionMenu
                    {
                        Menu = menu,
                        DiaSemana = row.day,
                        HorarioId = almuerzoHorario.Id,
                        OpcionIdA = row.aId,
                        OpcionIdB = row.bId,
                        OpcionIdC = row.cId,
                        OpcionesMaximas = 3
                    });
                }

                var adicionalesExistentes = await db.MenusAdicionales.Where(a => a.MenuId == menu.Id).ToListAsync();
                if (adicionalesExistentes.Count > 0)
                    db.MenusAdicionales.RemoveRange(adicionalesExistentes);

                foreach (var adicional in adicionales)
                {
                    db.MenusAdicionales.Add(new MenuAdicional
                    {
                        Menu = menu,
                        OpcionId = adicional.Id
                    });
                }
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }

    private static readonly Dictionary<DayOfWeek, string?[]> MenuAlmuerzoSemana20260112 = new()
    {
        [DayOfWeek.Monday] = new[]
        {
            "Arroz blanco + Hab. giras guisadas + Filete de pechuga en salsa pizza + Ensalada verde picada + Casabe tostado",
            "Sancocho criollo + Arroz blanco + Casabe tostado",
            "Mangu de guineitos + Cuadros de res guisado + Ensalada verde picada + Casabe tostado",
            "Feriado",
            "Feriado"
        },
        [DayOfWeek.Tuesday] = new[]
        {
            "Arroz blanco + Hab. negras guisadas + Pollo horneado al limon + Ensalada capresa + Yuquita frita",
            "Moro de guandules con auyama gratinado + Higado de res encebollado + Ensalada capresa + Yuquita frita",
            "Pure de yuca y auyama gratinado + Pollo horneado al limon + Ensalada capresa + Yuquita frita",
            "Ensalada mozzarella capresa",
            "Jugo"
        },
        [DayOfWeek.Wednesday] = new[]
        {
            "Arroz blanco + Guandules guisados con perejil + Pollo frito empanizado + Ensalada coles law + Yaniquequito",
            "Arroz con coco decorado + Pollo frito empanizado + Bacalao en ensalada con papa + Yaniquequito y cebolla",
            "Papas salteadas + Guandules guisados + Ensalada coles law + Yaniquequito",
            "Dia Feriado",
            "Dia Feriado"
        },
        [DayOfWeek.Thursday] = new[]
        {
            "Arroz blanco + Hab. rojas guisadas + Pollo asado en su jugo + Ensalada verde fresca + Arepitas de maiz",
            "Locrio de cerdo con vegetales + Hab. rojas guisadas + Ensalada verde fresca + Arepitas de maiz",
            "Mangu maduro gratinado + Filete de mero al ajillo + Ensalada verde fresca + Arepitas de maiz",
            "Ensalada san. de pavo y pastrami",
            "Jugo"
        },
        [DayOfWeek.Friday] = new[]
        {
            "Arroz blanco + Lentejas guisadas + Pollo guisado a la criolla + Ens. de vegetales al vapor + Chips",
            "Moro de hab. negras + Ens. de vegetales al vapor + Chips",
            "Guineitos hervidos con aji y cubanela y cebolla + Chuleta fresca a la bbq + Ens. de vegetales al vapor + Chips",
            "Baguette integral de pavo y mozzarella",
            "Ensalada + Jugo"
        }
    };

    private static readonly Dictionary<DayOfWeek, string?[]> MenuDesayunoSemana20260112 = new()
    {
        [DayOfWeek.Monday] = new[]
        {
            "Viveres + Compana",
            "Empanada + Jugo",
            "Croissant + Jugo"
        },
        [DayOfWeek.Tuesday] = new[]
        {
            "Viveres + Compana",
            "Empanada + Jugo",
            "Croissant + Jugo"
        },
        [DayOfWeek.Wednesday] = new string?[] { null, null, null },
        [DayOfWeek.Thursday] = new[] { "Feriado", "Feriado", "Feriado" },
        [DayOfWeek.Friday] = new[]
        {
            "Viveres + Compana",
            "Empanada + Jugo",
            "Croissant + Jugo"
        }
    };

    private static async Task EnsureMenuSemana20260112Async(AppDbContext db)
    {
        var almuerzoHorario = await db.Horarios.FirstOrDefaultAsync(h => h.Nombre == "Almuerzo");
        var desayunoHorario = await db.Horarios.FirstOrDefaultAsync(h => h.Nombre == "Desayuno");
        if (almuerzoHorario == null || desayunoHorario == null)
            return;

        var weekStart = new DateOnly(2026, 1, 12);
        var weekEnd = weekStart.AddDays(4);

        var opcionesMap = new Dictionary<string, Opcion>(StringComparer.OrdinalIgnoreCase);

        async Task<Opcion> EnsureOpcionAsync(string nombre)
        {
            var trimmed = nombre.Trim();
            if (opcionesMap.TryGetValue(trimmed, out var cached))
                return cached;

            var existente = await db.Opciones.FirstOrDefaultAsync(o => o.Nombre == trimmed);
            if (existente == null)
            {
                existente = new Opcion
                {
                    Nombre = trimmed,
                    Descripcion = trimmed,
                    Costo = 240m,
                    Precio = 240m,
                    EsSubsidiado = true,
                    LlevaItbis = true
                };
                db.Opciones.Add(existente);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(existente.Descripcion))
                    existente.Descripcion = trimmed;
                if (existente.Costo <= 0)
                    existente.Costo = 240m;
                if (existente.Precio == null)
                    existente.Precio = existente.Costo;
                if (!existente.EsSubsidiado)
                    existente.EsSubsidiado = true;
                if (!existente.LlevaItbis)
                    existente.LlevaItbis = true;
            }

            opcionesMap[trimmed] = existente;
            return existente;
        }

        async Task<OpcionMenu> BuildOpcionMenuAsync(Menu menu, DayOfWeek day, int horarioId, string?[] opciones)
        {
            var items = opciones
                .Select(o => string.IsNullOrWhiteSpace(o) ? null : o!.Trim())
                .ToArray();

            var opcionMenu = new OpcionMenu
            {
                Menu = menu,
                DiaSemana = day,
                HorarioId = horarioId,
                OpcionesMaximas = items.Count(i => !string.IsNullOrWhiteSpace(i))
            };

            if (!string.IsNullOrWhiteSpace(items.ElementAtOrDefault(0)))
                opcionMenu.OpcionA = await EnsureOpcionAsync(items[0]!);
            if (!string.IsNullOrWhiteSpace(items.ElementAtOrDefault(1)))
                opcionMenu.OpcionB = await EnsureOpcionAsync(items[1]!);
            if (!string.IsNullOrWhiteSpace(items.ElementAtOrDefault(2)))
                opcionMenu.OpcionC = await EnsureOpcionAsync(items[2]!);
            if (!string.IsNullOrWhiteSpace(items.ElementAtOrDefault(3)))
                opcionMenu.OpcionD = await EnsureOpcionAsync(items[3]!);
            if (!string.IsNullOrWhiteSpace(items.ElementAtOrDefault(4)))
                opcionMenu.OpcionE = await EnsureOpcionAsync(items[4]!);

            return opcionMenu;
        }

        var sucursales = await db.Sucursales.ToListAsync();
        foreach (var suc in sucursales)
        {
            var menu = await db.Menus
                .Include(m => m.OpcionesPorDia)
                .FirstOrDefaultAsync(m => m.SucursalId == suc.Id && m.FechaInicio == weekStart && m.FechaTermino == weekEnd);
            if (menu == null)
            {
                menu = new Menu
                {
                    FechaInicio = weekStart,
                    FechaTermino = weekEnd,
                    EmpresaId = suc.EmpresaId,
                    SucursalId = suc.Id,
                    OpcionesPorDia = new List<OpcionMenu>()
                };
                db.Menus.Add(menu);
            }

            var existentes = menu.OpcionesPorDia
                .Where(o => o.HorarioId == almuerzoHorario.Id || o.HorarioId == desayunoHorario.Id)
                .ToList();
            if (existentes.Count > 0)
                db.OpcionesMenu.RemoveRange(existentes);

            foreach (var day in DemoWeekDays)
            {
                if (MenuAlmuerzoSemana20260112.TryGetValue(day, out var almuerzo))
                {
                    var om = await BuildOpcionMenuAsync(menu, day, almuerzoHorario.Id, almuerzo);
                    menu.OpcionesPorDia.Add(om);
                }

                if (MenuDesayunoSemana20260112.TryGetValue(day, out var desayuno))
                {
                    var om = await BuildOpcionMenuAsync(menu, day, desayunoHorario.Id, desayuno);
                    menu.OpcionesPorDia.Add(om);
                }
            }
        }

        var empresas = await db.Empresas.AsNoTracking().Select(e => e.Id).ToListAsync();
        foreach (var empresaId in empresas)
        {
            var menu = await db.Menus
                .Include(m => m.OpcionesPorDia)
                .FirstOrDefaultAsync(m => m.SucursalId == null && m.EmpresaId == empresaId && m.FechaInicio == weekStart && m.FechaTermino == weekEnd);
            if (menu == null)
            {
                menu = new Menu
                {
                    FechaInicio = weekStart,
                    FechaTermino = weekEnd,
                    EmpresaId = empresaId,
                    SucursalId = null,
                    OpcionesPorDia = new List<OpcionMenu>()
                };
                db.Menus.Add(menu);
            }

            var existentes = menu.OpcionesPorDia
                .Where(o => o.HorarioId == almuerzoHorario.Id || o.HorarioId == desayunoHorario.Id)
                .ToList();
            if (existentes.Count > 0)
                db.OpcionesMenu.RemoveRange(existentes);

            foreach (var day in DemoWeekDays)
            {
                if (MenuAlmuerzoSemana20260112.TryGetValue(day, out var almuerzo))
                {
                    var om = await BuildOpcionMenuAsync(menu, day, almuerzoHorario.Id, almuerzo);
                    menu.OpcionesPorDia.Add(om);
                }

                if (MenuDesayunoSemana20260112.TryGetValue(day, out var desayuno))
                {
                    var om = await BuildOpcionMenuAsync(menu, day, desayunoHorario.Id, desayuno);
                    menu.OpcionesPorDia.Add(om);
                }
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }
}


