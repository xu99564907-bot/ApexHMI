using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;

namespace ApexHMI.Services;

/// <summary>
/// 把 IO 程序生成得到的 artifact 按规则合并到 Git 拉取目录里对应的 .st 文件。
///
/// 映射规则（OPXX 使用工位号，例如 OP80；DBXX 使用对应的 DB 号，例如 DB80）：
///  - DBXX_IO            -> {GitRoot}/2.Main/Main_IO.ST
///  - DI_ACT_Comment     -> {GitRoot}/OPXX/2.PRG/OPXX_DI_Mirror/ACT_Comment.st
///  - DO_ACT_Comment     -> {GitRoot}/OPXX/2.PRG/OPXX_DO_Mirror/ACT_Comment.st
///  - ACT_Cylinder       -> {GitRoot}/OPXX/2.PRG/OPXX_DriveControl/ACT_Cylinder.st
///  - ACT_Vacuum         -> {GitRoot}/OPXX/2.PRG/OPXX_DriveControl/ACT_Vacuum.st
///  - ACT_Sensor         -> {GitRoot}/OPXX/2.PRG/OPXX_DriveControl/ACT_Sensor.st
///  - ACT_Motor          -> {GitRoot}/OPXX/2.PRG/OPXX_DriveControl/ACT_Motor.st
///  - ACT_Axis           -> {GitRoot}/OPXX/2.PRG/OPXX_DriveControl/ACT_Axis.st
///  - ACT_Rotdisk        -> {GitRoot}/OPXX/2.PRG/OPXX_DriveControl/ACT_Rotdisk.st
///  - ACT_Epson/ACT_Kuka -> {GitRoot}/OPXX/2.PRG/OPXX_DriveControl/ACT_Epson.st | ACT_Kuka.st
///
/// 如果目标文件存在：保留现有内容与编码，按变量地址或实例调用头替换重复块，新增块追加。
/// 如果目标文件不存在：按 UTF-8 无 BOM 创建并写入。
/// </summary>
public class GeneratedArtifactSyncService : IGeneratedArtifactSyncService
{
    private static readonly Regex DbIoPattern  = new(@"^DB\d+_IO$",                                    RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EnumDutPattern = new(@"^Enum_[A-Za-z0-9]+_(Cyl|Axis|Vac|Motor|Sensor)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InstanceStartPattern = new(@"(?m)^[ \t]*(?<key>FB_[A-Za-z0-9_]+_instance\[\d+\])\s*\(", RegexOptions.Compiled);
    private static readonly Regex VarDeclarationPattern = new(@"(?<line>^[ \t]*.*?\bAT\b[ \t]*%(?<address>[A-Za-z]+\d+(?:\.\d+)?)[ \t]*:[ \t]*BOOL[ \t]*;[ \t]*(?:\r?\n|$))", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public async Task<GeneratedArtifactSyncResult> AppendArtifactsAsync(
        IReadOnlyList<GeneratedProgramArtifact> artifacts,
        string gitRootFolder,
        string operationNumber)
    {
        if (artifacts is null) throw new ArgumentNullException(nameof(artifacts));

        var result = new GeneratedArtifactSyncResult
        {
            GitRootFolder = gitRootFolder ?? string.Empty,
            OperationNumber = operationNumber ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(gitRootFolder))
        {
            throw new InvalidOperationException("未配置 Git 保存目录，无法同步生成内容。请先在 Git 代码拉取中选择目录。");
        }

        if (!Directory.Exists(gitRootFolder))
        {
            throw new DirectoryNotFoundException($"Git 保存目录不存在：{gitRootFolder}");
        }

        var normalizedOp = NormalizeOperationNumber(operationNumber);
        foreach (var artifact in artifacts)
        {
            if (artifact is null) continue;
            var relative = MapArtifactToRelativePath(artifact.DisplayName, normalizedOp);
            if (relative is null)
            {
                result.Skipped.Add(new GeneratedArtifactSyncSkipped(artifact.DisplayName, "未匹配到映射规则"));
                continue;
            }

            var targetPath = Path.GetFullPath(Path.Combine(gitRootFolder, relative));

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            var encoding = await ResolveEncodingAsync(targetPath);
            var existingContent = File.Exists(targetPath)
                ? await Compat.ReadAllTextAsync(targetPath, encoding)
                : string.Empty;
            var mergedContent = MergeArtifactContent(existingContent, artifact);

            await Compat.WriteAllTextAsync(targetPath, mergedContent, encoding);

            result.Appended.Add(new GeneratedArtifactSyncAppended(
                artifact.DisplayName,
                targetPath,
                encoding.WebName,
                (artifact.Content ?? string.Empty).Length));
        }

        return result;
    }

    private static string? MapArtifactToRelativePath(string displayName, string normalizedOp)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var name = displayName.Trim();

        if (DbIoPattern.IsMatch(name))
        {
            return Path.Combine("2.Main", "Main_IO.ST");
        }

        var prgDir = Path.Combine(normalizedOp, "2.PRG");
        var driveDir = Path.Combine(prgDir, $"{normalizedOp}_DriveControl");

        // 枚举 DUT：Enum_OPXX_Cyl/Axis/Vac/Motor/Sensor → OPXX/0.Struct/Enum_OPXX_XXX.st
        if (EnumDutPattern.IsMatch(name))
        {
            return Path.Combine(normalizedOp, "0.Struct", $"{name}.st");
        }

        return name.ToUpperInvariant() switch
        {
            "DI_ACT_COMMENT" => Path.Combine(prgDir, $"{normalizedOp}_DI_Mirror", "ACT_Comment.st"),
            "DO_ACT_COMMENT" => Path.Combine(prgDir, $"{normalizedOp}_DO_Mirror", "ACT_Comment.st"),
            "ACT_CYLINDER" => Path.Combine(driveDir, "ACT_Cylinder.st"),
            "ACT_VACUUM" => Path.Combine(driveDir, "ACT_Vacuum.st"),
            "ACT_SENSOR" => Path.Combine(driveDir, "ACT_Sensor.st"),
            "ACT_MOTOR" => Path.Combine(driveDir, "ACT_Motor.st"),
            "ACT_AXIS" => Path.Combine(driveDir, "ACT_Axis.st"),
            "ACT_ROTDISK" => Path.Combine(driveDir, "ACT_Rotdisk.st"),
            "ACT_EPSON" => Path.Combine(driveDir, "ACT_Epson.st"),
            "ACT_KUKA" => Path.Combine(driveDir, "ACT_Kuka.st"),
            _ => null
        };
    }

    private static string NormalizeOperationNumber(string? operationNumber)
    {
        var trimmed = (operationNumber ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "OP00";
        }

        if (!trimmed.StartsWith("OP", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "OP" + trimmed;
        }

        // 统一转成大写，如 op80 -> OP80。
        return trimmed.ToUpperInvariant();
    }

    private static string MergeArtifactContent(string existingContent, GeneratedProgramArtifact artifact)
    {
        var displayName = artifact.DisplayName ?? string.Empty;
        var generatedContent = NormalizeTrailingNewline(artifact.Content ?? string.Empty, GetPreferredNewLine(existingContent));

        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return generatedContent;
        }

        // 枚举 DUT 每次完整重新生成，直接替换旧内容
        if (EnumDutPattern.IsMatch(displayName))
        {
            return generatedContent;
        }

        return DbIoPattern.IsMatch(displayName)
            ? MergeVariableDeclarations(existingContent, generatedContent)
            : MergeInstanceBlocks(existingContent, generatedContent);
    }

    private static string MergeVariableDeclarations(string existingContent, string generatedContent)
    {
        var newLine = GetPreferredNewLine(existingContent);
        var generatedDeclarations = ExtractVariableDeclarations(generatedContent);
        if (generatedDeclarations.Count == 0)
        {
            return EnsureSeparatedAppend(existingContent, generatedContent, newLine);
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var replaced = VarDeclarationPattern.Replace(existingContent, match =>
        {
            var key = NormalizeAddressKey(match.Groups["address"].Value);
            if (!generatedDeclarations.TryGetValue(key, out var declaration))
            {
                return match.Value;
            }

            used.Add(key);
            return EnsureLineEnding(declaration, newLine);
        });

        var remaining = generatedDeclarations
            .Where(pair => !used.Contains(pair.Key))
            .Select(pair => EnsureLineEnding(pair.Value, newLine))
            .ToList();

        if (remaining.Count == 0)
        {
            return replaced;
        }

        var endVarMatch = Regex.Match(replaced, @"(?im)^[ \t]*END_VAR\b.*(?:\r?\n|$)");
        if (!endVarMatch.Success)
        {
            return EnsureSeparatedAppend(replaced, BuildVarGlobalBlock(remaining, newLine), newLine);
        }

        var insertion = string.Concat(remaining);
        return replaced.Insert(endVarMatch.Index, insertion);
    }

    private static Dictionary<string, string> ExtractVariableDeclarations(string content)
    {
        var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in VarDeclarationPattern.Matches(content))
        {
            var key = NormalizeAddressKey(match.Groups["address"].Value);
            declarations[key] = TrimTrailingNewLine(match.Groups["line"].Value);
        }

        return declarations;
    }

    private static string MergeInstanceBlocks(string existingContent, string generatedContent)
    {
        var newLine = GetPreferredNewLine(existingContent);
        var generatedBlockList = ExtractInstanceBlocks(generatedContent);
        var generatedBlocks = generatedBlockList
            .GroupBy(block => block.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        if (generatedBlocks.Count == 0)
        {
            return EnsureSeparatedAppend(existingContent, generatedContent, newLine);
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingBlocks = ExtractInstanceBlocks(existingContent);
        var builder = new StringBuilder(existingContent.Length + generatedContent.Length);
        var cursor = 0;

        foreach (var block in existingBlocks)
        {
            builder.Append(existingContent, cursor, block.Start - cursor);
            if (generatedBlocks.TryGetValue(block.Key, out var replacement))
            {
                if (!used.Contains(block.Key))
                {
                    builder.Append(NormalizeTrailingNewline(replacement.Text, newLine));
                    used.Add(block.Key);
                }
            }
            else
            {
                builder.Append(existingContent, block.Start, block.End - block.Start);
            }

            cursor = block.End;
        }

        builder.Append(existingContent, cursor, existingContent.Length - cursor);

        var remaining = generatedBlockList
            .Where(block => !used.Contains(block.Key))
            .Select(block => NormalizeTrailingNewline(block.Text, newLine))
            .ToList();

        foreach (var block in remaining)
        {
            var current = builder.ToString();
            if (!string.IsNullOrWhiteSpace(current))
            {
                builder.Append(EndsWithNewLine(current) ? newLine : newLine + newLine);
            }

            builder.Append(block);
        }

        return builder.ToString();
    }

    private static List<InstanceBlock> ExtractInstanceBlocks(string content)
    {
        var blocks = new List<InstanceBlock>();
        foreach (Match match in InstanceStartPattern.Matches(content))
        {
            var end = FindInstanceBlockEnd(content, match.Index);
            if (end <= match.Index)
            {
                continue;
            }

            var key = match.Groups["key"].Value.Trim();
            blocks.Add(new InstanceBlock(key, match.Index, end, content.Substring(match.Index, end - match.Index)));
        }

        return blocks;
    }

    private static int FindInstanceBlockEnd(string content, int start)
    {
        var terminator = content.IndexOf(");", start, StringComparison.Ordinal);
        if (terminator < 0)
        {
            return -1;
        }

        var end = terminator + 2;
        while (end < content.Length && (content[end] == '\r' || content[end] == '\n'))
        {
            end++;
        }

        for (var i = 0; i < 3; i++)
        {
            var lineStart = end;
            while (lineStart < content.Length && (content[lineStart] == '\r' || content[lineStart] == '\n'))
            {
                lineStart++;
            }

            var lineEnd = lineStart;
            while (lineEnd < content.Length && content[lineEnd] != '\r' && content[lineEnd] != '\n')
            {
                lineEnd++;
            }

            var line = content.Substring(lineStart, lineEnd - lineStart).Trim();
            if (line != "///////")
            {
                break;
            }

            end = lineEnd;
            while (end < content.Length && (content[end] == '\r' || content[end] == '\n'))
            {
                end++;
            }
        }

        return end;
    }

    private static string BuildVarGlobalBlock(IEnumerable<string> declarations, string newLine)
    {
        return "VAR_GLOBAL" + newLine
            + string.Concat(declarations)
            + "END_VAR" + newLine;
    }

    private static string EnsureSeparatedAppend(string existingContent, string appendContent, string newLine)
    {
        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return NormalizeTrailingNewline(appendContent, newLine);
        }

        var separator = EndsWithNewLine(existingContent) ? newLine : newLine + newLine;
        return existingContent + separator + NormalizeTrailingNewline(appendContent, newLine);
    }

    private static string NormalizeAddressKey(string address)
    {
        return (address ?? string.Empty).Trim().TrimStart('%').ToUpperInvariant();
    }

    private static string EnsureLineEnding(string text, string newLine)
    {
        return TrimTrailingNewLine(text) + newLine;
    }

    private static string NormalizeTrailingNewline(string text, string newLine)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return EndsWithNewLine(text) ? text : text + newLine;
    }

    private static string TrimTrailingNewLine(string text)
    {
        return (text ?? string.Empty).TrimEnd('\r', '\n');
    }

    private static bool EndsWithNewLine(string text)
    {
        return !string.IsNullOrEmpty(text) && (text.EndsWith("\n", StringComparison.Ordinal) || text.EndsWith("\r", StringComparison.Ordinal));
    }

    private static string GetPreferredNewLine(string text)
    {
        return (text ?? string.Empty).Contains("\r\n") ? "\r\n" : Environment.NewLine;
    }

    /// <summary>
    /// 读取目标文件前几个字节判断编码，并检查末尾是否以换行结束。
    /// 不存在则返回 UTF-8 无 BOM。
    /// </summary>
    private static async Task<Encoding> ResolveEncodingAsync(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return Utf8NoBom;
        }

        using var stream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        Encoding encoding = Utf8NoBom;
        if (stream.Length >= 3)
        {
            var header = new byte[3];
            var read = await stream.ReadAsync(header, 0, 3);
            if (read >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
            {
                encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            }
            else if (read >= 2 && header[0] == 0xFF && header[1] == 0xFE)
            {
                encoding = Encoding.Unicode; // UTF-16 LE
            }
            else if (read >= 2 && header[0] == 0xFE && header[1] == 0xFF)
            {
                encoding = Encoding.BigEndianUnicode;
            }
        }

        return encoding;
    }

    private sealed record InstanceBlock(string Key, int Start, int End, string Text);
}

public class GeneratedArtifactSyncResult
{
    public string GitRootFolder { get; set; } = string.Empty;
    public string OperationNumber { get; set; } = string.Empty;
    public List<GeneratedArtifactSyncAppended> Appended { get; } = new();
    public List<GeneratedArtifactSyncSkipped> Skipped { get; } = new();
}

public record GeneratedArtifactSyncAppended(string DisplayName, string TargetPath, string EncodingName, int AppendedLength);

public record GeneratedArtifactSyncSkipped(string DisplayName, string Reason);
