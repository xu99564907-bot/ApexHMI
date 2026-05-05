using System.Windows;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Dialogs;

public partial class AlarmTermsDialog : Window
{
    public AlarmTermsDialog()
    {
        InitializeComponent();
    }

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AlarmTermsViewModel vm)
            vm.SaveCommand.Execute(null);
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
