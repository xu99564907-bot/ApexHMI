#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>P6D: 工程图片资源管理 — 管理 <c>{AppBase}/projects/_sample/assets/images/</c>
/// 目录的图片清单。提供 List / Add / Remove / GetFullPath。
/// </summary>
public static class ImageResourceService
{
    private static readonly string[] Exts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg" };

    public static string ImagesDir => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "projects", "_sample", "assets", "images");

    public static IReadOnlyList<string> List()
    {
        try
        {
            if (!Directory.Exists(ImagesDir)) return Array.Empty<string>();
            return Directory.EnumerateFiles(ImagesDir)
                .Where(f => Exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    public static string GetFullPath(string fileName)
        => Path.Combine(ImagesDir, fileName);

    /// <summary>把外部文件复制到资源目录，返回新文件名（保留原名；冲突时加 _2、_3）。</summary>
    public static string Add(string sourcePath)
    {
        Directory.CreateDirectory(ImagesDir);
        var name = Path.GetFileName(sourcePath);
        var target = Path.Combine(ImagesDir, name);
        var i = 1;
        while (File.Exists(target))
        {
            i++;
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            target = Path.Combine(ImagesDir, $"{stem}_{i}{ext}");
        }
        File.Copy(sourcePath, target);
        return Path.GetFileName(target);
    }

    public static void Remove(string fileName)
    {
        var p = GetFullPath(fileName);
        if (File.Exists(p)) File.Delete(p);
    }
}
