using System;
using System.Windows;
using System.Windows.Threading;
using ApexHMI.ViewModels;

namespace ApexHMI.Views.Dialogs;

public partial class AxisSettingsWindow : Window
{
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public AxisSettingsWindow()
    {
        InitializeComponent();
        Loaded += AxisSettingsWindow_Loaded;
        Closed += AxisSettingsWindow_Closed;
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.RefreshSelectedAxisBindingValuesAsync();
        }
    }

    private void AxisSettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Start();
    }

    private void AxisSettingsWindow_Closed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        Loaded -= AxisSettingsWindow_Loaded;
        Closed -= AxisSettingsWindow_Closed;
    }
}
