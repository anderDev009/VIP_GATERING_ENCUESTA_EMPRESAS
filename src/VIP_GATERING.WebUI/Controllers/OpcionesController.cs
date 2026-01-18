using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.WebUI.Services;
using System.Globalization;
using System.Text;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin")]
public class OpcionesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IOptionImageService _imageService;
    private const int ImportErrorLimit = 50;
    private const int ImportWarningLimit = 50;
    public OpcionesController(AppDbContext db, IOptionImageService imageService)
    {
        _db = db;
        _imageService = imageService;
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Opciones.Where(o => !o.Borrado).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(o =>
                o.Nombre.ToLower().Contains(ql)
                || (o.Descripcion != null && o.Descripcion.ToLower().Contains(ql))
                || (o.Codigo != null && o.Codigo.ToLower().Contains(ql))
            );
        }
        var paged = await query.OrderBy(o => o.Nombre).ToPagedResultAsync(page, pageSize);
        ViewBag.Q = q;
        return View("IndexClean", paged);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? q)
    {
        var opciones = await BuildExportQuery(q).OrderBy(o => o.Nombre).ToListAsync();
        var headers = new[] { "Codigo", "Nombre", "Descripcion", "Costo", "Precio" };
        var rows = opciones.Select(o => (IReadOnlyList<string>)new[]
        {
            o.Codigo ?? string.Empty,
            o.Nombre ?? string.Empty,
            o.Descripcion ?? string.Empty,
            o.Costo.ToString("0.00"),
            (o.Precio ?? 0m).ToString("0.00")
        }).ToList();
        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", "platos.csv");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ExportExcel(string? q)
    {
        var opciones = await BuildExportQuery(q).OrderBy(o => o.Nombre).ToListAsync();
        var headers = new[] { "Codigo", "Nombre", "Descripcion", "Costo", "Precio" };
        var rows = opciones.Select(o => (IReadOnlyList<string>)new[]
        {
            o.Codigo ?? string.Empty,
            o.Nombre ?? string.Empty,
            o.Descripcion ?? string.Empty,
            o.Costo.ToString("0.00"),
            (o.Precio ?? 0m).ToString("0.00")
        }).ToList();
        var bytes = ExportHelper.BuildExcel("Platos", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "platos.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ExportPdf(string? q)
    {
        var opciones = await BuildExportQuery(q).OrderBy(o => o.Nombre).ToListAsync();
        var headers = new[] { "Codigo", "Nombre", "Descripcion", "Costo", "Precio" };
        var rows = opciones.Select(o => (IReadOnlyList<string>)new[]
        {
            o.Codigo ?? string.Empty,
            o.Nombre ?? string.Empty,
            o.Descripcion ?? string.Empty,
            o.Costo.ToString("0.00"),
            (o.Precio ?? 0m).ToString("0.00")
        }).ToList();
        var pdf = ExportHelper.BuildPdf("Platos", headers, rows);
        return File(pdf, "application/pdf", "platos.pdf");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult ImportarProductos()
    {
        return View(new ProductosImportVM());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportarProductos(ProductosImportVM model)
    {
        if (model.Archivo == null || model.Archivo.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Debes seleccionar un archivo Excel.");
            return View(model);
        }

        var errores = new List<string>();
        var advertencias = new List<string>();
        int totalFilas = 0;
        int filasProcesadas = 0;
        int productosCreados = 0;
        int productosActualizados = 0;
        int productosSaltados = 0;

        using var stream = model.Archivo.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            ModelState.AddModelError(string.Empty, "El archivo Excel no contiene hojas.");
            return View(model);
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow == null)
        {
            ModelState.AddModelError(string.Empty, "No se encontro la fila de encabezados.");
            return View(model);
        }

        var headerMap = BuildHeaderMap(headerRow);
        if (!TryGetColumn(headerMap, out var colCodigo, "codigo")
            || !TryGetColumn(headerMap, out var colNombre, "nombre")
            || !TryGetColumn(headerMap, out var colDescripcion, "descripcion")
            || !TryGetColumn(headerMap, out var colCosto, "costo")
            || !TryGetColumn(headerMap, out var colPrecio, "precio"))
        {
            ModelState.AddModelError(string.Empty, "Faltan columnas requeridas: Codigo, Nombre, Descripcion, Costo, Precio.");
            return View(model);
        }

        var opciones = await _db.Opciones.ToListAsync();
        var opcionesPorCodigo = opciones
            .Where(o => !string.IsNullOrWhiteSpace(o.Codigo))
            .ToDictionary(o => NormalizeKey(o.Codigo!), o => o);
        var opcionesPorNombre = opciones
            .Where(o => !string.IsNullOrWhiteSpace(o.Nombre))
            .ToDictionary(o => NormalizeKey(o.Nombre!), o => o);

        var firstDataRow = headerRow.RowBelow();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRow.RowNumber();

        for (var rowNumber = firstDataRow.RowNumber(); rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty()) continue;
            totalFilas++;

            var codigo = row.Cell(colCodigo).GetString().Trim();
            var nombre = row.Cell(colNombre).GetString().Trim();
            var descripcion = row.Cell(colDescripcion).GetString().Trim();
            var costoRaw = row.Cell(colCosto).GetString().Trim();
            var precioRaw = row.Cell(colPrecio).GetString().Trim();

            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(nombre))
            {
                productosSaltados++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(nombre))
            {
                AddLimitedError(errores, $"Fila {rowNumber}: falta el nombre.");
                productosSaltados++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(descripcion))
                descripcion = nombre;

            if (!TryParseDecimal(costoRaw, out var costo)) costo = 0m;
            if (!TryParseDecimal(precioRaw, out var precio)) precio = 0m;
            if (costo <= 0m && precio <= 0m)
            {
                AddLimitedError(errores, $"Fila {rowNumber}: costo y precio invalidos.");
                productosSaltados++;
                continue;
            }

            if (costo <= 0 && precio > 0) costo = precio;
            if (precio <= 0 && costo > 0) precio = costo;

            var opcion = FindProducto(opcionesPorCodigo, opcionesPorNombre, codigo, nombre);
            if (opcion == null)
            {
                opcion = new Opcion
                {
                    Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo,
                    Nombre = nombre,
                    Descripcion = descripcion,
                    Costo = costo,
                    Precio = precio,
                    EsSubsidiado = true,
                    LlevaItbis = true
                };
                await _db.Opciones.AddAsync(opcion);
                productosCreados++;
                if (!string.IsNullOrWhiteSpace(opcion.Codigo))
                    opcionesPorCodigo[NormalizeKey(opcion.Codigo!)] = opcion;
                opcionesPorNombre[NormalizeKey(opcion.Nombre!)] = opcion;
            }
            else
            {
                opcion.Codigo = string.IsNullOrWhiteSpace(codigo) ? opcion.Codigo : codigo;
                opcion.Nombre = nombre;
                opcion.Descripcion = descripcion;
                opcion.Costo = costo;
                opcion.Precio = precio;
                opcion.EsSubsidiado = true;
                opcion.LlevaItbis = true;
                opcion.Borrado = false;
                productosActualizados++;
            }

            filasProcesadas++;
        }

        await _db.SaveChangesAsync();

        model.TotalFilas = totalFilas;
        model.FilasProcesadas = filasProcesadas;
        model.ProductosCreados = productosCreados;
        model.ProductosActualizados = productosActualizados;
        model.ProductosSaltados = productosSaltados;
        model.Errores = errores;
        model.Advertencias = advertencias;

        if (errores.Count == 0)
            TempData["Success"] = $"Importacion completada: {productosCreados} creados, {productosActualizados} actualizados.";
        else
            TempData["Error"] = $"Importacion completada con {errores.Count} errores.";

        return View(model);
    }

    public async Task<IActionResult> Create()
    {
        await LoadHorariosAsync();
        var model = new Opcion { Codigo = await GetNextPlatoCodigoAsync(), LlevaItbis = true };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opcion model, IFormFile? imagen)
    {
        var selectedHorarios = ParseHorarioIds(Request.Form);
        var errorImagen = _imageService.Validate(imagen);
        if (errorImagen != null)
        {
            ModelState.AddModelError("Imagen", errorImagen);
        }
        if (!ModelState.IsValid)
        {
            await LoadHorariosAsync(selectedHorarios);
            return View(model);
        }
        model.Codigo = string.IsNullOrWhiteSpace(model.Codigo)
            ? await GetNextPlatoCodigoAsync()
            : model.Codigo.Trim();
        model.EsSubsidiado = true;
        model.ImagenUrl = await _imageService.SaveAsync(imagen, null);
        await _db.Opciones.AddAsync(model);
        await _db.SaveChangesAsync();
        foreach (var horarioId in selectedHorarios)
        {
            _db.OpcionesHorarios.Add(new OpcionHorario { OpcionId = model.Id, HorarioId = horarioId });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var ent = await _db.Opciones.Include(o => o.Horarios).FirstOrDefaultAsync(o => o.Id == id);
        if (ent == null) return NotFound();
        await SetOptionTypeAsync(ent.Id);
        await LoadHorariosAsync(ent.Horarios.Select(h => h.HorarioId));
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opcion model, IFormFile? imagen, bool eliminarImagen = false)
    {
        var selectedHorarios = ParseHorarioIds(Request.Form);
        var ent = await _db.Opciones.FindAsync(id);
        if (ent == null) return NotFound();
        var errorImagen = _imageService.Validate(imagen);
        if (errorImagen != null)
        {
            ModelState.AddModelError("Imagen", errorImagen);
        }
        if (!ModelState.IsValid)
        {
            model.ImagenUrl = ent.ImagenUrl;
            await SetOptionTypeAsync(ent.Id);
            await LoadHorariosAsync(selectedHorarios);
            return View(model);
        }
        if (eliminarImagen)
        {
            await _imageService.DeleteAsync(ent.ImagenUrl);
            ent.ImagenUrl = null;
        }
        if (imagen != null && imagen.Length > 0)
        {
            ent.ImagenUrl = await _imageService.SaveAsync(imagen, ent.ImagenUrl);
        }
        ent.Codigo = model.Codigo; ent.Nombre = model.Nombre; ent.Descripcion = model.Descripcion; ent.Costo = model.Costo;
        ent.Precio = model.Precio; ent.EsSubsidiado = true; ent.LlevaItbis = true; ent.EsAdicional = model.EsAdicional;

        var actuales = await _db.OpcionesHorarios.Where(oh => oh.OpcionId == ent.Id).ToListAsync();
        var actualesSet = actuales.Select(oh => oh.HorarioId).ToHashSet();
        var seleccionSet = selectedHorarios.ToHashSet();
        var toRemove = actuales.Where(oh => !seleccionSet.Contains(oh.HorarioId)).ToList();
        if (toRemove.Count > 0)
            _db.OpcionesHorarios.RemoveRange(toRemove);
        var toAdd = seleccionSet.Except(actualesSet).Select(h => new OpcionHorario { OpcionId = ent.Id, HorarioId = h }).ToList();
        if (toAdd.Count > 0)
            await _db.OpcionesHorarios.AddRangeAsync(toAdd);

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task SetOptionTypeAsync(int optionId)
    {
        var esAdicional = await _db.MenusAdicionales.AnyAsync(m => m.OpcionId == optionId);
        ViewBag.EsAdicional = esAdicional;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ent = await _db.Opciones.FindAsync(id);
        if (ent != null)
        {
            await _imageService.DeleteAsync(ent.ImagenUrl);
            ent.ImagenUrl = null;
            ent.Borrado = true;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadHorariosAsync(IEnumerable<int>? selected = null)
    {
        var horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        ViewBag.Horarios = horarios;
        ViewBag.HorarioIds = selected?.ToList() ?? new List<int>();
    }

    private static List<int> ParseHorarioIds(IFormCollection form)
    {
        var values = form["HorarioIds"];
        var ids = new List<int>();
        foreach (var value in values)
        {
            if (int.TryParse(value, out var id))
                ids.Add(id);
        }
        return ids.Distinct().ToList();
    }

    private IQueryable<Opcion> BuildExportQuery(string? q)
    {
        var query = _db.Opciones.Where(o => !o.Borrado).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(o =>
                o.Nombre.ToLower().Contains(ql)
                || (o.Descripcion != null && o.Descripcion.ToLower().Contains(ql))
                || (o.Codigo != null && o.Codigo.ToLower().Contains(ql))
            );
        }
        return query;
    }

    private async Task<string> GetNextPlatoCodigoAsync()
    {
        var codigos = await _db.Opciones
            .AsNoTracking()
            .Where(o => o.Codigo != null && o.Codigo.StartsWith("P"))
            .Select(o => o.Codigo!)
            .ToListAsync();

        var max = 0;
        var maxDigits = 0;
        foreach (var codigo in codigos)
        {
            if (codigo.Length < 2) continue;
            var digits = new string(codigo.Skip(1).TakeWhile(char.IsDigit).ToArray());
            if (digits.Length == 0) continue;
            if (int.TryParse(digits, out var val))
            {
                if (val > max) max = val;
                if (digits.Length > maxDigits) maxDigits = digits.Length;
            }
        }

        var width = Math.Max(4, maxDigits);
        var next = max + 1;
        return $"P{next.ToString().PadLeft(width, '0')}";
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>();
        var lastCell = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var col = 1; col <= lastCell; col++)
        {
            var raw = headerRow.Cell(col).GetString().Trim();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var key = NormalizeKey(raw);
            map[key] = col;
        }
        return map;
    }

    private static bool TryGetColumn(Dictionary<string, int> headerMap, out int col, params string[] tokens)
    {
        if (tokens.Length == 0)
        {
            col = 0;
            return false;
        }
        foreach (var entry in headerMap)
        {
            var match = true;
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (!entry.Key.Contains(token))
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                col = entry.Value;
                return true;
            }
        }
        col = 0;
        return false;
    }

    private static string NormalizeKey(string value)
    {
        var cleaned = RemoveDiacritics(value ?? string.Empty).ToLowerInvariant();
        var sb = new StringBuilder(cleaned.Length);
        foreach (var ch in cleaned)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var inv))
        {
            result = inv;
            return true;
        }
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var local))
        {
            result = local;
            return true;
        }
        return false;
    }

    private static Opcion? FindProducto(Dictionary<string, Opcion> porCodigo, Dictionary<string, Opcion> porNombre, string codigo, string nombre)
    {
        var nombreKey = !string.IsNullOrWhiteSpace(nombre) ? NormalizeKey(nombre) : null;
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var key = NormalizeKey(codigo);
            if (porCodigo.TryGetValue(key, out var encontrado))
            {
                if (nombreKey == null)
                    return encontrado;
                var encontradoNombre = !string.IsNullOrWhiteSpace(encontrado.Nombre) ? NormalizeKey(encontrado.Nombre) : null;
                if (encontradoNombre == nombreKey)
                    return encontrado;
            }
        }
        if (nombreKey != null)
        {
            if (porNombre.TryGetValue(nombreKey, out var encontrado))
                return encontrado;
        }
        return null;
    }

    private static void AddLimitedError(List<string> errores, string mensaje)
    {
        if (errores.Count < ImportErrorLimit)
            errores.Add(mensaje);
    }

    private static void AddLimitedWarning(List<string> advertencias, string mensaje)
    {
        if (advertencias.Count < ImportWarningLimit)
            advertencias.Add(mensaje);
    }
}
