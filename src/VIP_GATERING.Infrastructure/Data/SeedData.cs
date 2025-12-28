using Microsoft.EntityFrameworkCore;
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
                var sucursalesPorEmpresa = await db.Sucursales
                    .AsNoTracking()
                    .GroupBy(s => s.EmpresaId)
                    .Select(g => new
                    {
                        EmpresaId = g.Key,
                        SucursalId = g.Select(s => s.Id).FirstOrDefault(),
                        SucursalNombre = g.Select(s => s.Nombre).FirstOrDefault()
                    })
                    .ToListAsync();
                var existentes = await db.Localizaciones
                    .AsNoTracking()
                    .Select(l => new { l.SucursalId, l.EmpresaId, l.Nombre })
                    .ToListAsync();
                var existentesSet = new HashSet<string>(
                    existentes.Select(e => $"{e.SucursalId:N}|{e.EmpresaId:N}|{e.Nombre}"),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var suc in sucursalesPorEmpresa)
                {
                    if (suc.SucursalId == Guid.Empty) continue;
                    foreach (var nombre in nombresLoc)
                    {
                        var key = $"{suc.SucursalId:N}|{suc.EmpresaId:N}|{nombre}";
                        if (existentesSet.Contains(key)) continue;
                        var sucursalNombre = string.IsNullOrWhiteSpace(suc.SucursalNombre) ? "filial" : suc.SucursalNombre;
                        db.Localizaciones.Add(new Localizacion
                        {
                            Nombre = nombre,
                            EmpresaId = suc.EmpresaId,
                            SucursalId = suc.SucursalId,
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


