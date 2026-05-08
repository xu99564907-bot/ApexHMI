using System;
using System.Linq;
using System.Windows;
using Serilog;

namespace ApexHMI.Services;

public enum AppTheme
{
    Light,
    Dark,
    HighContrast
}

/// <summary>
/// Phase 3 主题切换服务：在运行时替换 App.Resources.MergedDictionaries 中的 Theme.xaml。
/// 各页面的 {DynamicResource Brush.*} 会自动重新解析。
/// </summary>
public sealed class ThemeService
{
    private const string ThemeKey = "ApexHMI.Theme";

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void Apply(AppTheme theme)
    {
        try
        {
            var app = Application.Current;
            if (app is null) return;

            var uri = theme switch
            {
                AppTheme.Dark => "/Themes/Theme.Dark.xaml",
                AppTheme.HighContrast => "/Themes/Theme.HighContrast.xaml",
                _ => "/Themes/Theme.xaml"
            };

            var newDict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };

            // 找到旧 Theme dictionary（按 Source 后缀识别），原地替换
            var dicts = app.Resources.MergedDictionaries;
            var oldIndex = -1;
            for (var i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.OriginalString ?? string.Empty;
                if (src.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase)
                    || src.EndsWith("Theme.Dark.xaml", StringComparison.OrdinalIgnoreCase)
                    || src.EndsWith("Theme.HighContrast.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    oldIndex = i;
                    break;
                }
            }

            if (oldIndex >= 0)
            {
                dicts[oldIndex] = newDict;
            }
            else
            {
                dicts.Insert(0, newDict);
            }

            CurrentTheme = theme;
            Log.Information("ThemeService: 已切换到 {Theme}", theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ThemeService.Apply 失败 theme={Theme}", theme);
        }
    }
}
