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
        // B2B: acceptOnExit 由 VM 内部判断
        if (DataContext is IoNumericWidgetViewModel vm)
        {
            vm.OnLostFocus();
        }
    }

    private void EditBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        // B2B: clearOnFocus 由 VM 内部判断
        if (DataContext is IoNumericWidgetViewModel vm)
        {
            vm.OnFocus();
        }

        // B2B: editOnFocus + acceptOnFull 触发 OnTextChanged 链路放在 TextChanged 事件
    }

    private void EditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is IoNumericWidgetViewModel vm)
        {
            vm.OnTextChanged();
        }
    }
}
