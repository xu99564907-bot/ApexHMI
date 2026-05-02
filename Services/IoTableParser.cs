using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ApexHMI.Interfaces;
using ApexHMI.Models;

namespace ApexHMI.Services;

/// <summary>
/// IO 表解析器的默认实现，将二维字符串表解析为 IoTableRow。
/// </summary>
public class IoTableParser : IIoTableParser
{
    private static readonly Regex IoAddressPattern = new(@"^[A-Za-z]{1,4}\d+(?:\.\d+)?$", RegexOptions.Compiled);
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

    public IoTableParseResult Parse(IReadOnlyList<IReadOnlyList<string>> sourceRows)
    {
        var materialized = sourceRows
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .Select(row => row.Select(cell => cell?.Trim() ?? string.Empty).ToList())
            .Cast<IReadOnlyList<string>>()
            .ToList();

        if (materialized.Count == 0)
        {
            return new IoTableParseResult
            {
                Headers = new List<string>(StructuredHeaders),
                Rows = new List<IoTableRow>()
            };
        }

        var headerIndex = materialized.FindIndex(IsHeaderRow);
        if (headerIndex < 0)
        {
            return ParseLegacyFormat(materialized, 0, hasHeader: false);
        }

        var headerRow = materialized[headerIndex];
        if (IsStructuredHeaderRow(headerRow))
        {
            return ParseStructuredFormat(materialized, headerIndex);
        }

        if (IsModuleSheetHeaderRow(headerRow))
        {
            return ParseModuleSheetFormat(materialized, headerIndex);
        }

        return ParseLegacyFormat(materialized, headerIndex, hasHeader: true);
    }

    // ========== Structured format ==========

    private static IoTableParseResult ParseStructuredFormat(IReadOnlyList<IReadOnlyList<string>> rows, int headerIndex)
    {
        var headerRow = rows[headerIndex];
        var map = BuildHeaderIndexMap(headerRow);
        var parsed = new List<IoTableRow>();

        for (var i = headerIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var item = new IoTableRow
            {
                InputModule = GetColumn(row, map, "输入模块"),
                InputAddress = GetColumn(row, map, "输入地址"),
                InputStation = GetColumn(row, map, "输入工位"),
                InputComment = GetColumn(row, map, "输入变量注释"),
                InputRemark = GetColumn(row, map, "输入备注"),
                OutputModule = GetColumn(row, map, "输出模块"),
                OutputAddress = GetColumn(row, map, "输出地址"),
                OutputStation = GetColumn(row, map, "输出工位"),
                OutputComment = GetColumn(row, map, "输出变量注释"),
                OutputRemark = GetColumn(row, map, "输出备注")
            };

            if (HasStructuredContent(item))
            {
                parsed.Add(item);
            }
        }

        return new IoTableParseResult
        {
            Headers = new List<string>(StructuredHeaders),
            Rows = NormalizeRows(parsed)
        };
    }

    // ========== Module sheet format ==========

    private static IoTableParseResult ParseModuleSheetFormat(IReadOnlyList<IReadOnlyList<string>> rows, int headerIndex)
    {
        var headerRow = rows[headerIndex];
        var inputSectionCol = FindColumnIndex(headerRow, "模块编号");
        var inputAddressCol = FindColumnIndex(headerRow, "输入地址");
        var inputStationCol = FindNextColumnIndex(headerRow, inputAddressCol + 1, "工位");
        var inputCommentCol = FindNextColumnIndex(headerRow, inputStationCol + 1, "变量注释");
        var inputRemarkCol = FindNextColumnIndex(headerRow, inputCommentCol + 1, "备注");

        var outputLabelCol = FindColumnIndex(headerRow, "输出地址") - 1;
        var outputAddressCol = FindColumnIndex(headerRow, "输出地址");
        var outputStationCol = FindNextColumnIndex(headerRow, outputAddressCol + 1, "工位");
        var outputCommentCol = FindNextColumnIndex(headerRow, outputStationCol + 1, "变量注释");
        var outputRemarkCol = FindNextColumnIndex(headerRow, outputCommentCol + 1, "备注");

        var parsed = new List<IoTableRow>();
        var currentInputModule = string.Empty;
        var currentOutputModule = string.Empty;

        for (var i = headerIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var inputCandidate = GetColumn(row, inputAddressCol);
            var outputCandidate = GetColumn(row, outputAddressCol);

            if (LooksLikeModuleValue(inputCandidate))
            {
                currentInputModule = inputCandidate;
            }

            if (LooksLikeModuleValue(outputCandidate))
            {
                currentOutputModule = outputCandidate;
            }

            var item = new IoTableRow();

            if (LooksLikeIoAddress(inputCandidate))
            {
                item.InputModule = currentInputModule;
                item.InputAddress = inputCandidate;
                item.InputStation = GetColumn(row, inputStationCol);
                item.InputComment = GetColumn(row, inputCommentCol);
                item.InputRemark = GetColumn(row, inputRemarkCol);
            }

            if (LooksLikeIoAddress(outputCandidate))
            {
                item.OutputModule = currentOutputModule;
                item.OutputAddress = outputCandidate;
                item.OutputStation = GetColumn(row, outputStationCol);
                item.OutputComment = GetColumn(row, outputCommentCol);
                item.OutputRemark = GetColumn(row, outputRemarkCol);
            }

            if (!HasStructuredContent(item))
            {
                var inputSectionLabel = GetColumn(row, inputSectionCol);
                var outputSectionLabel = outputLabelCol >= 0 ? GetColumn(row, outputLabelCol) : string.Empty;
                if (LooksLikeModuleValue(inputSectionLabel))
                {
                    currentInputModule = inputCandidate;
                }

                if (LooksLikeModuleValue(outputSectionLabel))
                {
                    currentOutputModule = outputCandidate;
                }

                continue;
            }

            parsed.Add(item);
        }

        return new IoTableParseResult
        {
            Headers = new List<string>(StructuredHeaders),
            Rows = NormalizeRows(parsed)
        };
    }

