#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ApexHMI.Models;
using ApexHMI.Services;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    // ========== Git 仓库拉取 ==========

    [ObservableProperty] private string gitRepositoryUrl = string.Empty;

    [ObservableProperty] private string gitBranch = string.Empty;

    [ObservableProperty] private string gitTargetFolder = string.Empty;

    /// <summary>项目文件夹 / 分支名（二合一）：空则不启用子目录与自动建分支。</summary>
    [ObservableProperty] private string gitProjectFolderName = string.Empty;

    [ObservableProperty] private string gitUsername = string.Empty;

    [ObservableProperty] private string gitAccessToken = string.Empty;

    [ObservableProperty] private string gitPullStatus = "尚未执行拉取。";

    [ObservableProperty] private string gitPullLog = string.Empty;

    [ObservableProperty] private bool isGitPullRunning;

    /// <summary>生成程序时是否把生成内容追加到 Git 目录里对应的 .st 文件。</summary>
    [ObservableProperty] private bool isSyncGeneratedToGitEnabled = true;

    /// <summary>拉取代码时是否把 .project 工程文件检出到工作区；默认不拉取。</summary>
    [ObservableProperty] private bool isIncludeProjectFilesOnPullEnabled;

    /// <summary>勾选后拉取时会 reset --hard origin/branch + clean -fd，放弃本地改动。</summary>
    [ObservableProperty] private bool isForceResetLocalEnabled;

    /// <summary>勾选后若本地项目分支还没有上游，拉取完成后会执行 git push -u origin 推到远端。</summary>
    [ObservableProperty] private bool isPushProjectBranchToRemoteEnabled;

    /// <summary>勾选后"生成程序"结束会自动 git add/commit/push 把生成追加内容推到远端。</summary>
    [ObservableProperty] private bool isCommitAndPushAfterGenerateEnabled;

    /// <summary>用户自定义的 commit 消息模板；支持 {time} / {op} 占位符，留空用默认模板。</summary>
    [ObservableProperty] private string gitAutoCommitMessageTemplate = string.Empty;

    // 配置恢复期间屏蔽自动保存，避免重复写盘。
    private bool _suppressGitSettingsAutoSave;

    // 防抖：最后一次输入变化后 ~1.5 秒再落盘。
    private CancellationTokenSource? _gitSettingsAutoSaveCts;

    partial void OnIsGitPullRunningChanged(bool value)
    {
        PullGitRepositoryCommand.NotifyCanExecuteChanged();
        BrowseGitTargetFolderCommand.NotifyCanExecuteChanged();
    }

    partial void OnGitRepositoryUrlChanged(string value) => ScheduleGitSettingsAutoSave();
    partial void OnGitTargetFolderChanged(string value) => ScheduleGitSettingsAutoSave();
    partial void OnGitProjectFolderNameChanged(string value) => ScheduleGitSettingsAutoSave();
    partial void OnGitBranchChanged(string value) => ScheduleGitSettingsAutoSave();
    partial void OnGitUsernameChanged(string value) => ScheduleGitSettingsAutoSave();
    partial void OnGitAccessTokenChanged(string value) => ScheduleGitSettingsAutoSave();
    partial void OnIsSyncGeneratedToGitEnabledChanged(bool value) => ScheduleGitSettingsAutoSave();
    partial void OnIsIncludeProjectFilesOnPullEnabledChanged(bool value) => ScheduleGitSettingsAutoSave();
    partial void OnIsForceResetLocalEnabledChanged(bool value) => ScheduleGitSettingsAutoSave();
    partial void OnIsPushProjectBranchToRemoteEnabledChanged(bool value) => ScheduleGitSettingsAutoSave();
    partial void OnIsCommitAndPushAfterGenerateEnabledChanged(bool value) => ScheduleGitSettingsAutoSave();
    partial void OnGitAutoCommitMessageTemplateChanged(string value) => ScheduleGitSettingsAutoSave();

    /// <summary>
    /// 对 Git 仓库地址、保存目录等字段做防抖保存：用户输入变化 1.5s 后把最新的 Git 配置写入 appsettings.json。
    /// 这样即便没有点"拉取代码"或"生成程序"，只要填了仓库地址和目录也会默认落盘。
    /// </summary>
    private void ScheduleGitSettingsAutoSave()
    {
        if (_suppressGitSettingsAutoSave)
        {
            return;
        }

        _gitSettingsAutoSaveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _gitSettingsAutoSaveCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500), cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            try
            {
                await PersistConfigAsync(updateStatus: false);
                AddLog("Git", "Git 仓库地址与保存目录已自动写入配置文件。", "Info");
            }
            catch (Exception ex)
            {
                AddLog("Git", $"自动保存 Git 配置失败：{ex.Message}", "Warning");
            }
        });
    }

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
            SystemMessage = $"选择目录失败：{ex.Message}";
            AddLog("Git", SystemMessage, "Error");
        }
    }

    [RelayCommand(CanExecute = nameof(CanPullGitRepository))]
    private async Task PullGitRepositoryAsync()
    {
        if (IsGitPullRunning) return;

        if (string.IsNullOrWhiteSpace(GitRepositoryUrl))
        {
            ShowPopup("Git 拉取", "请先填写仓库地址。", "Warning");
            return;
        }

        if (string.IsNullOrWhiteSpace(GitTargetFolder))
        {
            ShowPopup("Git 拉取", "请先选择本地保存目录。", "Warning");
            return;
        }

        IsGitPullRunning = true;
        GitPullLog = string.Empty;
        GitPullStatus = "正在拉取代码，请稍候...";
        AddLog("Git", $"开始拉取 {GitRepositoryUrl} 到 {GitTargetFolder}", "Info");

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

        // 用 StringBuilder 累积日志，每 50 行刷一次 UI；否则 git 在大仓库里输出上千行
        // checkout/status 时，按行 "GitPullLog += line" 会在 UI 线程上造成 O(n^2) 字符串拼接 + 过度重绘，
        // 最终表现为"拉取看起来没完成"。
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

            SystemMessage = GitPullStatus;
            AddLog("Git", GitPullStatus, "Info");

            try
            {
                await PostPullPostProcessAsync(result.TargetFolder);
            }
            catch (Exception postEx)
            {
                AddLog("Git", $"拉取后置处理失败：{postEx.Message}", "Warning");
            }

            try
            {
                await PersistConfigAsync(updateStatus: false);
            }
            catch (Exception persistEx)
            {
                AddLog("Git", $"保存 Git 配置失败：{persistEx.Message}", "Warning");
            }
        }
        catch (Exception ex)
        {
            GitPullLog = logBuffer.ToString();
            GitPullStatus = $"拉取失败：{ex.Message}";
            SystemMessage = GitPullStatus;
            AddLog("Git", GitPullStatus, "Error");
            ShowPopup("Git 拉取失败", ex.Message, "Error");
        }
        finally
        {
            IsGitPullRunning = false;

            // 把这次的完整日志落盘一份，方便排查（UI 里不用再长长地复制）。
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
            catch
            {
                // 落盘失败就忽略，不影响主流程。
            }
        }
    }

    private bool CanPullGitRepository() => !IsGitPullRunning;

    [RelayCommand]
    private void OpenGitTargetFolder()
    {
        try
        {
            var effective = ResolveEffectiveGitFolder();
            var toOpen = System.IO.Directory.Exists(effective) ? effective : GitTargetFolder;
            _gitPullService.OpenTargetFolder(toOpen);
        }
        catch (Exception ex)
        {
            SystemMessage = $"打开目录失败：{ex.Message}";
            AddLog("Git", SystemMessage, "Error");
            ShowPopup("打开目录失败", ex.Message, "Error");
        }
    }

    private void RestoreGitPullSettings(GitPullSettings? settings)
    {
        settings ??= new GitPullSettings();
        _suppressGitSettingsAutoSave = true;
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
            _suppressGitSettingsAutoSave = false;
        }
    }

    /// <summary>
    /// 计算实际工作目录：若用户填了"项目文件夹 / 分支名"，返回 {TargetFolder}/{Name}，否则返回 TargetFolder。
    /// </summary>
    private string ResolveEffectiveGitFolder()
    {
        var baseFolder = (GitTargetFolder ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseFolder)) return string.Empty;

        var sub = (GitProjectFolderName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sub)) return baseFolder;

        // 剥掉非法文件名字符和分隔符，保持与 GitPullService.SanitizeProjectFolderName 一致。
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(sub.Length);
        foreach (var ch in sub)
        {
            if (ch == '/' || ch == '\\') continue;
            if (Array.IndexOf(invalid, ch) >= 0) continue;
            sb.Append(ch);
        }
        var cleaned = sb.ToString();
        if (string.IsNullOrWhiteSpace(cleaned)) return baseFolder;
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseFolder, cleaned));
    }

    private GitPullSettings BuildGitPullSettingsForConfig()
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

    /// <summary>
    /// 生成程序后不再把 artifact 追加到 Git 根目录规则路径，避免与 _exported 导入源重复维护。
    /// 该方法保留为空操作，用于兼容旧流程调用点。
    /// </summary>
    private async Task SyncGeneratedArtifactsToGitAsync(Models.IoGenerationResult result)
    {
        await Task.CompletedTask;
        AddLog("IO 生成", "已跳过 Git 根目录 .st 追加，仅保留 _exported 导入源。", "Info");
    }

    /// <summary>
    /// 生成程序后，把同一批 artifact 追加到 InProShop 脚本约定的 {ProjectName}_exported 目录，
    /// 并把导入脚本放到 .project 同目录。真正写入 .project 必须由 InProShop/CODESYS 的 scriptengine 执行。
    /// </summary>
    private async Task PrepareInProShopProjectImportAsync(Models.IoGenerationResult result)
    {
        var projectFolder = ResolveEffectiveGitFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            AddLog("InProShop", "未配置 Git 保存目录，跳过 .project 导入准备。", "Info");
            return;
        }

        if (!System.IO.Directory.Exists(projectFolder))
        {
            AddLog("InProShop", $"工程目录不存在，跳过 .project 导入准备：{projectFolder}", "Warning");
            return;
        }

        var projectFilePath = RenameProjectFileIfNeeded(projectFolder) ?? FindSingleProjectFile(projectFolder);
        if (string.IsNullOrWhiteSpace(projectFilePath) || !System.IO.File.Exists(projectFilePath))
        {
            AddLog("InProShop", $"未在工程目录找到唯一 .project 文件，跳过导入准备：{projectFolder}", "Warning");
            return;
        }

        var projectName = System.IO.Path.GetFileNameWithoutExtension(projectFilePath);
        var exportedDirectory = System.IO.Path.Combine(projectFolder, projectName + "_exported");
        var createdExportDirectory = false;
        if (!System.IO.Directory.Exists(exportedDirectory))
        {
            System.IO.Directory.CreateDirectory(exportedDirectory);
            createdExportDirectory = true;
        }

        var syncResult = await _generatedArtifactSyncService.AppendArtifactsAsync(
            result.Artifacts,
            exportedDirectory,
            IoOperationNumber);

        foreach (var entry in syncResult.Appended)
        {
            AddLog("InProShop", $"已准备导入文件 {entry.DisplayName} -> {entry.TargetPath}", "Info");
        }

        foreach (var skipped in syncResult.Skipped)
        {
            AddLog("InProShop", $"跳过导入文件 {skipped.DisplayName}（{skipped.Reason}）", "Warning");
        }

        var scriptSourcePath = ResolveInProShopImportScriptPath();
        if (!string.IsNullOrWhiteSpace(scriptSourcePath))
        {
            var scriptTargetPath = System.IO.Path.Combine(projectFolder, "inproshop_import.py");
            System.IO.File.Copy(scriptSourcePath, scriptTargetPath, overwrite: true);
            AddLog("InProShop", $"导入脚本已复制：{scriptTargetPath}", "Info");
            await RunInProShopImportScriptAsync(projectFilePath, scriptTargetPath);
        }
        else
        {
            AddLog("InProShop", "未找到 inproshop_import.py，无法复制导入脚本。", "Warning");
        }

        if (createdExportDirectory)
        {
            AddLog("InProShop", $"已创建 {projectName}_exported。建议先从 InProShop 导出一次完整工程，再执行导入脚本。", "Warning");
        }

        if (IsCommitAndPushAfterGenerateEnabled && syncResult.Appended.Count > 0)
        {
            await CommitAndPushGeneratedAsync(projectFolder);
        }

        AddLog("InProShop", $".project 导入准备完成：{System.IO.Path.GetFileName(projectFilePath)}。生成流程不会自动打开 InoProShop。", "Info");
    }

    private async Task RunInProShopImportScriptAsync(string projectFilePath, string scriptPath)
    {
        await Task.Run(() =>
        {
            var (exePath, profileArg) = ResolveInProShopCommandFromProjectAssociation();
            if (string.IsNullOrWhiteSpace(exePath) || !System.IO.File.Exists(exePath))
            {
                AddLog("InProShop", "未找到 .project 关联的 InoProShop.exe，无法自动执行导入脚本。", "Warning");
                return;
            }

            var argsParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(profileArg))
            {
                argsParts.Add(profileArg);
            }

            argsParts.Add($"--runscript:\"{scriptPath}\"");
            var args = string.Join(" ", argsParts);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = System.IO.Path.GetDirectoryName(projectFilePath) ?? string.Empty,
                UseShellExecute = false
            };

            System.Diagnostics.Process.Start(psi);
            AddLog("InProShop", $"已执行导入脚本（脚本会自行打开同目录 .project）：{System.IO.Path.GetFileName(scriptPath)}", "Info");
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
                        AddLog("InProShop", $"已正常关闭 {description}", "Info");
                        continue;
                    }
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    AddLog("InProShop", $"已强制结束 {description}", "Warning");
                }
            }
            catch (Exception ex)
            {
                AddLog("InProShop", $"关闭 InoProShop 进程失败：{ex.Message}", "Warning");
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
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
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

    private static List<System.Diagnostics.Process> GetInProShopProcesses()
    {
        var result = new List<System.Diagnostics.Process>();
        foreach (var process in System.Diagnostics.Process.GetProcesses())
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
            using var extKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(".project");
            var progId = extKey?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(progId))
            {
                return (null, null);
            }

            using var commandKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
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
            System.IO.Path.Combine(AppContext.BaseDirectory, "inproshop_import.py"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "deploy", "inproshop_import.py"),
            System.IO.Path.Combine(GetApplicationRoot(), "deploy", "inproshop_import.py")
        };

        return candidates.FirstOrDefault(System.IO.File.Exists);
    }

    /// <summary>
    /// 调用 GitPullService 把 Git 工作区里的追加内容提交并推送。
    /// 包一层异常处理：commit/push 失败不让生成流程崩溃，只在日志里给出提示。
    /// </summary>
    private async Task CommitAndPushGeneratedAsync(string gitFolder)
    {
        try
        {
            var rawTemplate = (GitAutoCommitMessageTemplate ?? string.Empty).Trim();
            var op = string.IsNullOrWhiteSpace(IoOperationNumber) ? "OP" : IoOperationNumber;
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
                SystemMessage = $"已提交并推送到远端：{message}";
                AddLog("Git", SystemMessage, "Info");
                GitPullStatus = $"生成提交已推送：{message}";
            }
            else if (pushResult.Committed)
            {
                SystemMessage = "已提交到本地，但 push 失败，请检查日志（可能权限不足或需要先 pull）。";
                AddLog("Git", SystemMessage, "Warning");
                GitPullStatus = "生成已提交到本地，push 失败。";
            }
            else
            {
                AddLog("Git", "无待提交变更，跳过 commit / push。", "Info");
            }
        }
        catch (Exception ex)
        {
            AddLog("Git", $"生成后 commit/push 失败：{ex.Message}", "Error");
            ShowPopup("提交推送失败", ex.Message, "Error");
        }
    }

    /// <summary>
    /// 拉取完成后只做 .project 文件名与 _exported 文件夹名同步；不自动打开 .project。
    /// </summary>
    private async Task PostPullPostProcessAsync(string effectiveFolder)
    {
        if (string.IsNullOrWhiteSpace(effectiveFolder) || !System.IO.Directory.Exists(effectiveFolder))
        {
            return;
        }

        string? projectFilePath = null;

        try
        {
            projectFilePath = await Task.Run(() => RenameProjectFileIfNeeded(effectiveFolder));
        }
        catch (Exception ex)
        {
            AddLog("Git", $"重命名 .project 文件失败：{ex.Message}", "Warning");
        }

        projectFilePath ??= FindSingleProjectFile(effectiveFolder);
        if (!string.IsNullOrEmpty(projectFilePath))
        {
            AddLog("Git", $"已识别 .project 文件（拉取后不自动打开）：{projectFilePath}", "Info");
        }
    }

    /// <summary>
    /// 在 effectiveFolder 根目录找 .project 文件并重命名成 {ProjectFolderName}.project。
    /// 仅在恰好有一个 .project 文件、且名称不同于目标、且目标尚不存在时执行。
    /// 返回最终的 .project 完整路径（可能是已改名后的，也可能原本就是正确名称）。
    /// </summary>
    private string? RenameProjectFileIfNeeded(string effectiveFolder)
    {
        var target = (GitProjectFolderName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            AddLog("Git", "未填写项目文件夹 / 分支名，跳过 .project 文件重命名。", "Warning");
            return FindSingleProjectFile(effectiveFolder);
        }

        // 剥掉非法字符（复用 ResolveEffectiveGitFolder 里用到的规则）。
        var invalid = System.IO.Path.GetInvalidFileNameChars();
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
            AddLog("Git", "项目文件夹 / 分支名清洗后为空，跳过 .project 文件重命名。", "Warning");
            return FindSingleProjectFile(effectiveFolder);
        }

        var projectFiles = System.IO.Directory.GetFiles(effectiveFolder, "*.project", System.IO.SearchOption.TopDirectoryOnly);
        if (projectFiles.Length == 0)
        {
            AddLog("Git", $"{effectiveFolder} 根目录未找到 .project 文件，跳过重命名。", "Info");
            return null;
        }

        var desiredName = cleaned + ".project";
        var desiredPath = System.IO.Path.Combine(effectiveFolder, desiredName);

        if (projectFiles.Length > 1)
        {
            // 有多个根级 .project，避免误改；若其中一个已经叫目标名就直接返回它，否则放弃。
            foreach (var p in projectFiles)
            {
                if (string.Equals(System.IO.Path.GetFileName(p), desiredName, StringComparison.OrdinalIgnoreCase))
                {
                    AddLog("Git", $"根目录存在多个 .project，已匹配到同名目标：{p}", "Info");
                    return p;
                }
            }
            AddLog("Git", $"{effectiveFolder} 根目录存在多个 .project 文件，无法自动重命名，请手动处理。", "Warning");
            return null;
        }

        var current = projectFiles[0];
        var currentStem = System.IO.Path.GetFileNameWithoutExtension(current);

        if (string.Equals(System.IO.Path.GetFileName(current), desiredName, StringComparison.OrdinalIgnoreCase))
        {
            // 文件名已正确，仍检查一下 _exported 文件夹是否需要同步（例如用户手动改过 .project）。
            RenameExportedFolderIfNeeded(effectiveFolder, currentStem, cleaned);
            return current;
        }

        if (System.IO.File.Exists(desiredPath))
        {
            AddLog("Git", $"目标文件已存在，跳过重命名：{desiredPath}", "Warning");
            RenameExportedFolderIfNeeded(effectiveFolder, currentStem, cleaned);
            return desiredPath;
        }

        try
        {
            System.IO.File.Move(current, desiredPath);
            AddLog("Git", $".project 已重命名：{System.IO.Path.GetFileName(current)} -> {desiredName}", "Info");
            RenameExportedFolderIfNeeded(effectiveFolder, currentStem, cleaned);
            return desiredPath;
        }
        catch (Exception ex)
        {
            AddLog("Git", $"重命名 {current} 失败：{ex.Message}", "Warning");
            return current;
        }
    }

    /// <summary>
    /// 把同目录下的 {oldStem}_exported 文件夹同步改名为 {newStem}_exported。
    /// 源不存在、目标已存在或源 == 目标时静默跳过；失败只打 Warning 日志。
    /// </summary>
    private void RenameExportedFolderIfNeeded(string effectiveFolder, string oldStem, string newStem)
    {
        if (string.IsNullOrWhiteSpace(oldStem) || string.IsNullOrWhiteSpace(newStem))
        {
            return;
        }

        const string suffix = "_exported";
        var oldFolderName = oldStem + suffix;
        var newFolderName = newStem + suffix;

        if (string.Equals(oldFolderName, newFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var oldPath = System.IO.Path.Combine(effectiveFolder, oldFolderName);
        var newPath = System.IO.Path.Combine(effectiveFolder, newFolderName);

        if (!System.IO.Directory.Exists(oldPath))
        {
            return;
        }

        if (System.IO.Directory.Exists(newPath))
        {
            AddLog("Git", $"目标文件夹已存在，跳过 _exported 重命名：{newPath}", "Warning");
            return;
        }

        try
        {
            System.IO.Directory.Move(oldPath, newPath);
            AddLog("Git", $"_exported 文件夹已重命名：{oldFolderName} -> {newFolderName}", "Info");
        }
        catch (Exception ex)
        {
            AddLog("Git", $"重命名文件夹 {oldPath} 失败：{ex.Message}", "Warning");
        }
    }

    private static string? FindSingleProjectFile(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder)) return null;
        var files = System.IO.Directory.GetFiles(folder, "*.project", System.IO.SearchOption.TopDirectoryOnly);
        return files.Length == 1 ? files[0] : null;
    }

    /// <summary>
    /// 使用系统默认关联程序打开 .project 文件（一般是 inoproshop）。未关联时 ShellExecute 会弹出"打开方式"对话框。
    /// </summary>
    private void OpenProjectFileWithDefaultProgram(string projectFilePath)
    {
        if (!System.IO.File.Exists(projectFilePath))
        {
            throw new System.IO.FileNotFoundException($"未找到 .project 文件：{projectFilePath}", projectFilePath);
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = projectFilePath,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
        AddLog("Git", $"已用系统默认关联程序打开：{projectFilePath}", "Info");
    }
}
