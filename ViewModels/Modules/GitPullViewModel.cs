using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ApexHMI.ViewModels.Modules;

public sealed partial class GitPullViewModel : ModuleViewModelBase
{
    private readonly IGitPullService _gitPullService;
    private readonly IGeneratedArtifactSyncService _generatedArtifactSyncService;
    private CancellationTokenSource? _autoSaveCts;
    private bool _suppressAutoSave;

    public GitPullViewModel(MainViewModel shell, IGitPullService gitPullService, IGeneratedArtifactSyncService generatedArtifactSyncService)
        : base(shell, "Git 仓库拉取")
    {
        _gitPullService = gitPullService;
        _generatedArtifactSyncService = generatedArtifactSyncService;
    }

    // ========== Observable state ==========

    [ObservableProperty] private string gitRepositoryUrl = string.Empty;
    [ObservableProperty] private string gitBranch = string.Empty;
    [ObservableProperty] private string gitTargetFolder = string.Empty;
    [ObservableProperty] private string gitProjectFolderName = string.Empty;
    [ObservableProperty] private string gitUsername = string.Empty;
    [ObservableProperty] private string gitAccessToken = string.Empty;
    [ObservableProperty] private string gitPullStatus = "尚未执行拉取。";
    [ObservableProperty] private string gitPullLog = string.Empty;
    [ObservableProperty] private bool isGitPullRunning;
    [ObservableProperty] private bool isSyncGeneratedToGitEnabled = true;
    [ObservableProperty] private bool isIncludeProjectFilesOnPullEnabled;
    [ObservableProperty] private bool isForceResetLocalEnabled;
    [ObservableProperty] private bool isPushProjectBranchToRemoteEnabled;
    [ObservableProperty] private bool isCommitAndPushAfterGenerateEnabled;
    [ObservableProperty] private string gitAutoCommitMessageTemplate = string.Empty;

    // ========== Property-change side effects ==========

    partial void OnIsGitPullRunningChanged(bool value)
    {
        PullGitRepositoryCommand.NotifyCanExecuteChanged();
        BrowseGitTargetFolderCommand.NotifyCanExecuteChanged();
    }

    partial void OnGitRepositoryUrlChanged(string value) => ScheduleAutoSave();
    partial void OnGitTargetFolderChanged(string value) => ScheduleAutoSave();
    partial void OnGitProjectFolderNameChanged(string value) => ScheduleAutoSave();
    partial void OnGitBranchChanged(string value) => ScheduleAutoSave();
    partial void OnGitUsernameChanged(string value) => ScheduleAutoSave();
    partial void OnGitAccessTokenChanged(string value) => ScheduleAutoSave();
    partial void OnIsSyncGeneratedToGitEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnIsIncludeProjectFilesOnPullEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnIsForceResetLocalEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnIsPushProjectBranchToRemoteEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnIsCommitAndPushAfterGenerateEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnGitAutoCommitMessageTemplateChanged(string value) => ScheduleAutoSave();

    // ========== Commands ==========

