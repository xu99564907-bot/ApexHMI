using System.Collections.ObjectModel;
using ApexHMI.Models;

namespace ApexHMI.ViewModels.Modules;

public sealed class RecipeViewModel : ModuleViewModelBase
{
    public RecipeViewModel(MainViewModel shell)
        : base(shell, "配方管理")
    {
    }

    public ObservableCollection<RecipeItem> Recipes => Shell.Recipes;
    public ObservableCollection<ParameterItem> ActiveRecipeParameters => Shell.ActiveRecipeParameters;
    public string SelectedRecipeName
    {
        get => Shell.SelectedRecipeName;
        set => Shell.SelectedRecipeName = value;
    }
}
