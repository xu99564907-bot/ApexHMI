using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class DateTimeWidget : UserControl
{
    public DateTimeWidget()
    {
        InitializeComponent();
    }

    private void EditDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is DateTimeWidgetViewModel vm) vm.CommitDateTime();
    }

    private void EditTime_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is DateTimeWidgetViewModel vm) vm.CommitDateTime();
    }

    private void EditTime_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DateTimeWidgetViewModel vm) vm.CommitDateTime();
    }
}
