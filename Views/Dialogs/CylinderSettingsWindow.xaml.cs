using System.Windows;
using System.Windows.Threading;
using ApexHMI.ViewModels;

namespace ApexHMI.Views.Dialogs;

public partial class CylinderSettingsWindow : Window
{
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public CylinderSettingsWindow()
    {
        InitializeComponent();
        Loaded += CylinderSettingsWindow_Loaded;
        Closed += CylinderSettingsWindow_Closed;
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
            await vm.RefreshSelectedCylinderParmValuesAsync();
        }
    }

    private void CylinderSettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Start();
    }

    private void CylinderSettingsWindow_Closed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
    }
}
