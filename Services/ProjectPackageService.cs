#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services;

/// <summary>
/// P10D: 工程导出/导入服务（zip 打包）。
/// <para>导出内容：project.json + assets/ 目录（用户上传图片等）；如果项目目录里有 globals.json
/// 或 plc-tags.json 也一并打包。</para>
/// <para>导入：解压到目标目录，读出 project.json 并返回 ProjectDocument。</para>
/// </summary>
public sealed class ProjectPackageService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>把 <paramref name="projectPath"/> 所在目录打包到 zip。</summary>
    public void Export(string projectPath, string zipPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("projectPath 不能为空", nameof(projectPath));
        if (!File.Exists(projectPath))
            throw new FileNotFoundException("project.json 不存在", projectPath);

        var srcDir = Path.GetDirectoryName(projectPath)!;
        if (File.Exists(zipPath)) File.Delete(zipPath);

        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create, Encoding.UTF8);

        // 主 project.json
        zip.CreateEntryFromFile(projectPath, "project.json", CompressionLevel.Optimal);

        // assets/
        var assetsDir = Path.Combine(srcDir, "assets");
        if (Directory.Exists(assetsDir))
        {
            foreach (var f in Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories))
            {
                var rel = "assets/" + GetRelativePath(assetsDir, f).Replace('\\', '/');
                zip.CreateEntryFromFile(f, rel, CompressionLevel.Optimal);
            }
        }

        // 附加可选文件
        foreach (var fn in new[] { "globals.json", "plc-tags.json", "library.json" })
        {
            var p = Path.Combine(srcDir, fn);
            if (File.Exists(p))
                zip.CreateEntryFromFile(p, fn, CompressionLevel.Optimal);
        }

        Log.Information("ProjectPackage: 已导出 {Zip}（源 {Src}）", zipPath, srcDir);
    }

    /// <summary>把 zip 解压到 <paramref name="targetDir"/>，并返回其中的 project.json 反序列化结果。</summary>
    public ProjectDocument Import(string zipPath, string targetDir)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("zip 不存在", zipPath);

        Directory.CreateDirectory(targetDir);
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in zip.Entries)
            {
                var dst = Path.Combine(targetDir, entry.FullName);
                var dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir)) Directory.CreateDirectory(dstDir);
                if (string.IsNullOrEmpty(entry.Name)) continue; // 目录条目
                entry.ExtractToFile(dst, overwrite: true);
            }
        }

        var projectJson = Path.Combine(targetDir, "project.json");
        if (!File.Exists(projectJson))
            throw new InvalidDataException("zip 内未找到 project.json");

        var json = File.ReadAllText(projectJson, Encoding.UTF8);
        var doc = JsonSerializer.Deserialize<ProjectDocument>(json, _jsonOptions)
                  ?? throw new InvalidDataException("project.json 解析失败");

        Log.Information("ProjectPackage: 已从 {Zip} 导入到 {Target}", zipPath, targetDir);
        return doc;
    }

    /// <summary>net48 没有 Path.GetRelativePath，自实现简化版。</summary>
    private static string GetRelativePath(string baseDir, string fullPath)
    {
        var baseUri = new Uri(baseDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? baseDir : baseDir + Path.DirectorySeparatorChar);
        var fileUri = new Uri(fullPath);
        var rel = baseUri.MakeRelativeUri(fileUri).ToString();
        return Uri.UnescapeDataString(rel).Replace('/', Path.DirectorySeparatorChar);
    }
}