    [RelayCommand]
    private void BrowseGitTargetFolder()
    {
        try
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择代码保存目录",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrWhiteSpace(GitTargetFolder) && Directory.Exists(GitTargetFolder))
            {
                dialog.SelectedPath = GitTargetFolder;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                GitTargetFolder = dialog.SelectedPath;
            }
        }
        catch (Exception ex)
        {
            Shell.SystemMessage = $"选择目录失败：{ex.Message}";
            Shell.AddLog("Git", Shell.SystemMessage, "Error");
        }
    }

    private bool CanPullGitRepository() => !IsGitPullRunning;

    [RelayCommand(CanExecute = nameof(CanPullGitRepository))]
    private async Task PullGitRepositoryAsync()
    {
        if (IsGitPullRunning) return;

        if (string.IsNullOrWhiteSpace(GitRepositoryUrl))
        {
            Shell.ShowPopup("Git 拉取", "请先填写仓库地址。", "Warning");
            return;
        }

        if (string.IsNullOrWhiteSpace(GitTargetFolder))
        {
            Shell.ShowPopup("Git 拉取", "请先选择本地保存目录。", "Warning");
            return;
        }

        IsGitPullRunning = true;
        GitPullLog = string.Empty;
        GitPullStatus = "正在拉取代码，请稍候...";
        Shell.AddLog("Git", $"开始拉取 {GitRepositoryUrl} 到 {GitTargetFolder}", "Info");

        var settings = new GitPullSettings
        {
            RepositoryUrl = GitRepositoryUrl.Trim(),
            Branch = (GitBranch ?? string.Empty).Trim(),
            TargetFolder = GitTargetFolder.Trim(),
            ProjectFolderName = (GitProjectFolderName ?? string.Empty).Trim(),
            Username = (GitUsername ?? string.Empty).Trim(),
            AccessToken = (GitAccessToken ?? string.Empty).Trim(),
            IncludeProjectFiles = IsIncludeProjectFilesOnPullEnabled,
            ForceResetLocal = IsForceResetLocalEnabled,
            PushProjectBranchToRemote = IsPushProjectBranchToRemoteEnabled
        };

        var logBuffer = new System.Text.StringBuilder();
        int lineCount = 0;
        var progress = new Progress<string>(line =>
        {
            logBuffer.AppendLine(line);
            lineCount++;
            if (lineCount % 50 == 0)
            {
                GitPullLog = logBuffer.ToString();
            }
        });

        try
        {
            var result = await Task.Run(() => _gitPullService.PullAsync(settings, progress));

            GitPullLog = logBuffer.ToString();

            GitPullStatus = result.IsFreshClone
                ? $"克隆完成：{result.TargetFolder}"
                : $"拉取完成：{result.TargetFolder}";

            if (!string.IsNullOrEmpty(result.WorkingBranch))
            {
                GitPullStatus += result.BranchPushed
                    ? $"（分支 {result.WorkingBranch} · 已推送到远端）"
                    : $"（分支 {result.WorkingBranch}）";
            }

            if (!string.IsNullOrEmpty(result.HeadCommit))
            {
                var shortCommit = result.HeadCommit.Length > 8 ? result.HeadCommit.Substring(0, 8) : result.HeadCommit;
                GitPullStatus += $"（HEAD {shortCommit} {result.HeadSubject}）";
            }

            if (result.RestoredFileCount > 0)
            {
                GitPullStatus += $"（自动恢复 {result.RestoredFileCount} 个本地缺失文件）";
            }
            else if (result.LocalDeletedDetected > 0)
            {
                GitPullStatus += $"（警告：本地仍缺失 {result.LocalDeletedDetected} 个文件，有本地修改未自动恢复，请勾选\"强制重置本地\"再拉一次）";
            }

            Shell.SystemMessage = GitPullStatus;
            Shell.AddLog("Git", GitPullStatus, "Info");
            Shell.ShowPopup("Git 拉取完成", GitPullStatus, "Info");

            try { await PostPullPostProcessAsync(result.TargetFolder); }
            catch (Exception postEx) { Shell.AddLog("Git", $"拉取后置处理失败：{postEx.Message}", "Warning"); }

            try { await Shell.PersistConfigAsync(updateStatus: false); }
            catch (Exception persistEx) { Shell.AddLog("Git", $"保存 Git 配置失败：{persistEx.Message}", "Warning"); }
        }
        catch (Exception ex)
        {
            GitPullLog = logBuffer.ToString();
            GitPullStatus = $"拉取失败：{ex.Message}";
            Shell.SystemMessage = GitPullStatus;
            Shell.AddLog("Git", GitPullStatus, "Error");
            Shell.ShowPopup("Git 拉取失败", ex.Message, "Error");
        }
        finally
        {
            IsGitPullRunning = false;
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "git-pull-last.log");
                var header = $"===== GitPull Run @ {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====" + Environment.NewLine
                           + $"Status : {GitPullStatus}" + Environment.NewLine
                           + $"Target : {GitTargetFolder}" + Environment.NewLine
                           + $"Project: {GitProjectFolderName}" + Environment.NewLine
                           + $"Force  : {IsForceResetLocalEnabled}" + Environment.NewLine
                           + $"Push   : {IsPushProjectBranchToRemoteEnabled}" + Environment.NewLine
                           + "================================================" + Environment.NewLine;
                File.WriteAllText(logPath, header + logBuffer.ToString());
            }
            catch { /* 落盘失败忽略 */ }
        }
    }

    [RelayCommand]
    private void OpenGitTargetFolder()
    {
        try
        {
            var effective = ResolveEffectiveGitFolder();
            var toOpen = Directory.Exists(effective) ? effective : GitTargetFolder;
            _gitPullService.OpenTargetFolder(toOpen);
        }
        catch (Exception ex)
        {
            Shell.SystemMessage = $"打开目录失败：{ex.Message}";
            Shell.AddLog("Git", Shell.SystemMessage, "Error");
            Shell.ShowPopup("打开目录失败", ex.Message, "Error");
        }
    }

    // ========== Settings persistence ==========

    public void RestoreGitPullSettings(GitPullSettings? settings)
    {
        settings ??= new GitPullSettings();
        _suppressAutoSave = true;
        try
        {
            GitRepositoryUrl = settings.RepositoryUrl ?? string.Empty;
            GitBranch = settings.Branch ?? string.Empty;
            GitTargetFolder = settings.TargetFolder ?? string.Empty;
            GitProjectFolderName = settings.ProjectFolderName ?? string.Empty;
            GitUsername = settings.Username ?? string.Empty;
            GitAccessToken = settings.AccessToken ?? string.Empty;
            IsSyncGeneratedToGitEnabled = settings.SyncGeneratedToGit;
            IsIncludeProjectFilesOnPullEnabled = settings.IncludeProjectFiles;
            IsForceResetLocalEnabled = settings.ForceResetLocal;
            IsPushProjectBranchToRemoteEnabled = settings.PushProjectBranchToRemote;
            IsCommitAndPushAfterGenerateEnabled = settings.CommitAndPushAfterGenerate;
            GitAutoCommitMessageTemplate = settings.AutoCommitMessageTemplate ?? string.Empty;
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    public GitPullSettings BuildGitPullSettingsForConfig()
    {
        return new GitPullSettings
        {
            RepositoryUrl = GitRepositoryUrl ?? string.Empty,
            Branch = GitBranch ?? string.Empty,
            TargetFolder = GitTargetFolder ?? string.Empty,
            ProjectFolderName = GitProjectFolderName ?? string.Empty,
            Username = GitUsername ?? string.Empty,
            AccessToken = GitAccessToken ?? string.Empty,
            SyncGeneratedToGit = IsSyncGeneratedToGitEnabled,
            IncludeProjectFiles = IsIncludeProjectFilesOnPullEnabled,
            ForceResetLocal = IsForceResetLocalEnabled,
            PushProjectBranchToRemote = IsPushProjectBranchToRemoteEnabled,
            CommitAndPushAfterGenerate = IsCommitAndPushAfterGenerateEnabled,
            AutoCommitMessageTemplate = GitAutoCommitMessageTemplate ?? string.Empty
        };
    }

    private void ScheduleAutoSave()
    {
        if (_suppressAutoSave) return;

        _autoSaveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _autoSaveCts = cts;

        _ = Task.Run(async () =>
        {
            try { await Task.Delay(TimeSpan.FromMilliseconds(1500), cts.Token); }
            catch (TaskCanceledException) { return; }

            try
            {
                await Shell.PersistConfigAsync(updateStatus: false);
                Shell.AddLog("Git", "Git 仓库地址与保存目录已自动写入配置文件。", "Info");
            }
            catch (Exception ex)
            {
                Shell.AddLog("Git", $"自动保存 Git 配置失败：{ex.Message}", "Warning");
            }
        });
    }

    // ========== Helpers ==========

    public string ResolveEffectiveGitFolder()
    {
        var baseFolder = (GitTargetFolder ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseFolder)) return string.Empty;

        var sub = (GitProjectFolderName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sub)) return baseFolder;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(sub.Length);
        foreach (var ch in sub)
        {
            if (ch == '/' || ch == '\\') continue;
            if (Array.IndexOf(invalid, ch) >= 0) continue;
            sb.Append(ch);
        }
        var cleaned = sb.ToString();
        if (string.IsNullOrWhiteSpace(cleaned)) return baseFolder;
        return Path.GetFullPath(Path.Combine(baseFolder, cleaned));
    }

    private async Task PostPullPostProcessAsync(string effectiveFolder)
    {
        if (string.IsNullOrWhiteSpace(effectiveFolder) || !Directory.Exists(effectiveFolder)) return;

        string? projectFilePath = null;
        try { projectFilePath = await Task.Run(() => RenameProjectFileIfNeeded(effectiveFolder)); }
        catch (Exception ex) { Shell.AddLog("Git", $"重命名 .project 文件失败：{ex.Message}", "Warning"); }

        projectFilePath ??= FindSingleProjectFile(effectiveFolder);
        if (!string.IsNullOrEmpty(projectFilePath))
        {
            Shell.AddLog("Git", $"已识别 .project 文件（拉取后不自动打开）：{projectFilePath}", "Info");
        }
    }

    private string? RenameProjectFileIfNeeded(string effectiveFolder)
    {
        var target = (GitProjectFolderName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            Shell.AddLog("Git", "未填写项目文件夹 / 分支名，跳过 .project 文件重命名。", "Warning");
            return FindSingleProjectFile(effectiveFolder);
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(target.Length);
        foreach (var ch in target)
        {
            if (ch == '/' || ch == '\\') continue;
            if (Array.IndexOf(invalid, ch) >= 0) continue;
            sb.Append(ch);
        }
        var cleaned = sb.ToString();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            Shell.AddLog("Git", "项目文件夹 / 分支名清洗后为空，跳过 .project 文件重命名。", "Warning");
            return FindSingleProjectFile(effectiveFolder);
        }

        var projectFiles = Directory.GetFiles(effectiveFolder, "*.project", SearchOption.TopDirectoryOnly);
        if (projectFiles.Length == 0)
        {
            Shell.AddLog("Git", $"{effectiveFolder} 根目录未找到 .project 文件，跳过重命名。", "Info");
            return null;
        }

        var desiredName = cleaned + ".project";
        var desiredPath = Path.Combine(effectiveFolder, desiredName);

        if (projectFiles.Length > 1)
        {
            foreach (var p in projectFiles)
            {
                if (string.Equals(Path.GetFileName(p), desiredName, StringComparison.OrdinalIgnoreCase))
                {
                    Shell.AddLog("Git", $"根目录存在多个 .project，已匹配到同名目标：{p}", "Info");
                    return p;
                }
            }
            Shell.AddLog("Git", $"{effectiveFolder} 根目录存在多个 .project 文件，无法自动重命名，请手动处理。", "Warning");
            return null;
        }

        var current = projectFiles[0];
        var currentStem = Path.GetFileNameWithoutExtension(current);

        if (string.Equals(Path.GetFileName(current), desiredName, StringComparison.OrdinalIgnoreCase))
        {
            RenameExportedFolderIfNeeded(effectiveFolder, currentStem, cleaned);
            return current;
        }

        if (File.Exists(desiredPath))
        {
            Shell.AddLog("Git", $"目标文件已存在，跳过重命名：{desiredPath}", "Warning");
            RenameExportedFolderIfNeeded(effectiveFolder, currentStem, cleaned);
            return desiredPath;
        }

        try
        {
            File.Move(current, desiredPath);
            Shell.AddLog("Git", $".project 已重命名：{Path.GetFileName(current)} -> {desiredName}", "Info");
            RenameExportedFolderIfNeeded(effectiveFolder, currentStem, cleaned);
            return desiredPath;
        }
        catch (Exception ex)
        {
            Shell.AddLog("Git", $"重命名 {current} 失败：{ex.Message}", "Warning");
            return current;
        }
    }

    private void RenameExportedFolderIfNeeded(string effectiveFolder, string oldStem, string newStem)
    {
        if (string.IsNullOrWhiteSpace(oldStem) || string.IsNullOrWhiteSpace(newStem)) return;

        const string suffix = "_exported";
        var oldFolderName = oldStem + suffix;
        var newFolderName = newStem + suffix;

        if (string.Equals(oldFolderName, newFolderName, StringComparison.OrdinalIgnoreCase)) return;

        var oldPath = Path.Combine(effectiveFolder, oldFolderName);
        var newPath = Path.Combine(effectiveFolder, newFolderName);

        if (!Directory.Exists(oldPath)) return;

        if (Directory.Exists(newPath))
        {
            Shell.AddLog("Git", $"目标文件夹已存在，跳过 _exported 重命名：{newPath}", "Warning");
            return;
        }

        try
        {
            Directory.Move(oldPath, newPath);
            Shell.AddLog("Git", $"_exported 文件夹已重命名：{oldFolderName} -> {newFolderName}", "Info");
        }
        catch (Exception ex)
        {
            Shell.AddLog("Git", $"重命名文件夹 {oldPath} 失败：{ex.Message}", "Warning");
        }
    }

    private static string? FindSingleProjectFile(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
        var files = Directory.GetFiles(folder, "*.project", SearchOption.TopDirectoryOnly);
        return files.Length == 1 ? files[0] : null;
    }

    /// <summary>返回 _exported 目录路径，未配置或找不到 .project 时返回 null。</summary>
    public string? TryGetExportedDirectory()
    {
        var projectFolder = ResolveEffectiveGitFolder();
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder)) return null;
        var projectFilePath = RenameProjectFileIfNeeded(projectFolder) ?? FindSingleProjectFile(projectFolder);
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath)) return null;
        var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(projectFolder, projectName + "_exported");
    }

    // ========== InProShop 导入准备 ==========

    /// <summary>
    /// 生成程序后，把同一批 artifact 追加到 InProShop 脚本约定的 {ProjectName}_exported 目录，
    /// 并把导入脚本放到 .project 同目录。真正写入 .project 必须由 InProShop/CODESYS 的 scriptengine 执行。
    /// </summary>
    public async Task PrepareInProShopProjectImportAsync(IoGenerationResult result)
    {
        var projectFolder = ResolveEffectiveGitFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            Shell.AddLog("InProShop", "未配置 Git 保存目录，跳过 .project 导入准备。", "Info");
            return;
        }

        if (!Directory.Exists(projectFolder))
        {
            Shell.AddLog("InProShop", $"工程目录不存在，跳过 .project 导入准备：{projectFolder}", "Warning");
            return;
        }

        var projectFilePath = RenameProjectFileIfNeeded(projectFolder) ?? FindSingleProjectFile(projectFolder);
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
        {
            Shell.AddLog("InProShop", $"未在工程目录找到唯一 .project 文件，跳过导入准备：{projectFolder}", "Warning");
            return;
        }

        var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
        var exportedDirectory = Path.Combine(projectFolder, projectName + "_exported");
        var createdExportDirectory = false;
        if (!Directory.Exists(exportedDirectory))
        {
            Directory.CreateDirectory(exportedDirectory);
            createdExportDirectory = true;
        }

        var syncResult = await _generatedArtifactSyncService.AppendArtifactsAsync(
            result.Artifacts,
            exportedDirectory,
            Shell.IoOperationNumber);

        foreach (var entry in syncResult.Appended)
        {
            Shell.AddLog("InProShop", $"已准备导入文件 {entry.DisplayName} -> {entry.TargetPath}", "Info");
        }

        foreach (var skipped in syncResult.Skipped)
        {
            Shell.AddLog("InProShop", $"跳过导入文件 {skipped.DisplayName}（{skipped.Reason}）", "Warning");
        }

        var scriptSourcePath = ResolveInProShopImportScriptPath();
        if (!string.IsNullOrWhiteSpace(scriptSourcePath))
        {
            var scriptTargetPath = Path.Combine(projectFolder, "inproshop_import.py");
            File.Copy(scriptSourcePath, scriptTargetPath, overwrite: true);
            Shell.AddLog("InProShop", $"导入脚本已复制：{scriptTargetPath}", "Info");

            var confirmed = await ConfirmRunImportScriptOnUiThreadAsync();
            if (confirmed)
                await RunInProShopImportScriptAsync(projectFilePath, scriptTargetPath);
            else
                Shell.AddLog("InProShop", "用户取消，跳过执行导入脚本。", "Info");
        }
        else
        {
            Shell.AddLog("InProShop", "未找到 inproshop_import.py，无法复制导入脚本。", "Warning");
        }

        if (createdExportDirectory)
        {
            Shell.AddLog("InProShop", $"已创建 {projectName}_exported。建议先从 InProShop 导出一次完整工程，再执行导入脚本。", "Warning");
        }

        if (IsCommitAndPushAfterGenerateEnabled && syncResult.Appended.Count > 0)
        {
            await CommitAndPushGeneratedAsync(projectFolder);
        }

        Shell.AddLog("InProShop", $".project 导入准备完成：{Path.GetFileName(projectFilePath)}。生成流程不会自动打开 InoProShop。", "Info");
    }

    /// <summary>
    /// 直接运行 InProShop 导入脚本（不复制 artifacts，仅执行脚本）。
    /// 用于 SFC 生成后自动刷新 .project。
    /// </summary>
    public async Task RunImportScriptIfAvailableAsync()
    {
        var projectFolder = ResolveEffectiveGitFolder();
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
        {
            Shell.AddLog("InProShop", "未配置 Git 保存目录，跳过自动导入脚本。", "Info");
            return;
        }

        var projectFilePath = RenameProjectFileIfNeeded(projectFolder) ?? FindSingleProjectFile(projectFolder);
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
        {
            Shell.AddLog("InProShop", $"未找到 .project 文件，跳过自动导入脚本：{projectFolder}", "Warning");
            return;
        }

        // 同步最新脚本到工程目录
        var scriptTargetPath = Path.Combine(projectFolder, "inproshop_import.py");
        var scriptSourcePath = ResolveInProShopImportScriptPath();
        if (!string.IsNullOrWhiteSpace(scriptSourcePath))
        {
            try { File.Copy(scriptSourcePath, scriptTargetPath, overwrite: true); }
            catch { /* 复制失败不影响主流程 */ }
        }

        if (!File.Exists(scriptTargetPath))
        {
            Shell.AddLog("InProShop", "未找到 inproshop_import.py，跳过自动导入。", "Warning");
            return;
        }

        var confirmed = await ConfirmRunImportScriptOnUiThreadAsync();
        if (confirmed)
            await RunInProShopImportScriptAsync(projectFilePath, scriptTargetPath);
        else
            Shell.AddLog("InProShop", "用户取消，跳过执行导入脚本。", "Info");
    }

    /// <summary>在 UI 线程上弹出确认框，询问是否立即执行 InProShop 导入脚本。</summary>
    private static Task<bool> ConfirmRunImportScriptOnUiThreadAsync()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return Task.FromResult(ConfirmRunImportScriptDialog());
        }
        return dispatcher.InvokeAsync(ConfirmRunImportScriptDialog).Task;
    }

    private static bool ConfirmRunImportScriptDialog()
    {
        var result = System.Windows.MessageBox.Show(
            "是否立即运行 InProShop 导入脚本，将生成内容写入 .project 文件？\n\n" +
            "（请确保 InProShop 已关闭或处于可脚本状态）",
            "执行导入脚本",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        return result == System.Windows.MessageBoxResult.Yes;
    }

    private async Task RunInProShopImportScriptAsync(string projectFilePath, string scriptPath)
    {
        await Task.Run(() =>
        {
            var (exePath, profileArg) = ResolveInProShopCommandFromProjectAssociation();
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                Shell.AddLog("InProShop", "未找到 .project 关联的 InoProShop.exe，无法自动执行导入脚本。", "Warning");
                return;
            }

            // 把目标 .project 路径写到脚本同目录的 sidecar 文件，
            // 脚本启动后优先读这个文件来定位工程，避免同目录多 .project 时
            // 触发 "Multiple .project files found" 早退。
            try
            {
                var sidecarPath = Path.Combine(
                    Path.GetDirectoryName(scriptPath) ?? string.Empty,
                    "_import_target.txt");
                File.WriteAllText(sidecarPath, projectFilePath);
            }
            catch (Exception ex)
            {
                Shell.AddLog("InProShop", $"写入 _import_target.txt 失败：{ex.Message}", "Warning");
            }

            var argsParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(profileArg))
            {
                argsParts.Add(profileArg);
            }

            argsParts.Add($"--runscript:\"{scriptPath}\"");
            var args = string.Join(" ", argsParts);

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(projectFilePath) ?? string.Empty,
                UseShellExecute = false
            };

            Process.Start(psi);
            Shell.AddLog("InProShop", $"已执行导入脚本：{Path.GetFileName(scriptPath)}（目标工程 {Path.GetFileName(projectFilePath)}）", "Info");
        });
    }

    private void CloseRunningInProShopInstances()
    {
        foreach (var process in GetInProShopProcesses())
        {
            try
            {
                var description = $"{process.ProcessName}({process.Id})";
                if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                {
                    process.CloseMainWindow();
                    if (process.WaitForExit(8000))
                    {
                        Shell.AddLog("InProShop", $"已正常关闭 {description}", "Info");
                        continue;
                    }
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    Shell.AddLog("InProShop", $"已强制结束 {description}", "Warning");
                }
            }
            catch (Exception ex)
            {
                Shell.AddLog("InProShop", $"关闭 InoProShop 进程失败：{ex.Message}", "Warning");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static bool WaitForFileUnlocked(string filePath, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!IsFileLocked(filePath))
            {
                return true;
            }

            Thread.Sleep(500);
        }

        return !IsFileLocked(filePath);
    }

    private static bool IsFileLocked(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsInProShopRunning()
    {
        try
        {
            var processes = GetInProShopProcesses();
            try
            {
                return processes.Any();
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static List<Process> GetInProShopProcesses()
    {
        var result = new List<Process>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.ProcessName.Contains("InoProShop", StringComparison.OrdinalIgnoreCase)
                    || process.ProcessName.Contains("CODESYS", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            catch
            {
                process.Dispose();
            }
        }

        return result;
    }

    private static (string? ExePath, string? ProfileArg) ResolveInProShopCommandFromProjectAssociation()
    {
        try
        {
            using var extKey = Registry.ClassesRoot.OpenSubKey(".project");
            var progId = extKey?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(progId))
            {
                return (null, null);
            }

            using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
            var command = commandKey?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(command))
            {
                return (null, null);
            }

            var exePath = ExtractExecutablePath(command);
            var profileMatch = Regex.Match(command, @"--profile(?:=|:)(?:""(?<quoted>[^""]+)""|(?<raw>\S+))", RegexOptions.IgnoreCase);
            var profileValue = profileMatch.Success
                ? profileMatch.Groups["quoted"].Success ? profileMatch.Groups["quoted"].Value : profileMatch.Groups["raw"].Value
                : string.Empty;
            var profileArg = string.IsNullOrWhiteSpace(profileValue) ? null : $"--profile=\"{profileValue}\"";

            return (exePath, profileArg);
        }
        catch
        {
            return (null, null);
        }
    }

    private static string? ExtractExecutablePath(string command)
    {
        var trimmed = (command ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            return end > 1 ? trimmed.Substring(1, end - 1) : null;
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex >= 0 ? trimmed.Substring(0, exeIndex + 4).Trim() : null;
    }

    private string? ResolveInProShopImportScriptPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "inproshop_import.py"),
            Path.Combine(AppContext.BaseDirectory, "deploy", "inproshop_import.py"),
            Path.Combine(Shell.GetProjectRoot(), "deploy", "inproshop_import.py")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// 调用 GitPullService 把 Git 工作区里的追加内容提交并推送。
    /// </summary>
    private async Task CommitAndPushGeneratedAsync(string gitFolder)
    {
        try
        {
            var rawTemplate = (GitAutoCommitMessageTemplate ?? string.Empty).Trim();
            var op = string.IsNullOrWhiteSpace(Shell.IoOperationNumber) ? "OP" : Shell.IoOperationNumber;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var message = string.IsNullOrEmpty(rawTemplate)
                ? $"HMI 生成程序 ({op}) @ {timestamp}"
                : rawTemplate.Replace("{time}", timestamp).Replace("{op}", op);

            var options = new GitCommitAndPushOptions
            {
                WorkingFolder = gitFolder,
                RepositoryUrl = (GitRepositoryUrl ?? string.Empty).Trim(),
                Username = (GitUsername ?? string.Empty).Trim(),
                AccessToken = (GitAccessToken ?? string.Empty).Trim(),
                CommitMessage = message
            };

            var progress = new Progress<string>(line => GitPullLog += line + Environment.NewLine);
            var pushResult = await Task.Run(() => _gitPullService.CommitAndPushAsync(options, progress));

            if (pushResult.Committed && pushResult.Pushed)
            {
                Shell.SystemMessage = $"已提交并推送到远端：{message}";
                Shell.AddLog("Git", Shell.SystemMessage, "Info");
                GitPullStatus = $"生成提交已推送：{message}";
            }
            else if (pushResult.Committed)
            {
                Shell.SystemMessage = "已提交到本地，但 push 失败，请检查日志（可能权限不足或需要先 pull）。";
                Shell.AddLog("Git", Shell.SystemMessage, "Warning");
                GitPullStatus = "生成已提交到本地，push 失败。";
            }
            else
            {
                Shell.AddLog("Git", "无待提交变更，跳过 commit / push。", "Info");
            }
        }
        catch (Exception ex)
        {
            Shell.AddLog("Git", $"生成后 commit/push 失败：{ex.Message}", "Error");
            Shell.ShowPopup("提交推送失败", ex.Message, "Error");
        }
    }

    /// <summary>
    /// 使用系统默认关联程序打开 .project 文件（一般是 inoproshop）。
    /// </summary>
    private void OpenProjectFileWithDefaultProgram(string projectFilePath)
    {
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException($"未找到 .project 文件：{projectFilePath}", projectFilePath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = projectFilePath,
            UseShellExecute = true
        };
        Process.Start(psi);
        Shell.AddLog("Git", $"已用系统默认关联程序打开：{projectFilePath}", "Info");
    }
}
