using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    private void SeedRecipes()
    {
        if (this is Shell.MainWindowViewModel { Recipe: { } recipe })
        {
            recipe.SeedRecipes();
        }
    }

    [RelayCommand]
    private async Task SaveRecipesAsync()
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            await shell.Recipe.SaveRecipesAsync();
        }
    }

    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            await shell.Recipe.LoadRecipesAsync();
        }
    }

    [RelayCommand]
    private void ApplyRecipe(string? recipeName)
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            shell.Recipe.ApplyRecipe(recipeName);
        }
    }

    [RelayCommand]
    private void CreateRecipe()
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            shell.Recipe.CreateRecipe();
        }
    }

    [RelayCommand]
    private void DuplicateRecipe()
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            shell.Recipe.DuplicateRecipe();
        }
    }

    [RelayCommand]
    private void DeleteRecipe()
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            shell.Recipe.DeleteRecipe();
        }
    }

    [RelayCommand]
    private void CaptureCurrentParametersToRecipe()
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            shell.Recipe.CaptureCurrentParametersToRecipe();
        }
    }
}
