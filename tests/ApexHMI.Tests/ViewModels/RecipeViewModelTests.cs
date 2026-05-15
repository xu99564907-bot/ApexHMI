using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class RecipeViewModelTests {
    [Fact(Skip = "M6.4: 需要完整 WPF Application 集成测试基座 — 推迟到 M7 窄面重写")]
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
