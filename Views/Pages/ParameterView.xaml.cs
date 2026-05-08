using System.Windows;
using System.Windows.Controls;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Pages;

public partial class ParameterView : UserControl
{
    public ParameterView()
    {
        InitializeComponent();
    }

    private void BatchEditButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ParameterViewModel vm)
        {
            return;
        }

        var shell = vm.ShellViewModel;
        if (shell.BatchEditParametersCommand.CanExecute(null))
        {
            shell.BatchEditParametersCommand.Execute(ParameterGrid.SelectedItems);
        }
    }
}
