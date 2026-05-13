using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class SwitchWidget : UserControl
{
    public SwitchWidget()
    {
        InitializeComponent();
    }

    private void Switch_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SwitchWidgetViewModel vm) vm.PressDownCommand.Execute(null);
    }

    private void Switch_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SwitchWidgetViewModel vm) vm.ReleaseCommand.Execute(null);
    }
}
