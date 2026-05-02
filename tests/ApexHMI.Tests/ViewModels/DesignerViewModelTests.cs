using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class DesignerViewModelTests
{
    [Fact]
    public void DesignerModuleOwnsAll20DesignerCommands()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(shell.Designer.SaveConfigCommand);
        Assert.NotNull(shell.Designer.LoadConfigCommand);
        Assert.NotNull(shell.Designer.ImportIoTableCommand);
        Assert.NotNull(shell.Designer.ClearIoTableCommand);
        Assert.NotNull(shell.Designer.SaveIoTableToSourceCommand);
        Assert.NotNull(shell.Designer.GenerateIoProgramsCommand);
        Assert.NotNull(shell.Designer.ResetAutoProgramFlowCommand);
        Assert.NotNull(shell.Designer.AddAutoProgramStepCommand);
        Assert.NotNull(shell.Designer.GenerateAutoProgramsCommand);
        Assert.NotNull(shell.Designer.OpenGeneratedIoFolderCommand);
        Assert.NotNull(shell.Designer.OpenGeneratedAutoFolderCommand);
        Assert.NotNull(shell.Designer.OpenGeneratedIoFileCommand);
        Assert.NotNull(shell.Designer.ApplyRuntimeTemplateCommand);
        Assert.NotNull(shell.Designer.AddDesignerElementCommand);
        Assert.NotNull(shell.Designer.AddDesignerElementAtDropCommand);
        Assert.NotNull(shell.Designer.StartToolboxDragCommand);
        Assert.NotNull(shell.Designer.RemoveSelectedDesignerElementCommand);
        Assert.NotNull(shell.Designer.CopySelectedDesignerElementCommand);
        Assert.NotNull(shell.Designer.PasteDesignerElementCommand);
        Assert.NotNull(shell.Designer.MoveSelectedElementCommand);
    }

    [Fact]
    public void DesignerModuleOwnsGitPullCommands()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(shell.Designer.BrowseGitTargetFolderCommand);
        Assert.NotNull(shell.Designer.PullGitRepositoryCommand);
        Assert.NotNull(shell.Designer.OpenGitTargetFolderCommand);
    }

    [Fact]
    public void DesignerCommandsAreDistinctFromShell()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotSame(shell.ImportIoTableCommand, shell.Designer.ImportIoTableCommand);
        Assert.NotSame(shell.GenerateIoProgramsCommand, shell.Designer.GenerateIoProgramsCommand);
        Assert.NotSame(shell.GenerateAutoProgramsCommand, shell.Designer.GenerateAutoProgramsCommand);
        Assert.NotSame(shell.AddDesignerElementCommand, shell.Designer.AddDesignerElementCommand);
        Assert.NotSame(shell.RemoveSelectedDesignerElementCommand, shell.Designer.RemoveSelectedDesignerElementCommand);
        Assert.NotSame(shell.CopySelectedDesignerElementCommand, shell.Designer.CopySelectedDesignerElementCommand);
        Assert.NotSame(shell.PasteDesignerElementCommand, shell.Designer.PasteDesignerElementCommand);
        Assert.NotSame(shell.MoveSelectedElementCommand, shell.Designer.MoveSelectedElementCommand);
        Assert.NotSame(shell.OpenGeneratedIoFolderCommand, shell.Designer.OpenGeneratedIoFolderCommand);
        Assert.NotSame(shell.OpenGeneratedAutoFolderCommand, shell.Designer.OpenGeneratedAutoFolderCommand);
    }

    [Fact]
    public void DesignerModuleDelegatesCollections()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.Same(shell.DesignerPages, shell.Designer.Pages);
        Assert.Same(shell.DesignerElements, shell.Designer.Elements);
        Assert.Same(shell.IoTableRows, shell.Designer.IoTableRows);
        Assert.Same(shell.GeneratedIoPrograms, shell.Designer.GeneratedIoPrograms);
        Assert.Same(shell.GeneratedAutoPrograms, shell.Designer.GeneratedAutoPrograms);
        Assert.Same(shell.AutoProgramFlowNodes, shell.Designer.AutoProgramFlowNodes);
        Assert.Same(shell.Tags, shell.Designer.Tags);
        Assert.Same(shell.DesignerActionOptions, shell.Designer.DesignerActionOptions);
    }

    [Fact]
    public void DesignerModuleDelegatesModeFlags()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.Equal(shell.IsDesignMode, shell.Designer.IsDesignMode);
        Assert.Equal(shell.IsRuntimeMode, shell.Designer.IsRuntimeMode);
        Assert.Equal(shell.IsDesignerCanvasPageVisible, shell.Designer.IsDesignerCanvasPageVisible);
    }

    [Fact]
    public void DesignerModuleDelegatesIoProperties()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.Equal(shell.CanSaveIoTable, shell.Designer.CanSaveIoTable);
        Assert.Equal(shell.GeneratedIoOutputDirectory, shell.Designer.GeneratedIoOutputDirectory);
        Assert.Equal(shell.GeneratedAutoOutputDirectory, shell.Designer.GeneratedAutoOutputDirectory);
    }
}
