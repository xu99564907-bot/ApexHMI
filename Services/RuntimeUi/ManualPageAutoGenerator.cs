using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// 路径 B：IO 导入后自动为 ProjectDocument 生成手动操作页面（manual.cylinders / .axes / .robots / .stoppers）。
///
/// 行为：
/// - 已存在且 IsUserEdited=true 的页面不覆盖（保留用户布局/编辑），
///   仅追加新增设备的 widget。
/// - 不存在或 IsUserEdited=false 时全量重建该页。
/// - 自动按网格布局排列 widget。
/// </summary>
public sealed class ManualPageAutoGenerator
{
    private readonly IProjectEditorService _projectEditor;
    private readonly IWidgetEditorService _widgetEditor;

    public ManualPageAutoGenerator(IProjectEditorService projectEditor, IWidgetEditorService widgetEditor)
    {
        _projectEditor = projectEditor;
        _widgetEditor = widgetEditor;
    }

    public const string CylindersRouteKey = "manual.cylinders";
    public const string AxesRouteKey      = "manual.axes";
    public const string RobotsRouteKey    = "manual.robots";
    public const string StoppersRouteKey  = "manual.stoppers";

    /// <summary>
    /// 根据 Shell 当前的设备集合刷新所有 manual.* 页面。
    /// </summary>
    public void GenerateAll(
        ProjectDocument document,
        IEnumerable<ManualCylinderBlockItem> cylinders,
        IEnumerable<ManualAxisBlockItem> axes,
        bool hasRobot,
        IEnumerable<TagItem> stopperTags)
    {
        var cylList = cylinders?.ToList() ?? new List<ManualCylinderBlockItem>();
        if (cylList.Count > 0)
        {
            EnsurePage(document, CylindersRouteKey, "气缸",
                cylList.Select(c => string.IsNullOrWhiteSpace(c.DisplayName) ? $"Cyl{c.CylinderIndex}" : c.DisplayName).ToList(),
                "manual-cylinder-block", 240, 250);
        }

        var axisList = axes?.ToList() ?? new List<ManualAxisBlockItem>();
        if (axisList.Count > 0)
        {
            EnsurePage(document, AxesRouteKey, "轴",
                axisList.Select(a => string.IsNullOrWhiteSpace(a.DisplayName) ? $"Axis{a.AxisIndex}" : a.DisplayName).ToList(),
                "manual-axis-block", 260, 230);
        }

        if (hasRobot)
        {
            EnsurePage(document, RobotsRouteKey, "机械手",
                new List<string> { "Robot" },
                "manual-robot-block", 360, 340);
        }

        var stopperNames = stopperTags?
            .Where(t => t.Name?.IndexOf("stopper", System.StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(t => t.Name)
            .Distinct()
            .ToList() ?? new List<string>();
        if (stopperNames.Count > 0)
        {
            EnsurePage(document, StoppersRouteKey, "挡停",
                stopperNames, "manual-stopper-block", 180, 130);
        }

        Log.Information("ManualPageAutoGenerator: 自动生成完成 cyl={Cyl} axis={Axis} robot={Robot} stopper={Stop}",
            cylList.Count, axisList.Count, hasRobot ? 1 : 0, stopperNames.Count);
    }

    /// <summary>查找或创建路径键对应的页面，按设备名同步 widget。</summary>
    private void EnsurePage(
        ProjectDocument document,
        string routeKey,
        string title,
        IReadOnlyList<string> deviceNames,
        string widgetTypeId,
        double widgetWidth,
        double widgetHeight)
    {
        var page = document.Pages.FirstOrDefault(p =>
            string.Equals(p.RouteKey, routeKey, System.StringComparison.OrdinalIgnoreCase));

        if (page is null)
        {
            page = _projectEditor.AddPage(document, title);
            page.RouteKey = routeKey;
            page.CanvasWidth = 1280;
            page.CanvasHeight = 720;
        }

        if (page.IsUserEdited)
        {
            // 用户已编辑过：仅追加新设备
            AppendNewDevices(page, deviceNames, widgetTypeId, widgetWidth, widgetHeight);
        }
        else
        {
            // 全量重建：清空所有 widget 后按网格生成
            page.Widgets.Clear();
            FillGrid(page, deviceNames, widgetTypeId, widgetWidth, widgetHeight);
        }
    }

    private void FillGrid(
        PageDefinition page,
        IReadOnlyList<string> deviceNames,
        string widgetTypeId,
        double widgetWidth,
        double widgetHeight)
    {
        const double gap = 12;
        const double startX = 20;
        const double startY = 20;

        var maxCols = System.Math.Max(1, (int)((page.CanvasWidth - startX) / (widgetWidth + gap)));
        for (int i = 0; i < deviceNames.Count; i++)
        {
            var col = i % maxCols;
            var row = i / maxCols;
            var x = startX + col * (widgetWidth + gap);
            var y = startY + row * (widgetHeight + gap);
            CreateDeviceWidget(page, widgetTypeId, deviceNames[i], x, y, widgetWidth, widgetHeight);
        }
    }

    private void AppendNewDevices(
        PageDefinition page,
        IReadOnlyList<string> currentDeviceNames,
        string widgetTypeId,
        double widgetWidth,
        double widgetHeight)
    {
        // 已有的设备名集合
        var existing = new HashSet<string>(
            page.Widgets
                .Where(w => string.Equals(w.TypeId, widgetTypeId, System.StringComparison.OrdinalIgnoreCase))
                .Select(w => w.Properties.TryGetValue("deviceName", out var n) ? n : string.Empty)
                .Where(n => !string.IsNullOrEmpty(n)),
            System.StringComparer.OrdinalIgnoreCase);

        var newOnes = currentDeviceNames.Where(n => !existing.Contains(n)).ToList();
        if (newOnes.Count == 0) return;

        // 在页面末尾追加：放到当前最低 widget 之下
        const double gap = 12;
        const double startX = 20;
        var lowestY = page.Widgets.Count == 0 ? 20 : page.Widgets.Max(w => w.Y + w.Height) + gap;
        FillGridFrom(page, newOnes, widgetTypeId, widgetWidth, widgetHeight, startX, lowestY, gap);

        Log.Information("ManualPageAutoGenerator: 已编辑页面 {Route} 追加 {N} 个新设备",
            page.RouteKey, newOnes.Count);
    }

    private void FillGridFrom(
        PageDefinition page,
        IReadOnlyList<string> deviceNames,
        string widgetTypeId,
        double widgetWidth,
        double widgetHeight,
        double startX,
        double startY,
        double gap)
    {
        var maxCols = System.Math.Max(1, (int)((page.CanvasWidth - startX) / (widgetWidth + gap)));
        for (int i = 0; i < deviceNames.Count; i++)
        {
            var col = i % maxCols;
            var row = i / maxCols;
            var x = startX + col * (widgetWidth + gap);
            var y = startY + row * (widgetHeight + gap);
            CreateDeviceWidget(page, widgetTypeId, deviceNames[i], x, y, widgetWidth, widgetHeight);
        }
    }

    private void CreateDeviceWidget(
        PageDefinition page,
        string widgetTypeId,
        string deviceName,
        double x, double y,
        double width, double height)
    {
        var w = _widgetEditor.AddWidget(page, widgetTypeId, x, y);
        _widgetEditor.ResizeWidget(w, width, height);
        _widgetEditor.UpdateProperty(w, "deviceName", deviceName);

        // stopper 的 displayName 默认与 deviceName 一致
        if (string.Equals(widgetTypeId, "manual-stopper-block", System.StringComparison.OrdinalIgnoreCase))
        {
            _widgetEditor.UpdateProperty(w, "displayName", deviceName);
        }
    }
}
