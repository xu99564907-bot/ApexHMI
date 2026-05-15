using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class ButtonWidget : UserControl
{
    public ButtonWidget()
    {
        InitializeComponent();
        // M5.3: WinCC 标准 — Tab 拿到焦点的按钮，空格 = 单击
        Focusable = true;
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Space && e.Key != System.Windows.Input.Key.Enter) return;
        if (DataContext is ButtonWidgetViewModel vm && vm.ClickCommand.CanExecute(null))
        {
            vm.ClickCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ButtonWidgetViewModel vm) vm.PressDownCommand.Execute(null);
    }

    private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ButtonWidgetViewModel vm) vm.ReleaseCommand.Execute(null);
    }
}
