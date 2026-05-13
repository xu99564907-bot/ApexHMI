#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class PdfViewWidget : UserControl
{
    public PdfViewWidget()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PdfViewWidgetViewModel vm) return;
        if (vm.IsDesignTimeView) return;
        try
        {
            await Web.EnsureCoreWebView2Async();
            if (vm.FileUri is { } uri)
            {
                Web.Source = uri;
            }
        }
        catch
        {
            // WebView2 Runtime 未安装 — 静默忽略；TODO 用户安装 WebView2 Runtime
        }
    }
}
