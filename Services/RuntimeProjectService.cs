using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.Services;

/// <summary>
/// 负责加载 / 保存开放式 HMI 工程文件（project.json）。
/// 默认从 projects/_sample/project.json 加载；路径可在运行时覆盖。
/// </summary>
public class RuntimeProjectService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string DefaultProjectPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "projects", "_sample", "project.json");

    /// <summary>当前已加载的工程文档，未加载时为 null。</summary>
    public ProjectDocument? Current { get; private set; }

    /// <summary>加载默认示例工程。若文件不存在则尝试从 V1 格式自动迁移。</summary>
    public ProjectDocument LoadDefault()
    {
        if (File.Exists(DefaultProjectPath))
        {
            return Load(DefaultProjectPath);
        }

        // M-05: 检测 V1 designer-project.json 存在时自动迁移
        var v1Path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config", "designer-project.json");
        if (File.Exists(v1Path))
        {
            try
            {
                var v1Json = File.ReadAllText(v1Path, System.Text.Encoding.UTF8);
                var v1Project = JsonSerializer.Deserialize<DesignerProject>(v1Json, _jsonOptions);
                if (v1Project is not null)
                {
                    var migrated = V1ProjectMigrator.MigrateProject(v1Project);
                    migrated = ProjectMigration.Migrate(migrated);
                    ApexHMI.Services.RuntimeUi.BuiltInFaceplates.EnsureInjected(migrated.Faceplates ??= new Models.RuntimeUi.FaceplateLibrary());
                    Save(migrated, DefaultProjectPath);
                    Current = migrated;
                    ApexHMI.Services.RuntimeUi.DesignerContext.Document = migrated;
                    return migrated;
                }
            }
            catch
            {
                // 迁移失败则降级到 Demo
            }
        }

        var demo = CreateDemoProject();
        demo = ProjectMigration.Migrate(demo);
        // P7F: 注入内置 Faceplate
        ApexHMI.Services.RuntimeUi.BuiltInFaceplates.EnsureInjected(demo.Faceplates ??= new Models.RuntimeUi.FaceplateLibrary());
        Save(demo, DefaultProjectPath);
        Current = demo;
        ApexHMI.Services.RuntimeUi.DesignerContext.Document = demo;
        return demo;
    }

    public ProjectDocument Load(string path)
    {
        var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var doc = JsonSerializer.Deserialize<ProjectDocument>(json, _jsonOptions)
                  ?? new ProjectDocument();
        doc = ProjectMigration.Migrate(doc);
        // P7F: 注入内置 Faceplate（不在 Migration 内做，避免 Models → Services 反向依赖）
        ApexHMI.Services.RuntimeUi.BuiltInFaceplates.EnsureInjected(doc.Faceplates ??= new Models.RuntimeUi.FaceplateLibrary());
        Current = doc;
        ApexHMI.Services.RuntimeUi.DesignerContext.Document = doc;
        return doc;
    }

    public void Save(ProjectDocument doc, string? path = null)
    {
        var target = path ?? DefaultProjectPath;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var json = JsonSerializer.Serialize(doc, _jsonOptions);
        File.WriteAllText(target, json, System.Text.Encoding.UTF8);
    }

    /// <summary>内置演示工程：包含两个示例页面，不依赖真实 OPC UA 变量。</summary>
    private static ProjectDocument CreateDemoProject()
    {
        return new ProjectDocument
        {
            ProjectName = "演示工程",
            DefaultPageRouteKey = "overview",
            Pages =
            [
                new()
                {
                    Id = "page-overview",
                    Title = "运行总览",
                    RouteKey = "overview",
                    CanvasWidth = 1280,
                    CanvasHeight = 720,
                    Widgets =
                    [
                        new()
                        {
                            TypeId = "text",
                            X = 40, Y = 40, Width = 300, Height = 40,
                            Properties = { ["text"] = "设备状态总览", ["fontSize"] = "22", ["foreground"] = "#0F172A" }
                        },
                        new()
                        {
                            TypeId = "text",
                            X = 40, Y = 100, Width = 200, Height = 32,
                            Properties = { ["text"] = "示例文本", ["fontSize"] = "14", ["foreground"] = "#0F172A" }
                        },
                        new()
                        {
                            TypeId = "button",
                            X = 40, Y = 200, Width = 120, Height = 40,
                            Properties = { ["text"] = "前往第二页", ["background"] = "#374151", ["foreground"] = "#FFFFFF" },
                            ActionType = "navigate",
                            ActionParam = "page2"
                        }
                    ]
                },
                new()
                {
                    Id = "page-2",
                    Title = "页面 2",
                    RouteKey = "page2",
                    CanvasWidth = 1280,
                    CanvasHeight = 720,
                    Widgets =
                    [
                        new()
                        {
                            TypeId = "text",
                            X = 40, Y = 40, Width = 200, Height = 36,
                            Properties = { ["text"] = "页面 2", ["fontSize"] = "22", ["foreground"] = "#0F172A" }
                        },
                        new()
                        {
                            TypeId = "button",
                            X = 40, Y = 120, Width = 120, Height = 40,
                            Properties = { ["text"] = "返回总览", ["background"] = "#374151", ["foreground"] = "#FFFFFF" },
                            ActionType = "navigate",
                            ActionParam = "overview"
                        }
                    ]
                }
            ]
        };
    }
}
