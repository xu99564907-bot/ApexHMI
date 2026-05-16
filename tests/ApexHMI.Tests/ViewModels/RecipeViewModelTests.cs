#nullable enable
using ApexHMI.Interfaces;
using ApexHMI.ViewModels.Modules;
using Moq;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

/// <summary>M7.4: Moq + TestShell 重写。验证 Recipe 模块命令独立于 Shell 命令。</summary>
public class RecipeViewModelTests
{
    [Fact]
    public void RecipeModuleOwnsRecipeCommands()
    {
        var shell = new TestShell();
        var recipeSvc = new Mock<IRecipeService>(MockBehavior.Loose).Object;
        var recipe = new RecipeViewModel(shell, recipeSvc);

        Assert.NotNull(recipe.ApplyRecipeCommand);
        Assert.NotNull(recipe.CreateRecipeCommand);
        Assert.NotNull(recipe.DuplicateRecipeCommand);
        Assert.NotNull(recipe.DeleteRecipeCommand);
        Assert.NotNull(recipe.CaptureCurrentParametersToRecipeCommand);
        Assert.NotNull(recipe.LoadRecipesCommand);
        Assert.NotNull(recipe.SaveRecipesCommand);

        Assert.NotSame(shell.ApplyRecipeCommand, recipe.ApplyRecipeCommand);
        Assert.NotSame(shell.LoadRecipesCommand, recipe.LoadRecipesCommand);
        Assert.NotSame(shell.SaveRecipesCommand, recipe.SaveRecipesCommand);
    }
}
