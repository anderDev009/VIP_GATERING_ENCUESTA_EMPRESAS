using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VIP_GATERING.WebUI.Services;

public static class ExportHelper
{
    public static byte[] BuildCsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            var output = new string[headers.Count];
            for (var i = 0; i < headers.Count; i++)
            {
                var value = i < row.Count ? row[i] : string.Empty;
                if (IsNumericHeader(headers[i]) && TryParseNumber(value, out var number))
                    output[i] = number.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                else
                    output[i] = value ?? string.Empty;
            }
            sb.AppendLine(string.Join(",", output.Select(EscapeCsv)));
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] BuildExcel(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Datos" : sheetName);
        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < headers.Count; c++)
            {
                var value = c < row.Count ? row[c] : string.Empty;
                if (IsNumericHeader(headers[c]) && TryParseNumber(value, out var number))
                    ws.Cell(r + 2, c + 1).Value = number;
                else
                    ws.Cell(r + 2, c + 1).Value = value ?? string.Empty;
            }
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] BuildPdf(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var dense = headers.Count > 10;
        var fontSize = dense ? 7 : 9;
        var cellPadding = dense ? 2 : 4;
        var pageSize = headers.Count > 12 ? PageSizes.A3.Landscape() : PageSizes.A4.Landscape();
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(pageSize);
                page.DefaultTextStyle(x => x.FontSize(fontSize));
                page.Header().Text(title).FontSize(dense ? 12 : 14).SemiBold();
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (var i = 0; i < headers.Count; i++)
                        {
                            var header = headers[i];
                            var h = header.ToLowerInvariant();
                            var wide = h.Contains("opcion") || h.Contains("localizacion") || h.Contains("plato") || h.Contains("descripcion");
                            columns.RelativeColumn(wide ? 2 : 1);
                        }
                    });
                    table.Header(h =>
                    {
                        foreach (var header in headers)
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(cellPadding).Text(header).SemiBold();
                        }
                    });
                    foreach (var row in rows)
                    {
                        for (var i = 0; i < headers.Count; i++)
                        {
                            var value = i < row.Count ? row[i] : string.Empty;
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(cellPadding).Text(value);
                        }
                    }
                });
            });
        }).GeneratePdf();
    }

    private static string EscapeCsv(string? value)
    {
        var v = value ?? string.Empty;
        var needsQuotes = v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r');
        if (v.Contains('"'))
            v = v.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{v}\"" : v;
    }

    private static bool IsNumericHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return false;
        var key = RemoveDiacritics(header).ToLowerInvariant();
        return key.Contains("costo")
            || key.Contains("precio")
            || key.Contains("base")
            || key.Contains("itbis")
            || key.Contains("total")
            || key.Contains("monto")
            || key.Contains("cantidad")
            || key.Contains("consumo")
            || key.Contains("beneficio")
            || key.Contains("paga")
            || key.Contains("subsidio");
    }

    private static bool TryParseNumber(string? value, out decimal number)
    {
        number = 0m;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var cleaned = value.Replace("RD$", string.Empty)
            .Replace("$", string.Empty)
            .Trim();
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out number)
            || decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("es-DO"), out number);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
