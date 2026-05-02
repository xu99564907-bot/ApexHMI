using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class RecipeViewModelTests
{
    [Fact]
    public void RecipeModuleOwnsRecipeCommands()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(shell.Recipe.ApplyRecipeCommand);
        Assert.NotNull(shell.Recipe.CreateRecipeCommand);
        Assert.NotNull(shell.Recipe.DuplicateRecipeCommand);
        Assert.NotNull(shell.Recipe.DeleteRecipeCommand);
        Assert.NotNull(shell.Recipe.CaptureCurrentParametersToRecipeCommand);
        Assert.NotNull(shell.Recipe.LoadRecipesCommand);
        Assert.NotNull(shell.Recipe.SaveRecipesCommand);

        Assert.NotSame(shell.ApplyRecipeCommand, shell.Recipe.ApplyRecipeCommand);
        Assert.NotSame(shell.LoadRecipesCommand, shell.Recipe.LoadRecipesCommand);
        Assert.NotSame(shell.SaveRecipesCommand, shell.Recipe.SaveRecipesCommand);
    }
}