    // ========== Legacy format ==========

    private static IoTableParseResult ParseLegacyFormat(IReadOnlyList<IReadOnlyList<string>> rows, int headerIndex, bool hasHeader)
    {
        var parsed = new List<IoTableRow>();
        var startIndex = hasHeader ? headerIndex + 1 : headerIndex;
        for (var i = startIndex; i < rows.Count; i++)
        {
            var row = rows[i];
            var item = new IoTableRow
            {
                InputAddress = GetColumn(row, 0),
                InputComment = GetColumn(row, 1),
                OutputAddress = GetColumn(row, 2),
                OutputComment = GetColumn(row, 3)
            };

            if (HasLegacyContent(item))
            {
                parsed.Add(item);
            }
        }

        var headers = hasHeader
            ? rows[headerIndex].Take(4).ToList()
            : new List<string> { "输入地址", "输入变量", "输出地址", "输出变量" };

        return new IoTableParseResult
        {
            Headers = headers,
            Rows = NormalizeRows(parsed)
        };
    }

    // ========== Normalization ==========

    private static List<IoTableRow> NormalizeRows(IReadOnlyList<IoTableRow> rows)
    {
        if (rows.Count == 0)
        {
            return new List<IoTableRow>();
        }

        var inputs = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.InputAddress))
            .Select(r => new IoSignal(r.InputModule, r.InputAddress, r.InputStation, r.InputComment, r.InputRemark))
            .ToList();
        var outputs = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.OutputAddress))
            .Select(r => new IoSignal(r.OutputModule, r.OutputAddress, r.OutputStation, r.OutputComment, r.OutputRemark))
            .ToList();

        var normalizedInputs = NormalizeSignals(inputs, "IX");
        var normalizedOutputs = NormalizeSignals(outputs, "QX");
        var max = Math.Max(normalizedInputs.Count, normalizedOutputs.Count);
        var normalizedRows = new List<IoTableRow>(max);

        for (var i = 0; i < max; i++)
        {
            var input = i < normalizedInputs.Count ? normalizedInputs[i] : null;
            var output = i < normalizedOutputs.Count ? normalizedOutputs[i] : null;
            normalizedRows.Add(new IoTableRow
            {
                InputModule = input?.Module ?? string.Empty,
                InputAddress = input?.Address ?? string.Empty,
                InputStation = input?.Station ?? string.Empty,
                InputComment = input?.Comment ?? string.Empty,
                InputRemark = input?.Remark ?? string.Empty,
                OutputModule = output?.Module ?? string.Empty,
                OutputAddress = output?.Address ?? string.Empty,
                OutputStation = output?.Station ?? string.Empty,
                OutputComment = output?.Comment ?? string.Empty,
                OutputRemark = output?.Remark ?? string.Empty
            });
        }

        return normalizedRows;
    }

    private static List<IoSignal> NormalizeSignals(IReadOnlyList<IoSignal> source, string expectedPrefix)
    {
        if (source.Count == 0)
        {
            return new List<IoSignal>();
        }

        var normalized = new List<IoSignal>();
        for (var i = 0; i < source.Count; i++)
        {
            var current = source[i];
            normalized.Add(current);

            if (!TryParseAddress(current.Address, expectedPrefix, out var currentWord))
            {
                continue;
            }

            if (i < source.Count - 1 && TryParseAddress(source[i + 1].Address, expectedPrefix, out var nextWord))
            {
                var fillWord = currentWord;
                while (fillWord % 2 == 0 && nextWord - fillWord > 1)
                {
                    fillWord++;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        normalized.Add(new IoSignal(current.Module, $"{expectedPrefix}{fillWord}.{bit}", current.Station, string.Empty, string.Empty));
                    }
                }
            }
            else if (i == source.Count - 1 && currentWord % 2 == 0)
            {
                var fillWord = currentWord + 1;
                for (var bit = 0; bit < 8; bit++)
                {
                    normalized.Add(new IoSignal(current.Module, $"{expectedPrefix}{fillWord}.{bit}", current.Station, string.Empty, string.Empty));
                }
            }
        }

        return normalized;
    }

    private static bool TryParseAddress(string address, string expectedPrefix, out int word)
    {
        word = -1;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var normalized = address.Trim().TrimStart('%').ToUpperInvariant();
        if (!normalized.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var dotIndex = normalized.IndexOf('.');
        if (dotIndex <= expectedPrefix.Length)
        {
            return false;
        }

        return int.TryParse(normalized.Substring(expectedPrefix.Length, dotIndex - expectedPrefix.Length), out word);
    }

    // ========== Header detection ==========

    private static bool IsHeaderRow(IReadOnlyList<string> columns)
    {
        var combined = string.Join("|", columns).ToLowerInvariant();
        return combined.Contains("输入")
            || combined.Contains("输出")
            || combined.Contains("地址")
            || combined.Contains("注释")
            || combined.Contains("comment")
            || combined.Contains("address")
            || combined.Contains("模块");
    }

    private static bool IsStructuredHeaderRow(IReadOnlyList<string> columns)
    {
        return FindColumnIndex(columns, "输入模块") >= 0
            && FindColumnIndex(columns, "输入地址") >= 0
            && FindColumnIndex(columns, "输出模块") >= 0
            && FindColumnIndex(columns, "输出地址") >= 0;
    }

    private static bool IsModuleSheetHeaderRow(IReadOnlyList<string> columns)
    {
        return FindColumnIndex(columns, "模块编号") >= 0
            && FindColumnIndex(columns, "输入地址") >= 0
            && FindColumnIndex(columns, "输出地址") >= 0;
    }

    // ========== Column helpers ==========

    private static Dictionary<string, int> BuildHeaderIndexMap(IReadOnlyList<string> headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headerRow.Count; i++)
        {
            var text = headerRow[i].Trim();
            if (!string.IsNullOrWhiteSpace(text) && !map.ContainsKey(text))
            {
                map[text] = i;
            }
        }

        return map;
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

    private static int FindNextColumnIndex(IReadOnlyList<string> row, int startIndex, string header)
    {
        for (var i = Math.Max(startIndex, 0); i < row.Count; i++)
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

    private static string GetColumn(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> map, string header)
    {
        return map.TryGetValue(header, out var index) ? GetColumn(columns, index) : string.Empty;
    }

    // ========== Content validation ==========

    private static bool HasStructuredContent(IoTableRow row)
    {
        return !string.IsNullOrWhiteSpace(row.InputModule)
            || !string.IsNullOrWhiteSpace(row.InputAddress)
            || !string.IsNullOrWhiteSpace(row.InputStation)
            || !string.IsNullOrWhiteSpace(row.InputComment)
            || !string.IsNullOrWhiteSpace(row.InputRemark)
            || !string.IsNullOrWhiteSpace(row.OutputModule)
            || !string.IsNullOrWhiteSpace(row.OutputAddress)
            || !string.IsNullOrWhiteSpace(row.OutputStation)
            || !string.IsNullOrWhiteSpace(row.OutputComment)
            || !string.IsNullOrWhiteSpace(row.OutputRemark);
    }

    private static bool HasLegacyContent(IoTableRow row)
    {
        return !string.IsNullOrWhiteSpace(row.InputAddress)
            || !string.IsNullOrWhiteSpace(row.InputComment)
            || !string.IsNullOrWhiteSpace(row.OutputAddress)
            || !string.IsNullOrWhiteSpace(row.OutputComment);
    }

    private static bool LooksLikeIoAddress(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && IoAddressPattern.IsMatch(value.Trim().TrimStart('%'));
    }

    private static bool LooksLikeModuleValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (LooksLikeIoAddress(text))
        {
            return false;
        }

        return text.Contains("模块", StringComparison.OrdinalIgnoreCase)
            || text.Contains("PLC本体", StringComparison.OrdinalIgnoreCase)
            || text.Contains("输入", StringComparison.OrdinalIgnoreCase)
            || text.Contains("输出", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record IoSignal(string Module, string Address, string Station, string Comment, string Remark);
}
