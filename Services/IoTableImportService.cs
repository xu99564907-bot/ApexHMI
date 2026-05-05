using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using Serilog;
using Serilog.Context;

namespace ApexHMI.Services;

public class IoTableImportService : IIoTableImportService
{
    private static readonly List<string> StructuredHeaders =
    [
        "输入模块",
        "输入地址",
        "输入工位",
        "输入变量注释",
        "输入备注",
        "输出模块",
        "输出地址",
        "输出工位",
        "输出变量注释",
        "输出备注"
    ];

    private readonly IIoTableParser _parser;

    public IoTableImportService(IIoTableParser parser)
    {
        _parser = parser;
    }

    public async Task<IoTableImportResult> ImportAsync(string filePath)
    {
        using var _ = LogContext.PushProperty("CorrelationId", Guid.NewGuid().ToString("N"));
        var sw = Stopwatch.StartNew();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var result = extension switch
        {
            ".csv" or ".txt" => await ImportDelimitedAsync(filePath),
            ".xlsx" => await ImportExcelAsync(filePath),
            _ => throw new NotSupportedException("当前版本支持 CSV/TXT/XLSX 格式的 IO 表。")
        };
        Log.Information("IO 表导入完成 elapsedMs={ElapsedMs} count={Count} source={Source}", sw.ElapsedMilliseconds, result.Rows.Count, filePath);
        return result;
    }

    public async Task SaveAsync(string filePath, IEnumerable<IoTableRow> rows, IReadOnlyList<string>? headers, int encodingCodePage)
    {
        var rowList = rows.ToList();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (extension == ".xlsx")
        {
            await SaveWorkbookAsync(filePath, rowList, ResolveHeaders(headers, rowList));
            return;
        }

        var encoding = TryGetEncoding(encodingCodePage) ?? Encoding.UTF8;
        var headerColumns = ResolveHeaders(headers, rowList);
        var lines = new List<string>
        {
            string.Join(",", headerColumns.Select(EscapeCsv))
        };

        foreach (var row in rowList)
        {
            var values = headerColumns.Count >= StructuredHeaders.Count
                ? GetStructuredRowValues(row)
                : GetLegacyRowValues(row);
            lines.Add(string.Join(",", values.Select(EscapeCsv)));
        }

        await Compat.WriteAllTextAsync(filePath, string.Join(Environment.NewLine, lines), encoding);
    }

    private async Task<IoTableImportResult> ImportDelimitedAsync(string filePath)
    {
        var encoding = DetectEncoding(filePath);
        var rawText = await Compat.ReadAllTextAsync(filePath, encoding);
        var rows = rawText
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(SplitLine)
            .Cast<IReadOnlyList<string>>()
            .ToList();

        var parsed = _parser.Parse(rows);
        return new IoTableImportResult
        {
            SourceFilePath = filePath,
            EncodingCodePage = encoding.CodePage,
            Headers = parsed.Headers,
            Rows = parsed.Rows
        };
    }

    private async Task<IoTableImportResult> ImportExcelAsync(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var rows = LoadWorksheetRows(archive, "IO表");
        var parsed = _parser.Parse(rows);
        var axisEntries = LoadAxisConfigEntries(archive);
        return new IoTableImportResult
        {
            SourceFilePath = filePath,
            EncodingCodePage = 65001,
            Headers = parsed.Headers,
            Rows = parsed.Rows,
            AxisConfigEntries = axisEntries
        };
    }

