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
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
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
                ws.Cell(r + 2, c + 1).Value = c < row.Count ? row[c] : string.Empty;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] BuildPdf(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4.Landscape());
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Header().Text(title).FontSize(14).SemiBold();
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (var i = 0; i < headers.Count; i++)
                            columns.RelativeColumn();
                    });
                    table.Header(h =>
                    {
                        foreach (var header in headers)
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(header).SemiBold();
                        }
                    });
                    foreach (var row in rows)
                    {
                        for (var i = 0; i < headers.Count; i++)
                        {
                            var value = i < row.Count ? row[i] : string.Empty;
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(value);
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
}
