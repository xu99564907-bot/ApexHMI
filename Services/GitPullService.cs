using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using Serilog;
using Serilog.Context;

namespace ApexHMI.Services;

/// <summary>
/// 通过系统已安装的 git.exe 实现仓库克隆与拉取。
/// 保留简单的 clone / pull 策略，便于在 IO 生成页面快速同步代码。
/// </summary>
public class GitPullService : IGitPullService
{
    public async Task<GitPullResult> PullAsync(GitPullSettings settings, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        using var _ = LogContext.PushProperty("CorrelationId", Guid.NewGuid().ToString("N"));
        var sw = Stopwatch.StartNew();
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var repositoryUrl = (settings.RepositoryUrl ?? string.Empty).Trim();
        var baseFolder = (settings.TargetFolder ?? string.Empty).Trim();
        var branch = (settings.Branch ?? string.Empty).Trim();
        var projectFolderName = SanitizeProjectFolderName(settings.ProjectFolderName);

        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            throw new InvalidOperationException("请先填写仓库地址 (Repository URL)。");
        }

        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            throw new InvalidOperationException("请先选择或填写本地保存目录。");
        }

        await EnsureGitAvailableAsync(cancellationToken);

        // 有项目文件夹名时，实际仓库落在 {baseFolder}/{projectFolderName}
        var targetFolder = string.IsNullOrWhiteSpace(projectFolderName)
            ? baseFolder
            : Path.GetFullPath(Path.Combine(baseFolder, projectFolderName));

        Directory.CreateDirectory(targetFolder);

        var authRepositoryUrl = BuildAuthenticatedUrl(repositoryUrl, settings.Username, settings.AccessToken);
        var isExistingRepo = Directory.Exists(Path.Combine(targetFolder, ".git"));
        var log = new StringBuilder();

        if (!isExistingRepo)
        {
            if (Directory.EnumerateFileSystemEntries(targetFolder).Any())
            {
                throw new InvalidOperationException($"目标目录非空且不是 Git 仓库：{targetFolder}。请选择空目录或已经初始化过的仓库目录。");
            }

            progress?.Report($"正在克隆仓库到：{targetFolder}");

            var cloneArgs = string.IsNullOrWhiteSpace(branch)
                ? $"clone --progress \"{authRepositoryUrl}\" \"{targetFolder}\""
                : $"clone --progress --branch \"{branch}\" \"{authRepositoryUrl}\" \"{targetFolder}\"";

            await RunGitAsync(cloneArgs, workingDirectory: null, log, progress, cancellationToken);
            await ConfigureProjectFileCheckoutAsync(targetFolder, settings.IncludeProjectFiles, log, progress, cancellationToken);
        }
        else
        {
            progress?.Report($"在现有仓库执行 fetch/pull：{targetFolder}");

            await RunGitAsync($"remote set-url origin \"{authRepositoryUrl}\"", targetFolder, log, progress, cancellationToken);
            await RunGitAsync("fetch --all --prune", targetFolder, log, progress, cancellationToken);
            await ConfigureProjectFileCheckoutAsync(targetFolder, settings.IncludeProjectFiles, log, progress, cancellationToken);

            // 分支为空时自动解析远端默认分支，避免 checkout 错分支导致 "Already up to date." 但本地实际看的是旧分支。
            var effectiveBranch = branch;
            if (string.IsNullOrWhiteSpace(effectiveBranch))
            {
                effectiveBranch = await TryResolveDefaultBranchAsync(targetFolder, log, progress, cancellationToken);
                if (!string.IsNullOrWhiteSpace(effectiveBranch))
                {
                    progress?.Report($"未指定分支，使用 origin 默认分支：{effectiveBranch}");
                }
            }

            if (settings.ForceResetLocal)
            {
                if (string.IsNullOrWhiteSpace(effectiveBranch))
                {
                    throw new InvalidOperationException("无法解析远端默认分支，请在界面上显式填写分支后再使用强制覆盖。");
                }

                progress?.Report($"强制覆盖本地：reset --hard origin/{effectiveBranch}");
                await RunGitAsync($"checkout \"{effectiveBranch}\"", targetFolder, log, progress, cancellationToken);
                await RunGitAsync($"reset --hard origin/{effectiveBranch}", targetFolder, log, progress, cancellationToken);
                // 清理未被跟踪的文件（比如生成时写入却未加入 .git 的内容），但保留 .gitignore 规则。
                await RunGitAsync("clean -fd", targetFolder, log, progress, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(effectiveBranch))
            {
                await RunGitAsync($"checkout \"{effectiveBranch}\"", targetFolder, log, progress, cancellationToken);
                await RunGitAsync($"pull --ff-only origin \"{effectiveBranch}\"", targetFolder, log, progress, cancellationToken);
            }
            else
            {
                await RunGitAsync("pull --ff-only", targetFolder, log, progress, cancellationToken);
            }
        }

        // 若指定了 "项目文件夹 / 分支名"，保证本地有同名分支并切过去。
        string? workingBranch = null;
        bool branchPushed = false;
        if (!string.IsNullOrWhiteSpace(projectFolderName))
        {
            var sourceBranch = string.IsNullOrWhiteSpace(branch)
                ? await TryResolveDefaultBranchAsync(targetFolder, log, progress, cancellationToken)
                : branch;

            workingBranch = await EnsureLocalBranchAsync(
                targetFolder,
                projectFolderName,
                sourceBranch,
                log,
                progress,
                cancellationToken);

            if (settings.PushProjectBranchToRemote && !string.IsNullOrWhiteSpace(workingBranch))
            {
                // 必须判断的是"远端是否已经有同名分支"，而不是"本地分支是否已有任意上游"。
                // 因为 checkout -b Team-Test1 origin/main 会把 origin/main 设成上游，但远端根本没有 Team-Test1。
                var remoteHasBranch = await RemoteBranchExistsAsync(targetFolder, workingBranch, cancellationToken);
                if (!remoteHasBranch)
                {
                    progress?.Report($"远端不存在 {workingBranch}，执行 git push -u origin {workingBranch}");
                    try
                    {
                        await RunGitAsync($"push -u origin \"{workingBranch}\"", targetFolder, log, progress, cancellationToken);
                        branchPushed = true;
                    }
                    catch (Exception ex)
                    {
                        // 推送失败不影响拉取结果：权限不够/空提交/只读令牌等情况常见，继续走流程。
                        Log.Warning(ex, "推送项目分支 {Branch} 到远端失败", workingBranch);
                        log.AppendLine($"[警告] 推送 {workingBranch} 到远端失败：{ex.Message}");
                        progress?.Report($"[警告] 推送失败：{ex.Message}");
                    }
                }
                else
                {
                    // 远端已经有同名分支。校正本地上游指向它（以前可能被设成 origin/main）。
                    progress?.Report($"远端已存在 origin/{workingBranch}，校正上游指向。");
                    try
                    {
                        await RunGitAsync($"branch --set-upstream-to=origin/{workingBranch} \"{workingBranch}\"", targetFolder, log, progress, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "设置项目分支 {Branch} 上游失败", workingBranch);
                        log.AppendLine($"[警告] 设置上游失败：{ex.Message}");
                    }
                }
            }
        }

        // 工作区健康性检查：
        //   场景：上一次把 .project 工程打开过 / 手动删过文件 / IDE 清空过目录，
        //   这时 git 看到的是一堆 "本地删除的跟踪文件"；而 pull --ff-only 不会替你恢复它们，
        //   结果 "Already up to date." 但目录里空空如也。
        //   这里做两级保护：无本地修改时自动 checkout HEAD 补齐；有本地修改时仅警告，避免覆盖用户工作。
        int deletedLocally = 0;
        int modifiedLocally = 0;
        int restoredByAutoCheckout = 0;
        if (!settings.ForceResetLocal)
        {
            deletedLocally = await CountGitOutputLinesAsync("ls-files --deleted", targetFolder, cancellationToken);
            modifiedLocally = await CountGitOutputLinesAsync("ls-files --modified", targetFolder, cancellationToken);

            // 注意：git 把"被删除的跟踪文件"同时也算进 `ls-files --modified`，所以 --modified 是 --deleted 的超集。
            // 真正的"非删除类本地修改"数量 = modified - deleted，只有它 > 0 时才算工作区有风险数据。
            int realEdits = modifiedLocally - deletedLocally;
            if (realEdits < 0) realEdits = 0;

            var diag = $"[工作区检查] ls-files --deleted={deletedLocally}，--modified={modifiedLocally}，真实修改={realEdits}";
            log.AppendLine(diag);
            progress?.Report(diag);

            if (deletedLocally > 0 && realEdits == 0)
            {
                progress?.Report($"检测到本地缺失 {deletedLocally} 个跟踪文件、无真实本地修改，自动从 HEAD 恢复...");
                try
                {
                    await RunGitAsync("checkout HEAD -- .", targetFolder, log, progress, cancellationToken);
                    restoredByAutoCheckout = deletedLocally;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "自动恢复 Git 工作区缺失文件失败");
                    log.AppendLine($"[警告] 自动恢复失败：{ex.Message}");
                    progress?.Report($"[警告] 自动恢复失败：{ex.Message}");
                }
            }
            else if (deletedLocally > 0)
            {
                var warn = $"[警告] 本地缺失 {deletedLocally} 个跟踪文件，同时有 {realEdits} 个真实本地修改，未自动恢复。如需同步，请勾选 \"强制重置本地\" 再拉一次。";
                log.AppendLine(warn);
                progress?.Report(warn);
            }
        }

        await ReportBranchStatusAsync(targetFolder, log, progress, cancellationToken);

        var headInfo = await TryReadHeadInfoAsync(targetFolder, cancellationToken);

        Log.Information("Git Pull 完成 elapsedMs={ElapsedMs} folder={Folder} freshClone={FreshClone}", sw.ElapsedMilliseconds, targetFolder, !isExistingRepo);

        return new GitPullResult
        {
            TargetFolder = targetFolder,
            IsFreshClone = !isExistingRepo,
            HeadCommit = headInfo.Commit,
            HeadSubject = headInfo.Subject,
            WorkingBranch = workingBranch ?? string.Empty,
            BranchPushed = branchPushed,
            LocalDeletedDetected = deletedLocally,
            LocalModifiedDetected = modifiedLocally,
            RestoredFileCount = restoredByAutoCheckout,
            Log = log.ToString()
        };
    }

    private static async Task ConfigureProjectFileCheckoutAsync(
        string targetFolder,
        bool includeProjectFiles,
        StringBuilder log,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (includeProjectFiles)
        {
            progress?.Report("已勾选拉取 .project 文件，关闭 sparse-checkout 限制。");
            try
            {
                await RunGitAsync("sparse-checkout disable", targetFolder, log, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "关闭 Git sparse-checkout 限制失败，继续执行");
                log.AppendLine($"[提示] sparse-checkout disable 跳过：{ex.Message}");
            }

            return;
        }

        progress?.Report("未勾选拉取 .project 文件，工作区将排除 *.project。");

        var infoDirectory = Path.Combine(targetFolder, ".git", "info");
        Directory.CreateDirectory(infoDirectory);
        var sparseCheckoutPath = Path.Combine(infoDirectory, "sparse-checkout");
        var sparseRules = string.Join(Environment.NewLine, new[]
        {
            "/*",
            "!*.project",
            "!**/*.project",
            string.Empty
        });
        await Compat.WriteAllTextAsync(sparseCheckoutPath, sparseRules, Encoding.UTF8, cancellationToken);

        await RunGitAsync("config core.sparseCheckout true", targetFolder, log, progress, cancellationToken);
        await RunGitAsync("config core.sparseCheckoutCone false", targetFolder, log, progress, cancellationToken);
        await RunGitAsync("read-tree -mu HEAD", targetFolder, log, progress, cancellationToken);
    }

    /// <summary>
    /// 执行一条 git 命令，把 stdout 按非空行计数返回；命令执行失败（非 0 退出码）返回 0。
    /// </summary>
    private static async Task<int> CountGitOutputLinesAsync(string arguments, string workingFolder, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = workingFolder
        };
        psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask);
        await Compat.WaitForExitAsync(process, cancellationToken);
        if (process.ExitCode != 0) return 0;

        int count = 0;
        foreach (var line in stdoutTask.Result.Replace("\r", string.Empty).Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line)) count++;
        }
        return count;
    }

    /// <summary>
    /// 判断远端是否已有同名分支：优先查本地 refs/remotes/origin/{branch}（fetch 时已 prune），
    /// 若找不到再回退 ls-remote 直接问远端一次，双重保险。
    /// </summary>
    private static async Task<bool> RemoteBranchExistsAsync(string targetFolder, string branch, CancellationToken cancellationToken)
    {
        if (await RunSilentAsync($"show-ref --verify --quiet \"refs/remotes/origin/{branch}\"", targetFolder, cancellationToken))
        {
            return true;
        }

        // refs/remotes/origin/<branch> 没有；再问一次远端本身，避免因为没 prune 到最新而漏判。
        return await RunSilentAsync($"ls-remote --exit-code --heads origin \"{branch}\"", targetFolder, cancellationToken);
    }

    /// <summary>
    /// 运行一条 git 命令只看退出码，不向日志/progress 吐信息。便于做判定。
    /// </summary>
    private static async Task<bool> RunSilentAsync(string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
        using var process = new Process { StartInfo = psi };
        process.Start();
        // 避免管道撑满阻塞。
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        await Compat.WaitForExitAsync(process, cancellationToken);
        return process.ExitCode == 0;
    }

    /// <summary>
    /// 清洗用户输入的项目文件夹名，去掉非法的文件名字符和分隔符；空串返回空串。
    /// </summary>
    private static string SanitizeProjectFolderName(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed)) return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (ch == '/' || ch == '\\') continue;
            if (Array.IndexOf(invalid, ch) >= 0) continue;
            buffer.Append(ch);
        }
        return buffer.ToString();
    }

    /// <summary>
    /// 保证本地存在 {localBranch}；不存在则基于 origin/{sourceBranch}（或当前 HEAD）新建，并切换到它。
    /// </summary>
    private static async Task<string> EnsureLocalBranchAsync(
        string targetFolder,
        string localBranch,
        string sourceBranch,
        StringBuilder log,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var exists = await LocalBranchExistsAsync(targetFolder, localBranch, cancellationToken);
        if (exists)
        {
            progress?.Report($"切换到本地分支：{localBranch}");
            await RunGitAsync($"checkout \"{localBranch}\"", targetFolder, log, progress, cancellationToken);
            return localBranch;
        }

        if (!string.IsNullOrWhiteSpace(sourceBranch))
        {
            progress?.Report($"基于 origin/{sourceBranch} 创建本地分支：{localBranch}");
            await RunGitAsync($"checkout -b \"{localBranch}\" \"origin/{sourceBranch}\"", targetFolder, log, progress, cancellationToken);
        }
        else
        {
            progress?.Report($"基于当前 HEAD 创建本地分支：{localBranch}");
            await RunGitAsync($"checkout -b \"{localBranch}\"", targetFolder, log, progress, cancellationToken);
        }
        return localBranch;
    }

    /// <summary>
    /// 轻量判断本地是否存在指定分支：git show-ref --verify --quiet refs/heads/{branch}（exit 0 存在）。
    /// </summary>
    private static async Task<bool> LocalBranchExistsAsync(string targetFolder, string branch, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"show-ref --verify --quiet \"refs/heads/{branch}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = targetFolder
        };
        psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = psi };
        process.Start();
        await Compat.WaitForExitAsync(process, cancellationToken);
        return process.ExitCode == 0;
    }

    /// <summary>
    /// 在指定 Git 仓库目录执行 add -A → (有变更时) commit → push。
    /// 用于"生成程序"后把追加写入的 .st 文件推送到远端。
    /// 没有实际变更时不会产生空 commit。
    /// </summary>
    public async Task<GitCommitAndPushResult> CommitAndPushAsync(
        GitCommitAndPushOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.WorkingFolder))
        {
            throw new InvalidOperationException("未指定 Git 工作目录。");
        }

        if (!Directory.Exists(Path.Combine(options.WorkingFolder, ".git")))
        {
            throw new InvalidOperationException($"目录不是 Git 仓库：{options.WorkingFolder}。请先执行拉取代码。");
        }

        await EnsureGitAvailableAsync(cancellationToken);

        var log = new StringBuilder();
        var result = new GitCommitAndPushResult { WorkingFolder = options.WorkingFolder };

        // 如果有认证信息，先把 origin URL 刷新带凭据版本，确保 push 不弹交互窗。
        if (!string.IsNullOrWhiteSpace(options.RepositoryUrl) &&
            (!string.IsNullOrWhiteSpace(options.Username) || !string.IsNullOrWhiteSpace(options.AccessToken)))
        {
            var auth = BuildAuthenticatedUrl(options.RepositoryUrl!, options.Username, options.AccessToken);
            await RunGitAsync($"remote set-url origin \"{auth}\"", options.WorkingFolder, log, progress, cancellationToken);
        }

        await RunGitAsync("add -A", options.WorkingFolder, log, progress, cancellationToken);

        // 有无变更：git diff --cached --quiet 无变更 exit 0；有变更 exit 1。
        var hasStaged = !await RunSilentAsync("diff --cached --quiet", options.WorkingFolder, cancellationToken);
        if (!hasStaged)
        {
            progress?.Report("没有待提交的变更，跳过 commit / push。");
            log.AppendLine("[info] nothing to commit.");
            result.Log = log.ToString();
            return result;
        }

        await EnsureRepositoryCommitIdentityAsync(options.WorkingFolder, log, progress, cancellationToken);

        var message = string.IsNullOrWhiteSpace(options.CommitMessage)
            ? $"HMI auto generated at {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            : options.CommitMessage!;

        await CommitWithMessageFileAsync(options.WorkingFolder, message, log, progress, cancellationToken);
        result.Committed = true;

        try
        {
            await RunGitAsync("push", options.WorkingFolder, log, progress, cancellationToken);
            result.Pushed = true;
        }
        catch (Exception ex)
        {
            // push 失败保留 commit，提示用户手动处理（可能权限/冲突）。
            Log.Warning(ex, "Git 提交后 push 失败，保留本地 commit");
            log.AppendLine($"[警告] push 失败：{ex.Message}");
            progress?.Report($"[警告] push 失败：{ex.Message}");
        }

        result.Log = log.ToString();
        return result;
    }

    private static async Task EnsureRepositoryCommitIdentityAsync(
        string workingFolder,
        StringBuilder log,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var hasUserName = await HasGitConfigValueAsync("user.name", workingFolder, cancellationToken);
        if (!hasUserName)
        {
            progress?.Report("当前仓库未配置 user.name，写入仓库级默认提交身份：ApexHMI");
            await RunGitAsync("config user.name \"ApexHMI\"", workingFolder, log, progress, cancellationToken);
        }

        var hasUserEmail = await HasGitConfigValueAsync("user.email", workingFolder, cancellationToken);
        if (!hasUserEmail)
        {
            progress?.Report("当前仓库未配置 user.email，写入仓库级默认提交邮箱：plcopcuahmi@local");
            await RunGitAsync("config user.email \"plcopcuahmi@local\"", workingFolder, log, progress, cancellationToken);
        }
    }

    private static async Task<bool> HasGitConfigValueAsync(string key, string workingFolder, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"config --get {key}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingFolder
        };
        psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await Compat.WaitForExitAsync(process, cancellationToken);

        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
    }

    public void OpenTargetFolder(string targetFolder)
    {
        if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
        {
            throw new DirectoryNotFoundException("目标目录不存在，请先执行一次拉取。");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = targetFolder,
            UseShellExecute = true
        });
    }

    private static async Task EnsureGitAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var log = new StringBuilder();
            await RunGitAsync("--version", workingDirectory: null, log, progress: null, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检测 git 命令失败");
            throw new InvalidOperationException("未检测到 git 命令。请先安装 Git for Windows 并确保 git.exe 已加入 PATH。", ex);
        }
    }

    private static string BuildAuthenticatedUrl(string url, string? username, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(username))
        {
            return url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return url;
        }

        var safeUser = Uri.EscapeDataString(string.IsNullOrWhiteSpace(username) ? "oauth2" : username!);
        var credential = string.IsNullOrWhiteSpace(token)
            ? safeUser
            : $"{safeUser}:{Uri.EscapeDataString(token!)}";

        var builder = new UriBuilder(uri)
        {
            UserName = credential,
            Password = string.Empty
        };

        var result = builder.Uri.ToString();
        // UriBuilder 会把凭据拼接成 user:pass@host；为简单起见直接返回。
        return result;
    }

    private static async Task CommitWithMessageFileAsync(
        string workingDirectory,
        string message,
        StringBuilder log,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var messageFile = Path.Combine(Path.GetTempPath(), $"ApexHMI-git-commit-{Guid.NewGuid():N}.txt");
        try
        {
            await Compat.WriteAllTextAsync(messageFile, message, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
            await RunGitAsync($"commit -F \"{messageFile}\"", workingDirectory, log, progress, cancellationToken);
        }
        finally
        {
            try
            {
                if (File.Exists(messageFile))
                {
                    File.Delete(messageFile);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "删除临时 Git 提交消息文件失败：{MessageFile}", messageFile);
                // 临时提交消息文件清理失败不影响提交结果。
            }
        }
    }


    private static async Task RunGitAsync(string arguments, string? workingDirectory, StringBuilder log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        // 避免交互式凭据弹窗卡住进程。
        psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var redactedArgs = RedactArguments(arguments);
        log.AppendLine($"> git {redactedArgs}");
        progress?.Report($"git {redactedArgs}");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            log.AppendLine(e.Data);
            progress?.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            log.AppendLine(e.Data);
            progress?.Report(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await Compat.WaitForExitAsync(process, cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {redactedArgs} 执行失败（ExitCode={process.ExitCode}）。详见日志。");
        }
    }

    private static string RedactArguments(string arguments)
    {
        // 去除 URL 中的 user:pass 片段，避免敏感信息写入日志。
        return System.Text.RegularExpressions.Regex.Replace(
            arguments,
            @"(https?://)([^/\s""]+)@",
            "$1***@");
    }

    /// <summary>
    /// 解析 origin 的默认分支（例如 main/master）。失败时返回空串。
    /// </summary>
    private static async Task<string> TryResolveDefaultBranchAsync(string targetFolder, StringBuilder log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var captured = new StringBuilder();
            await RunGitAsync("symbolic-ref --short refs/remotes/origin/HEAD", targetFolder, captured, progress: null, cancellationToken);
            var text = captured.ToString();
            foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
            {
                var trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(">", StringComparison.Ordinal)) continue;
                const string prefix = "origin/";
                if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return trimmed[prefix.Length..];
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "解析 origin 默认分支 symbolic-ref 失败，改用 remote show origin 回退");
            // 某些仓库没设 HEAD 符号链接，尝试用 remote show origin 回退。
            try
            {
                var captured = new StringBuilder();
                await RunGitAsync("remote show origin", targetFolder, captured, progress: null, cancellationToken);
                foreach (var raw in captured.ToString().Replace("\r", string.Empty).Split('\n'))
                {
                    var line = raw.Trim();
                    const string marker = "HEAD branch:";
                    if (line.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        return line[marker.Length..].Trim();
                    }
                }
            }
            catch (Exception fallbackEx)
            {
                Log.Debug(fallbackEx, "通过 remote show origin 解析默认分支失败");
                // 忽略，交给上层走 pull --ff-only 的默认行为。
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// 把"当前分支 + ahead/behind"打印到日志，帮助判断 Already up to date 是否真的同步成功。
    /// </summary>
    private static async Task ReportBranchStatusAsync(string targetFolder, StringBuilder log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        try
        {
            await RunGitAsync("rev-parse --abbrev-ref HEAD", targetFolder, log, progress, cancellationToken);
            await RunGitAsync("status -sb", targetFolder, log, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "读取 Git 分支状态诊断信息失败");
            // 纯诊断信息，失败忽略。
        }
    }

    private static async Task<(string Commit, string Subject)> TryReadHeadInfoAsync(string targetFolder, CancellationToken cancellationToken)
    {
        try
        {
            var log = new StringBuilder();
            await RunGitAsync("log -1 --pretty=format:%H%n%s", targetFolder, log, progress: null, cancellationToken);
            var text = log.ToString();
            // 取最后两行非空内容作为 commit / subject。
            var parts = text.Replace("\r", string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            string commit = string.Empty, subject = string.Empty;
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;
                if (string.IsNullOrEmpty(subject)) { subject = parts[i]; continue; }
                if (string.IsNullOrEmpty(commit)) { commit = parts[i]; break; }
            }
            return (commit, subject);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "读取 Git HEAD 信息失败");
            return (string.Empty, string.Empty);
        }
    }
}

public class GitCommitAndPushOptions
{
    public string WorkingFolder { get; set; } = string.Empty;
    public string? RepositoryUrl { get; set; }
    public string? Username { get; set; }
    public string? AccessToken { get; set; }
    public string? CommitMessage { get; set; }
}

public class GitCommitAndPushResult
{
    public string WorkingFolder { get; set; } = string.Empty;
    public bool Committed { get; set; }
    public bool Pushed { get; set; }
    public string Log { get; set; } = string.Empty;
}

public class GitPullResult
{
    public string TargetFolder { get; set; } = string.Empty;
    public bool IsFreshClone { get; set; }
    public string HeadCommit { get; set; } = string.Empty;
    public string HeadSubject { get; set; } = string.Empty;
    /// <summary>拉取结束后实际所处的本地分支（仅在提供了项目文件夹 / 分支名时非空）。</summary>
    public string WorkingBranch { get; set; } = string.Empty;
    /// <summary>本次拉取中是否把分支推送到了远端（仅当用户开启 push 且之前没有上游时为 true）。</summary>
    public bool BranchPushed { get; set; }
    /// <summary>拉取结束时"本地删除但仍被跟踪"的文件数（用于诊断工作区缺失问题）。</summary>
    public int LocalDeletedDetected { get; set; }
    /// <summary>拉取结束时"本地修改未提交"的文件数。</summary>
    public int LocalModifiedDetected { get; set; }
    /// <summary>本次自动 checkout HEAD 恢复的文件数（= 安全恢复时 LocalDeletedDetected 的副本）。</summary>
    public int RestoredFileCount { get; set; }
    public string Log { get; set; } = string.Empty;
}
