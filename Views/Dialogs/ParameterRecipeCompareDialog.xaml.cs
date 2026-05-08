using System.Windows;
using ApexHMI.Models;

namespace ApexHMI.Views.Dialogs;

public partial class ParameterRecipeCompareDialog : Window
{
    public ParameterRecipeCompareDialog() => InitializeComponent();

    public string SelectedRecipeName { get; private set; } = string.Empty;
    public bool ApplyRecipeRequested { get; private set; }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (RecipeCombo.SelectedItem is RecipeItem recipe)
        {
            SelectedRecipeName = recipe.Name;
            ApplyRecipeRequested = ApplyCheck.IsChecked == true;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("请先选择一个配方", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