    private static async Task SaveWorkbookAsync(string filePath, IReadOnlyList<IoTableRow> rows, IReadOnlyList<string> headers)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        WriteXmlEntry(archive, "[Content_Types].xml", BuildContentTypes());
        WriteXmlEntry(archive, "_rels/.rels", BuildRootRelationships());
        WriteXmlEntry(archive, "xl/workbook.xml", BuildWorkbook());
        WriteXmlEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
        WriteXmlEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheet(headers, rows));
    }

    private static List<AxisConfigEntry> LoadAxisConfigEntries(ZipArchive archive)
    {
        var entries = new List<AxisConfigEntry>();
        IReadOnlyList<IReadOnlyList<string>>? rows;
        try
        {
            rows = TryLoadWorksheetRows(archive, "轴名称");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "读取 Excel 轴配置 Sheet 失败，忽略轴配置。");
            return entries;
        }

        if (rows is null || rows.Count < 3)
        {
            return entries;
        }

        // 第1行是标题("轴参数")，第2行是表头(编号/名称/点位1~点位15)
        // 数据从第3行开始
        var headerRow = rows[1];
        var nameColIndex = FindColumnIndex(headerRow, "名称");
        var indexColIndex = FindColumnIndex(headerRow, "编号");
        if (indexColIndex < 0) indexColIndex = 0;
        if (nameColIndex < 0) nameColIndex = 1;

        // 找出点位列索引（点位1~点位15）
        var pointColumns = new List<(int ColIndex, int PointNumber)>();
        for (var col = 0; col < headerRow.Count; col++)
        {
            var header = headerRow[col].Trim();
            if (header.StartsWith("点位", StringComparison.Ordinal))
            {
                var numText = header["点位".Length..];
                if (int.TryParse(numText, out var pointNum) && pointNum >= 1)
                {
                    pointColumns.Add((col, pointNum));
                }
            }
        }

        pointColumns.Sort((a, b) => a.PointNumber.CompareTo(b.PointNumber));

        for (var i = 2; i < rows.Count; i++)
        {
            var row = rows[i];
            var indexCell = GetColumn(row, indexColIndex).Trim();
            var nameCell = nameColIndex < row.Count ? row[nameColIndex].Trim() : string.Empty;

            // 解析轴编号：从"轴0"、"轴1"之类的文字中提取数字
            var axisIndex = ParseAxisIndex(indexCell);
            if (axisIndex < 0) continue;

            // 如果没有名称，跳过（空轴位）
            if (string.IsNullOrWhiteSpace(nameCell)) continue;

            var entry = new AxisConfigEntry
            {
                Index = axisIndex,
                Name = nameCell
            };

            foreach (var (colIndex, pointNumber) in pointColumns)
            {
                var label = colIndex < row.Count ? row[colIndex].Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    entry.Points.Add(new AxisPointLabel(pointNumber, label));
                }
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static int ParseAxisIndex(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return -1;

        // 匹配 "轴0"、"轴1"、"AX01" 等格式
        var match = Regex.Match(text, @"(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var index) ? index : -1;
    }

    /// <summary>尝试加载指定名称的 Sheet，找不到时返回 null</summary>
    private static IReadOnlyList<IReadOnlyList<string>>? TryLoadWorksheetRows(ZipArchive archive, string sheetName)
    {
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var workbookRels = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var sharedStrings = LoadSharedStrings(archive);

        XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        var sheetElement = workbook
            .Descendants(mainNs + "sheet")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));

        if (sheetElement is null) return null;

        var relationId = (string?)sheetElement.Attribute(relNs + "id");
        if (string.IsNullOrWhiteSpace(relationId)) return null;

        var relation = workbookRels
            .Descendants(pkgRelNs + "Relationship")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("Id"), relationId, StringComparison.Ordinal));
        if (relation is null) return null;

        var target = ((string?)relation.Attribute("Target") ?? string.Empty).Replace('\\', '/');
        if (!target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
        {
            target = $"xl/{target.TrimStart('/')}";
        }

        var entry = archive.GetEntry(target);
        if (entry is null) return null;

        using var entryStream = entry.Open();
        var sheet = XDocument.Load(entryStream);
        var rows = new List<IReadOnlyList<string>>();
        var sheetRows = sheet.Descendants(mainNs + "row").ToList();
        var maxColumn = 0;

        foreach (var row in sheetRows)
        {
            foreach (var cell in row.Elements(mainNs + "c"))
            {
                maxColumn = Math.Max(maxColumn, GetColumnIndex((string?)cell.Attribute("r")));
            }
        }

        foreach (var row in sheetRows)
        {
            var values = Enumerable.Repeat(string.Empty, maxColumn + 1).ToArray();
            foreach (var cell in row.Elements(mainNs + "c"))
            {
                var index = GetColumnIndex((string?)cell.Attribute("r"));
                values[index] = GetCellValue(cell, sharedStrings, mainNs);
            }

            rows.Add(values);
        }

        return rows;
    }

    private static IReadOnlyList<IReadOnlyList<string>> LoadWorksheetRows(ZipArchive archive, string preferredSheetName)
    {
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var workbookRels = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var sharedStrings = LoadSharedStrings(archive);

        XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        var sheetElement = workbook
            .Descendants(mainNs + "sheet")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), preferredSheetName, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Descendants(mainNs + "sheet").FirstOrDefault()
            ?? throw new InvalidOperationException("未在 Excel 文件中找到可用工作表。");

        var relationId = (string?)sheetElement.Attribute(relNs + "id")
            ?? throw new InvalidOperationException("Excel 工作表关系丢失。");

        var relation = workbookRels
            .Descendants(pkgRelNs + "Relationship")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("Id"), relationId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("未找到 Excel 工作表目标。");

        var target = ((string?)relation.Attribute("Target") ?? string.Empty).Replace('\\', '/');
        if (!target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
        {
            target = $"xl/{target.TrimStart('/')}";
        }

        var sheet = LoadXml(archive, target);
        var rows = new List<IReadOnlyList<string>>();
        var sheetRows = sheet.Descendants(mainNs + "row").ToList();
        var maxColumn = 0;

        foreach (var row in sheetRows)
        {
            foreach (var cell in row.Elements(mainNs + "c"))
            {
                maxColumn = Math.Max(maxColumn, GetColumnIndex((string?)cell.Attribute("r")));
            }
        }

        foreach (var row in sheetRows)
        {
            var values = Enumerable.Repeat(string.Empty, maxColumn + 1).ToArray();
            foreach (var cell in row.Elements(mainNs + "c"))
            {
                var index = GetColumnIndex((string?)cell.Attribute("r"));
                values[index] = GetCellValue(cell, sharedStrings, mainNs);
            }

            rows.Add(values);
        }

        return rows;
    }

    private static List<string> LoadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return doc
            .Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToList();
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new FileNotFoundException($"缺少 Excel 结构文件：{path}");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string GetCellValue(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace ns)
    {
        var type = (string?)cell.Attribute("t");
        return type switch
        {
            "s" => ResolveSharedString(cell.Element(ns + "v")?.Value, sharedStrings),
            "inlineStr" => string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)),
            _ => cell.Element(ns + "v")?.Value?.Trim() ?? string.Empty
        };
    }

    private static string ResolveSharedString(string? indexText, IReadOnlyList<string> sharedStrings)
    {
        return int.TryParse(indexText, out var index) && index >= 0 && index < sharedStrings.Count
            ? sharedStrings[index]
            : string.Empty;
    }

    private static XDocument BuildContentTypes()
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        return new XDocument(
            new XElement(ns + "Types",
                new XElement(ns + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ns + "Default",
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(ns + "Override",
                    new XAttribute("PartName", "/xl/workbook.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ns + "Override",
                    new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));
    }

    private static XDocument BuildRootRelationships()
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(
            new XElement(ns + "Relationships",
                new XElement(ns + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml"))));
    }

    private static XDocument BuildWorkbook()
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        return new XDocument(
            new XElement(ns + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", relNs),
                new XElement(ns + "sheets",
                    new XElement(ns + "sheet",
                        new XAttribute("name", "IO表"),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(relNs + "id", "rId1")))));
    }

    private static XDocument BuildWorkbookRelationships()
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(
            new XElement(ns + "Relationships",
                new XElement(ns + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml"))));
    }

    private static XDocument BuildWorksheet(IReadOnlyList<string> headers, IReadOnlyList<IoTableRow> rows)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var dataRows = new List<IReadOnlyList<string>> { headers };
        dataRows.AddRange(rows.Select(row => headers.Count >= StructuredHeaders.Count ? GetStructuredRowValues(row) : GetLegacyRowValues(row)));

        return new XDocument(
            new XElement(ns + "worksheet",
                new XElement(ns + "sheetData",
                    dataRows.Select((values, rowIndex) =>
                        new XElement(ns + "row",
                            new XAttribute("r", rowIndex + 1),
                            values.Select((value, columnIndex) =>
                                new XElement(ns + "c",
                                    new XAttribute("r", $"{GetExcelColumnName(columnIndex)}{rowIndex + 1}"),
                                    new XAttribute("t", "inlineStr"),
                                    new XElement(ns + "is",
                                        new XElement(ns + "t", value ?? string.Empty)))))))));
    }

    private static void WriteXmlEntry(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        document.Save(writer);
    }

    private static IReadOnlyList<string> ResolveHeaders(IReadOnlyList<string>? headers, IReadOnlyList<IoTableRow> rows)
    {
        if (headers is { Count: >= 10 })
        {
            return headers.Take(10).ToList();
        }

        if (headers is { Count: >= 4 } && !HasStructuredColumns(rows))
        {
            return headers.Take(4).ToList();
        }

        return HasStructuredColumns(rows)
            ? new List<string>(StructuredHeaders)
            : new List<string> { "输入地址", "输入变量", "输出地址", "输出变量" };
    }

    private static bool HasStructuredColumns(IEnumerable<IoTableRow> rows)
    {
        return rows.Any(row =>
            !string.IsNullOrWhiteSpace(row.InputModule) ||
            !string.IsNullOrWhiteSpace(row.InputStation) ||
            !string.IsNullOrWhiteSpace(row.InputRemark) ||
            !string.IsNullOrWhiteSpace(row.OutputModule) ||
            !string.IsNullOrWhiteSpace(row.OutputStation) ||
            !string.IsNullOrWhiteSpace(row.OutputRemark));
    }

    private static IReadOnlyList<string> GetStructuredRowValues(IoTableRow row)
    {
        return
        [
            row.InputModule,
            row.InputAddress,
            row.InputStation,
            row.InputComment,
            row.InputRemark,
            row.OutputModule,
            row.OutputAddress,
            row.OutputStation,
            row.OutputComment,
            row.OutputRemark
        ];
    }

    private static IReadOnlyList<string> GetLegacyRowValues(IoTableRow row)
    {
        return
        [
            row.InputAddress,
            row.InputComment,
            row.OutputAddress,
            row.OutputComment
        ];
    }

    private static Encoding DetectEncoding(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode;
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            Log.Debug("文件不是严格 UTF-8 编码，继续尝试 GB18030/GBK。File={FilePath}", filePath);
        }

        foreach (var codePage in new[] { 54936, 936 })
        {
            var encoding = TryGetEncoding(codePage);
            if (encoding is not null && LooksLikeIoHeader(encoding.GetString(bytes)))
            {
                return encoding;
            }
        }

        return Encoding.Default;
    }

    private static Encoding? TryGetEncoding(int codePage)
    {
        try
        {
            return Encoding.GetEncoding(codePage);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "无法获取编码 CodePage={CodePage}", codePage);
            return null;
        }
    }

    private static bool LooksLikeIoHeader(string text)
    {
        var firstLine = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return firstLine.Contains("输入")
            || firstLine.Contains("输出")
            || firstLine.Contains("地址")
            || firstLine.Contains("变量")
            || firstLine.Contains("模块");
    }

    private static int FindColumnIndex(IReadOnlyList<string> row, string header)
    {
        for (var i = 0; i < row.Count; i++)
        {
            if (string.Equals(row[i].Trim(), header, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetColumn(IReadOnlyList<string> columns, int index)
    {
        return index >= 0 && index < columns.Count ? columns[index].Trim() : string.Empty;
    }

    private static List<string> SplitLine(string line)
    {
        var delimiter = line.Contains('\t') ? '\t' : ',';
        var result = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                result.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        result.Add(builder.ToString());
        return result;
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\r') && !text.Contains('\n'))
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return 0;
        }

        var column = 0;
        foreach (var ch in cellReference)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            column = column * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return Math.Max(0, column - 1);
    }

    private static string GetExcelColumnName(int index)
    {
        var dividend = index + 1;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar(65 + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }
}
