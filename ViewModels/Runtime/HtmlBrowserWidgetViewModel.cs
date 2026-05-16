#nullable enable
using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P9A: HTML 浏览器（WebView2 Edge）。
/// 设计时显示占位文本，运行时加载 Url。
/// </summary>
public partial class HtmlBrowserWidgetViewModel : WidgetViewModelBase
{
    public HtmlBrowserWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
    }

    public string Url
    {
        get => Prop("url", "https://www.bing.com");
        set
        {
            Model.Properties["url"] = value;
            Model.NotifyPropertiesChanged();
            OnPropertyChanged();
        }
    }

    public bool NavigateOnLoad => string.Equals(Prop("navigateOnLoad", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowToolbar    => string.Equals(Prop("showToolbar", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public string Background   => Prop("background", "#FFFFFF");

    public Visibility ToolbarVisibility => ShowToolbar ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>设计时（Shell 为 null 或 Designer*）：显示占位；运行时显示 WebView2。</summary>
    public bool IsDesignTimeView
    {
        get
        {
            var shell = _dataContext.Shell;
            if (shell is null) return true;
            return shell.GetType().Name.Contains("Designer", StringComparison.OrdinalIgnoreCase);
        }
    }

    public Visibility DesignPlaceholderVisibility => IsDesignTimeView ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RuntimeViewVisibility        => IsDesignTimeView ? Visibility.Collapsed : Visibility.Visible;
}
