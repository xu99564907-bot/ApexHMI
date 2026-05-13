using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class ButtonWidget : UserControl
{
    public ButtonWidget()
    {
        InitializeComponent();
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
