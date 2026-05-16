#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class HtmlBrowserWidget : UserControl
{
    public HtmlBrowserWidget()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HtmlBrowserWidgetViewModel vm) return;
        if (vm.IsDesignTimeView) return;
        try
        {
            await Web.EnsureCoreWebView2Async();
            if (vm.NavigateOnLoad && Uri.TryCreate(vm.Url, UriKind.Absolute, out var uri))
            {
                Web.Source = uri;
            }
        }
        catch
        {
            // WebView2 Runtime 未安装 — 静默忽略；TODO 用户安装 WebView2 Runtime
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2?.CanGoBack == true) Web.CoreWebView2.GoBack();
    }

    private void OnForward(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2?.CanGoForward == true) Web.CoreWebView2.GoForward();
    }

    private void OnReload(object sender, RoutedEventArgs e)
    {
        Web.CoreWebView2?.Reload();
    }

    private void OnNavigate(object sender, RoutedEventArgs e) => NavigateToAddress();

    private void OnAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateToAddress();
            e.Handled = true;
        }
    }

    private void NavigateToAddress()
    {
        if (DataContext is not HtmlBrowserWidgetViewModel vm) return;
        var url = AddressBox.Text;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            try { Web.Source = uri; } catch { /* ignore */ }
        }
    }
}
