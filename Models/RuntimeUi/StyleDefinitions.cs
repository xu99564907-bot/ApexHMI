#nullable enable
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>P6A: 全局样式系统中的一项色板条目（key → 十六进制颜色值）。</summary>
public partial class ColorPalette : ObservableObject
{
    [ObservableProperty] private string _key = "primary";
    [ObservableProperty] private string _name = "主色";
    [ObservableProperty] private string _value = "#2563EB";
}

/// <summary>P6A: 字体预设（family/size/weight 三元组）。</summary>
public partial class FontPreset : ObservableObject
{
    [ObservableProperty] private string _key = "default";
    [ObservableProperty] private string _name = "默认";
    [ObservableProperty] private string _family = "Microsoft YaHei UI";
    [ObservableProperty] private double _size = 13;
    /// <summary>WPF FontWeight 名称：Normal / Bold / SemiBold / Light 等。</summary>
    [ObservableProperty] private string _weight = "Normal";
}

/// <summary>
/// P6A: 工程级全局样式定义。控件属性值可以写 <c>{style:colors/primary}</c> /
/// <c>{style:fonts/title}</c> 引用此处定义的色板和字体，运行时由
/// <see cref="ApexHMI.Services.RuntimeUi.StyleResolver"/> 解析。
/// </summary>
public partial class StyleDefinitions : ObservableObject
{
    public ObservableCollection<ColorPalette> Colors { get; set; } = new();
    public ObservableCollection<FontPreset> Fonts { get; set; } = new();

    /// <summary>注入一份内置默认色板 + 字体集合（仅当当前为空时）。</summary>
    public void EnsureDefaults()
    {
        if (Colors.Count == 0)
        {
            Colors.Add(new ColorPalette { Key = "primary",    Name = "主色（蓝）",  Value = "#2563EB" });
            Colors.Add(new ColorPalette { Key = "accent",     Name = "强调（绿）",  Value = "#0F766E" });
            Colors.Add(new ColorPalette { Key = "warn",       Name = "警告（橙）",  Value = "#F59E0B" });
            Colors.Add(new ColorPalette { Key = "error",      Name = "错误（红）",  Value = "#B91C1C" });
            Colors.Add(new ColorPalette { Key = "text",       Name = "文本主色",    Value = "#0F172A" });
            Colors.Add(new ColorPalette { Key = "textMuted",  Name = "文本次色",    Value = "#64748B" });
            Colors.Add(new ColorPalette { Key = "border",     Name = "边框",        Value = "#CBD5E1" });
            Colors.Add(new ColorPalette { Key = "surface",    Name = "卡片底",      Value = "#FFFFFF" });
            Colors.Add(new ColorPalette { Key = "surfaceAlt", Name = "分隔背景",    Value = "#F8FAFC" });
        }

        if (Fonts.Count == 0)
        {
            Fonts.Add(new FontPreset { Key = "default", Name = "默认正文", Family = "Microsoft YaHei UI", Size = 13, Weight = "Normal" });
            Fonts.Add(new FontPreset { Key = "title",   Name = "大标题",   Family = "Microsoft YaHei UI", Size = 18, Weight = "Bold" });
            Fonts.Add(new FontPreset { Key = "label",   Name = "标签",     Family = "Microsoft YaHei UI", Size = 11, Weight = "SemiBold" });
            Fonts.Add(new FontPreset { Key = "small",   Name = "小字注释", Family = "Microsoft YaHei UI", Size = 10, Weight = "Normal" });
        }
    }
}
