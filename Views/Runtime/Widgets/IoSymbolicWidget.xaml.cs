using System.Windows.Controls;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class IoSymbolicWidget : UserControl
{
    public IoSymbolicWidget()
    {
        InitializeComponent();
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is IoSymbolicWidgetViewModel vm && sender is ComboBox cb && cb.SelectedItem is SymbolicEntry entry)
        {
            vm.SelectEntryCommand.Execute(entry);
        }
    }
}
