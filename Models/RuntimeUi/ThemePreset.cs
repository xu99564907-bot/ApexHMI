using System.Collections.Generic;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// 设计器主题预设，定义一组配色方案。
/// </summary>
public class ThemePreset
{
    public string Name { get; init; } = "默认";

    /// <summary>画布背景色。</summary>
    public string CanvasBackground { get; init; } = "#0F172A";

    /// <summary>默认控件前景色。</summary>
    public string DefaultForeground { get; init; } = "#FFFFFF";

    /// <summary>强调色。</summary>
    public string AccentColor { get; init; } = "#2563EB";

    /// <summary>默认控件背景色。</summary>
    public string DefaultBackground { get; init; } = "#374151";

    /// <summary>成功状态色。</summary>
    public string SuccessColor { get; init; } = "#22C55E";

    /// <summary>警告状态色。</summary>
    public string WarningColor { get; init; } = "#F59E0B";

    /// <summary>错误状态色。</summary>
    public string ErrorColor { get; init; } = "#EF4444";

    /// <summary>将主题颜色映射为控件属性的更新字典。</summary>
    public Dictionary<string, string> ToPropertyUpdates()
    {
        return new()
        {
            ["foreground"] = DefaultForeground,
            ["background"] = DefaultBackground,
        };
    }

    // -- 预置主题 --

    public static IReadOnlyList<ThemePreset> Presets { get; } = new[]
    {
        new ThemePreset
        {
            Name = "深色工业",
            CanvasBackground = "#0F172A",
            DefaultForeground = "#F8FAFC",
            DefaultBackground = "#1E293B",
            AccentColor = "#3B82F6",
            SuccessColor = "#22C55E",
            WarningColor = "#F59E0B",
            ErrorColor = "#EF4444"
        },
        new ThemePreset
        {
            Name = "浅色现代",
            CanvasBackground = "#F1F5F9",
            DefaultForeground = "#0F172A",
            DefaultBackground = "#FFFFFF",
            AccentColor = "#2563EB",
            SuccessColor = "#16A34A",
            WarningColor = "#D97706",
            ErrorColor = "#DC2626"
        },
        new ThemePreset
        {
            Name = "蓝白经典",
            CanvasBackground = "#EFF6FF",
            DefaultForeground = "#1E3A8A",
            DefaultBackground = "#DBEAFE",
            AccentColor = "#1D4ED8",
            SuccessColor = "#15803D",
            WarningColor = "#B45309",
            ErrorColor = "#B91C1C"
        }
    };
}
