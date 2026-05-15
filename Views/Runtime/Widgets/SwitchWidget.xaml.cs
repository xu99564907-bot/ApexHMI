using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class SwitchWidget : UserControl
{
    public SwitchWidget()
    {
        InitializeComponent();
        // M5.3: 空格 = 单击
        Focusable = true;
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Space && e.Key != System.Windows.Input.Key.Enter) return;
        if (DataContext is SwitchWidgetViewModel vm && vm.ClickCommand.CanExecute(null))
        {
            vm.ClickCommand.Execute(null);
            e.Handled = true;
        }
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
