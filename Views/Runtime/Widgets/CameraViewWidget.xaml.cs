#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class CameraViewWidget : UserControl
{
    public CameraViewWidget()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CameraViewWidgetViewModel vm) return;
        if (vm.IsDesignTimeView) return;
        try
        {
            vm.StartPlayback(Video);
        }
        catch
        {
            // LibVLC native 未加载 — 静默；TODO 用户安装/解锁 VLC native dll
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CameraViewWidgetViewModel vm)
        {
            try { vm.StopPlayback(); } catch { /* ignore */ }
        }
    }
}
