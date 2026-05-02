#nullable enable

using System.Threading.Tasks;
using ApexHMI.Models;
using ApexHMI.ViewModels.Modules;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    // ========== Git 仓库拉取（委托给 GitPullViewModel） ==========

    private GitPullViewModel? _gitPullVm;
    private GitPullSettings? _pendingGitPullSettings;

    /// <summary>由 MainWindowViewModel 在构造后注入。</summary>
    internal void SetGitPullViewModel(GitPullViewModel vm)
    {
        _gitPullVm = vm;

        // 如果在 SetGitPullViewModel 之前 LoadConfigAsync 已经执行了 RestoreGitPullSettings，
        // 这里把暂存的设置应用到 ViewModel。
        if (_pendingGitPullSettings is not null)
        {
            vm.RestoreGitPullSettings(_pendingGitPullSettings);
            _pendingGitPullSettings = null;
        }

        OnPropertyChanged(nameof(GitRepositoryUrl));
        OnPropertyChanged(nameof(GitPullStatus));
    }

    private GitPullViewModel GitPullVm => _gitPullVm!;

    // -- 属性委托 --
    public string GitRepositoryUrl { get => GitPullVm.GitRepositoryUrl; set => GitPullVm.GitRepositoryUrl = value; }
    public string GitBranch { get => GitPullVm.GitBranch; set => GitPullVm.GitBranch = value; }
    public string GitTargetFolder { get => GitPullVm.GitTargetFolder; set => GitPullVm.GitTargetFolder = value; }
    public string GitProjectFolderName { get => GitPullVm.GitProjectFolderName; set => GitPullVm.GitProjectFolderName = value; }
    public string GitUsername { get => GitPullVm.GitUsername; set => GitPullVm.GitUsername = value; }
    public string GitAccessToken { get => GitPullVm.GitAccessToken; set => GitPullVm.GitAccessToken = value; }
    public string GitPullStatus => GitPullVm.GitPullStatus;
    public string GitPullLog => GitPullVm.GitPullLog;
    public bool IsGitPullRunning => GitPullVm.IsGitPullRunning;
    public bool IsSyncGeneratedToGitEnabled { get => GitPullVm.IsSyncGeneratedToGitEnabled; set => GitPullVm.IsSyncGeneratedToGitEnabled = value; }
    public bool IsIncludeProjectFilesOnPullEnabled { get => GitPullVm.IsIncludeProjectFilesOnPullEnabled; set => GitPullVm.IsIncludeProjectFilesOnPullEnabled = value; }
    public bool IsForceResetLocalEnabled { get => GitPullVm.IsForceResetLocalEnabled; set => GitPullVm.IsForceResetLocalEnabled = value; }
    public bool IsPushProjectBranchToRemoteEnabled { get => GitPullVm.IsPushProjectBranchToRemoteEnabled; set => GitPullVm.IsPushProjectBranchToRemoteEnabled = value; }
    public bool IsCommitAndPushAfterGenerateEnabled { get => GitPullVm.IsCommitAndPushAfterGenerateEnabled; set => GitPullVm.IsCommitAndPushAfterGenerateEnabled = value; }
    public string GitAutoCommitMessageTemplate { get => GitPullVm.GitAutoCommitMessageTemplate; set => GitPullVm.GitAutoCommitMessageTemplate = value; }

    // -- 命令委托 --
    public IRelayCommand BrowseGitTargetFolderCommand => GitPullVm.BrowseGitTargetFolderCommand;
    public IAsyncRelayCommand PullGitRepositoryCommand => GitPullVm.PullGitRepositoryCommand;
    public IRelayCommand OpenGitTargetFolderCommand => GitPullVm.OpenGitTargetFolderCommand;

    // -- 方法委托 --
    internal void RestoreGitPullSettings(GitPullSettings? settings)
    {
        if (_gitPullVm is null)
        {
            // InitializeAsync 在 SetGitPullViewModel 之前执行，暂存设置。
            _pendingGitPullSettings = settings;
            return;
        }

        GitPullVm.RestoreGitPullSettings(settings);
    }
    internal GitPullSettings BuildGitPullSettingsForConfig() => GitPullVm.BuildGitPullSettingsForConfig();
    internal string ResolveEffectiveGitFolder() => GitPullVm.ResolveEffectiveGitFolder();

    private async Task SyncGeneratedArtifactsToGitAsync(Models.IoGenerationResult result)
    {
        await Task.CompletedTask;
        AddLog("IO 生成", "已跳过 Git 根目录 .st 追加，仅保留 _exported 导入源。", "Info");
    }

    /// <summary>
    /// 生成程序后将 artifact 追加到 InProShop 脚本约定的目录。委托给 GitPullViewModel。
    /// </summary>
    private async Task PrepareInProShopProjectImportAsync(IoGenerationResult result)
    {
        await GitPullVm.PrepareInProShopProjectImportAsync(result);
    }
}
