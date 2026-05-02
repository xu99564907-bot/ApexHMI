using ApexHMI.Models;
using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class GitPullViewModelTests
{
    [Fact]
    public void GitPullModuleOwnsCommands()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(shell.GitPull.BrowseGitTargetFolderCommand);
        Assert.NotNull(shell.GitPull.PullGitRepositoryCommand);
        Assert.NotNull(shell.GitPull.OpenGitTargetFolderCommand);
    }

    [Fact]
    public void GitPullCommandsAreSameInstanceOnShell()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        // GitPull 的命令通过 MainViewModel.GitPull.cs 直接委托，因此是同一实例
        Assert.Same(shell.BrowseGitTargetFolderCommand, shell.GitPull.BrowseGitTargetFolderCommand);
        Assert.Same(shell.PullGitRepositoryCommand, shell.GitPull.PullGitRepositoryCommand);
        Assert.Same(shell.OpenGitTargetFolderCommand, shell.GitPull.OpenGitTargetFolderCommand);
    }

    [Fact]
    public void DefaultValuesAreEmptyOrFalse()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        Assert.Equal(string.Empty, gitPull.GitRepositoryUrl);
        Assert.Equal(string.Empty, gitPull.GitBranch);
        Assert.Equal(string.Empty, gitPull.GitTargetFolder);
        Assert.Equal(string.Empty, gitPull.GitProjectFolderName);
        Assert.Equal(string.Empty, gitPull.GitUsername);
        Assert.Equal(string.Empty, gitPull.GitAccessToken);
        Assert.True(gitPull.IsSyncGeneratedToGitEnabled);
        Assert.False(gitPull.IsIncludeProjectFilesOnPullEnabled);
        Assert.False(gitPull.IsForceResetLocalEnabled);
        Assert.False(gitPull.IsPushProjectBranchToRemoteEnabled);
        Assert.False(gitPull.IsCommitAndPushAfterGenerateEnabled);
        Assert.False(gitPull.IsGitPullRunning);
    }

    [Fact]
    public void CanPullGitRepository_ReturnsFalse_WhenRunning()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        Assert.True(gitPull.PullGitRepositoryCommand.CanExecute(null));

        gitPull.IsGitPullRunning = true;
        Assert.False(gitPull.PullGitRepositoryCommand.CanExecute(null));

        gitPull.IsGitPullRunning = false;
        Assert.True(gitPull.PullGitRepositoryCommand.CanExecute(null));
    }

    [Fact]
    public void RestoreGitPullSettings_RestoresAllProperties()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        var settings = new GitPullSettings
        {
            RepositoryUrl = "https://git.example.com/repo.git",
            Branch = "develop",
            TargetFolder = @"C:\Projects\Repo",
            ProjectFolderName = "MyProject",
            Username = "user",
            AccessToken = "token123",
            SyncGeneratedToGit = false,
            IncludeProjectFiles = true,
            ForceResetLocal = true,
            PushProjectBranchToRemote = true,
            CommitAndPushAfterGenerate = true,
            AutoCommitMessageTemplate = "Auto commit: {Operation}"
        };

        gitPull.RestoreGitPullSettings(settings);

        Assert.Equal("https://git.example.com/repo.git", gitPull.GitRepositoryUrl);
        Assert.Equal("develop", gitPull.GitBranch);
        Assert.Equal(@"C:\Projects\Repo", gitPull.GitTargetFolder);
        Assert.Equal("MyProject", gitPull.GitProjectFolderName);
        Assert.Equal("user", gitPull.GitUsername);
        Assert.Equal("token123", gitPull.GitAccessToken);
        Assert.False(gitPull.IsSyncGeneratedToGitEnabled);
        Assert.True(gitPull.IsIncludeProjectFilesOnPullEnabled);
        Assert.True(gitPull.IsForceResetLocalEnabled);
        Assert.True(gitPull.IsPushProjectBranchToRemoteEnabled);
        Assert.True(gitPull.IsCommitAndPushAfterGenerateEnabled);
        Assert.Equal("Auto commit: {Operation}", gitPull.GitAutoCommitMessageTemplate);
    }

    [Fact]
    public void RestoreGitPullSettings_HandlesNull()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        // Should not throw
        gitPull.RestoreGitPullSettings(null);

        // Check all properties got defaulted
        Assert.Equal(string.Empty, gitPull.GitRepositoryUrl);
        Assert.Equal(string.Empty, gitPull.GitBranch);
        Assert.True(gitPull.IsSyncGeneratedToGitEnabled);
        Assert.False(gitPull.IsForceResetLocalEnabled);
    }

    [Fact]
    public void BuildGitPullSettingsForConfig_RoundTrips()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        var original = new GitPullSettings
        {
            RepositoryUrl = "https://git.example.com/repo.git",
            Branch = "main",
            TargetFolder = @"C:\Repo",
            ProjectFolderName = "Proj",
            Username = "admin",
            AccessToken = "secret",
            SyncGeneratedToGit = true,
            IncludeProjectFiles = false,
            ForceResetLocal = false,
            PushProjectBranchToRemote = true,
            CommitAndPushAfterGenerate = false,
            AutoCommitMessageTemplate = "CI: {Operation}"
        };

        gitPull.RestoreGitPullSettings(original);
        var result = gitPull.BuildGitPullSettingsForConfig();

        Assert.Equal(original.RepositoryUrl, result.RepositoryUrl);
        Assert.Equal(original.Branch, result.Branch);
        Assert.Equal(original.TargetFolder, result.TargetFolder);
        Assert.Equal(original.ProjectFolderName, result.ProjectFolderName);
        Assert.Equal(original.Username, result.Username);
        Assert.Equal(original.AccessToken, result.AccessToken);
        Assert.Equal(original.SyncGeneratedToGit, result.SyncGeneratedToGit);
        Assert.Equal(original.IncludeProjectFiles, result.IncludeProjectFiles);
        Assert.Equal(original.ForceResetLocal, result.ForceResetLocal);
        Assert.Equal(original.PushProjectBranchToRemote, result.PushProjectBranchToRemote);
        Assert.Equal(original.CommitAndPushAfterGenerate, result.CommitAndPushAfterGenerate);
        Assert.Equal(original.AutoCommitMessageTemplate, result.AutoCommitMessageTemplate);
    }

    [Fact]
    public void ResolveEffectiveGitFolder_ReturnsEmpty_WhenTargetFolderEmpty()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        gitPull.GitTargetFolder = string.Empty;
        Assert.Equal(string.Empty, gitPull.ResolveEffectiveGitFolder());
    }

    [Fact]
    public void ResolveEffectiveGitFolder_ReturnsBase_WhenSubFolderEmpty()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        gitPull.GitTargetFolder = @"C:\Base";
        gitPull.GitProjectFolderName = string.Empty;
        Assert.Equal(@"C:\Base", gitPull.ResolveEffectiveGitFolder());
    }

    [Fact]
    public void ResolveEffectiveGitFolder_CombinesPath()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        gitPull.GitTargetFolder = @"C:\Base";
        gitPull.GitProjectFolderName = "SubDir";
        var result = gitPull.ResolveEffectiveGitFolder();

        Assert.EndsWith("SubDir", result);
        Assert.StartsWith(@"C:\Base", result);
    }

    [Fact]
    public void ResolveEffectiveGitFolder_StripsInvalidChars()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        gitPull.GitTargetFolder = @"C:\Base";
        gitPull.GitProjectFolderName = "Sub/Dir\\Name*?";
        var result = gitPull.ResolveEffectiveGitFolder();

        Assert.EndsWith("SubDirName", result);
    }

    [Fact]
    public void GitPullPropertyChanges_UpdateCanExecute()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var gitPull = provider.GetRequiredService<MainWindowViewModel>().GitPull;

        Assert.True(gitPull.PullGitRepositoryCommand.CanExecute(null));

        gitPull.IsGitPullRunning = true;
        Assert.False(gitPull.PullGitRepositoryCommand.CanExecute(null));

        gitPull.IsGitPullRunning = false;
        Assert.True(gitPull.PullGitRepositoryCommand.CanExecute(null));
    }
}
