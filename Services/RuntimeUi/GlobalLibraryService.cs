#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>P6C: 全局库 — 跨工程的资产仓库，存放在
/// <c>%USERPROFILE%\.apexhmi\global-library.json</c>。
/// 启动时懒加载，AddAsset 立刻持久化。
/// </summary>
public sealed class GlobalLibraryService
{
    private static readonly Lazy<GlobalLibraryService> _instance = new(() => new GlobalLibraryService());
    public static GlobalLibraryService Instance => _instance.Value;

    private static readonly JsonSerializerOptions _opt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".apexhmi", "global-library.json");

    public ProjectLibrary Library { get; private set; } = new();

    private GlobalLibraryService()
    {
        try { Load(); }
        catch (Exception ex) { Log.Warning(ex, "GlobalLibraryService: 加载失败，使用空库"); }
    }

    private void Load()
    {
        if (!File.Exists(Path)) return;
        var json = File.ReadAllText(Path, System.Text.Encoding.UTF8);
        var lib = JsonSerializer.Deserialize<ProjectLibrary>(json, _opt);
        if (lib is not null) Library = lib;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var json = JsonSerializer.Serialize(Library, _opt);
            File.WriteAllText(Path, json, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) { Log.Warning(ex, "GlobalLibraryService: 保存失败"); }
    }

    public void AddAsset(LibraryAsset asset)
    {
        Library.Assets.Add(asset);
        Save();
    }

    public void RemoveAsset(LibraryAsset asset)
    {
        Library.Assets.Remove(asset);
        Save();
    }
}
