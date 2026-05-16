#nullable enable
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.ViewModels.Modules;
using Moq;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

/// <summary>
/// M7.4: 用 Moq 重写 — 绕开整套 Shell/Bootstrapper，直接构造 GitPullViewModel + TestShell + 服务 mock。
/// 原 11 条 [Fact(Skip)] 全部救回；测试本质是 ViewModel 窄面行为（属性赋值 / 命令委托 / 计算方法）。
/// </summary>
public class GitPullViewModelTests
{
    private static GitPullViewModel CreateSut()
    {
        var shell = new TestShell();
        var gitSvc = new Mock<IGitPullService>(MockBehavior.Loose).Object;
        var syncSvc = new Mock<IGeneratedArtifactSyncService>(MockBehavior.Loose).Object;
        return new GitPullViewModel(shell, gitSvc, syncSvc);
    }

    [Fact]
    public void GitPullModule_Commands_AreNonNull()
    {
        var gitPull = CreateSut();
        Assert.NotNull(gitPull.BrowseGitTargetFolderCommand);
        Assert.NotNull(gitPull.PullGitRepositoryCommand);
        Assert.NotNull(gitPull.OpenGitTargetFolderCommand);
    }

    [Fact]
    public void DefaultValuesAreEmptyOrFalse()
    {
        var gitPull = CreateSut();
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
        var gitPull = CreateSut();
        Assert.True(gitPull.PullGitRepositoryCommand.CanExecute(null));
        gitPull.IsGitPullRunning = true;
        Assert.False(gitPull.PullGitRepositoryCommand.CanExecute(null));
        gitPull.IsGitPullRunning = false;
        Assert.True(gitPull.PullGitRepositoryCommand.CanExecute(null));
    }

    [Fact]
    public void GitPullPropertyChanges_UpdateCanExecute()
    {
        var gitPull = CreateSut();
        Assert.True(gitPull.PullGitRepositoryCommand.CanExecute(null));
        gitPull.IsGitPullRunning = true;
        Assert.False(gitPull.PullGitRepositoryCommand.CanExecute(null));
        gitPull.IsGitPullRunning = false;
        Assert.True(gitPull.PullGitRepositoryCommand.CanExecute(null));
    }

    [Fact]
    public void RestoreGitPullSettings_RestoresAllProperties()
    {
        var gitPull = CreateSut();
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
            AutoCommitMessageTemplate = "Auto commit: {Operation}",
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
        var gitPull = CreateSut();
        gitPull.RestoreGitPullSettings(null);
        Assert.Equal(string.Empty, gitPull.GitRepositoryUrl);
        Assert.Equal(string.Empty, gitPull.GitBranch);
        Assert.True(gitPull.IsSyncGeneratedToGitEnabled);
        Assert.False(gitPull.IsForceResetLocalEnabled);
    }

    [Fact]
    public void BuildGitPullSettingsForConfig_RoundTrips()
    {
        var gitPull = CreateSut();
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
            AutoCommitMessageTemplate = "CI: {Operation}",
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
        var gitPull = CreateSut();
        gitPull.GitTargetFolder = string.Empty;
        Assert.Equal(string.Empty, gitPull.ResolveEffectiveGitFolder());
    }

    [Fact]
    public void ResolveEffectiveGitFolder_ReturnsBase_WhenSubFolderEmpty()
    {
        var gitPull = CreateSut();
        gitPull.GitTargetFolder = @"C:\Base";
        gitPull.GitProjectFolderName = string.Empty;
        Assert.Equal(@"C:\Base", gitPull.ResolveEffectiveGitFolder());
    }

    [Fact]
    public void ResolveEffectiveGitFolder_CombinesPath()
    {
        var gitPull = CreateSut();
        gitPull.GitTargetFolder = @"C:\Base";
        gitPull.GitProjectFolderName = "SubDir";
        var result = gitPull.ResolveEffectiveGitFolder();
        Assert.EndsWith("SubDir", result);
        Assert.StartsWith(@"C:\Base", result);
    }

    [Fact]
    public void ResolveEffectiveGitFolder_StripsInvalidChars()
    {
        var gitPull = CreateSut();
        gitPull.GitTargetFolder = @"C:\Base";
        gitPull.GitProjectFolderName = "Sub/Dir\\Name*?";
        var result = gitPull.ResolveEffectiveGitFolder();
        Assert.EndsWith("SubDirName", result);
    }
}
