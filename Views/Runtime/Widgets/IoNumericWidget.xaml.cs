using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class IoNumericWidget : UserControl
{
    public IoNumericWidget()
    {
        InitializeComponent();
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is IoNumericWidgetViewModel vm)
        {
            vm.CommitCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void EditBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is IoNumericWidgetViewModel vm)
        {
            vm.CommitCommand.Execute(null);
        }
    }
}
