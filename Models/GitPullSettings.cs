namespace ApexHMI.Models;

/// <summary>
/// Git 仓库拉取配置：记录仓库地址、分支、本地目录和可选的访问令牌。
/// </summary>
public class GitPullSettings
{
    public string RepositoryUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = string.Empty;

    public string TargetFolder { get; set; } = string.Empty;

    /// <summary>
    /// 项目文件夹名称 / 分支名（二合一）：
    ///  - 若非空，实际拉取目录为 {TargetFolder}/{ProjectFolderName}
    ///  - 拉取完成后自动 checkout 到同名本地分支（不存在则基于源分支创建）。
    /// </summary>
    public string ProjectFolderName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 拉取时是否检出 .project 工程文件。默认 false，避免覆盖本地正在使用的工程文件。
    /// </summary>
    public bool IncludeProjectFiles { get; set; }

    /// <summary>生成程序时是否把生成内容追加到 Git 目录里对应的 .st 文件。</summary>
    public bool SyncGeneratedToGit { get; set; } = true;

    /// <summary>
    /// 强制覆盖本地：fetch 之后执行 reset --hard origin/branch + clean -fd，让本地与远端完全一致。
    /// 会丢弃未提交的本地改动（含生成追加内容），请慎用。
    /// </summary>
    public bool ForceResetLocal { get; set; }

    /// <summary>
    /// 拉取后若本地项目分支还没有远端跟踪分支，则执行 git push -u origin {branch} 把分支推到远端。
    /// 需要仓库配有写权限并在上方填入用户名 / PAT。
    /// </summary>
    public bool PushProjectBranchToRemote { get; set; }

    /// <summary>
    /// 生成程序后自动把追加写入的 .st 文件 git add + commit + push 到远端。
    /// 只在有实际变更时提交，避免空 commit。
    /// </summary>
    public bool CommitAndPushAfterGenerate { get; set; }

    /// <summary>
    /// 生成程序后 auto commit 的消息模板，支持 {time} / {op} 占位符。留空则用默认模板。
    /// </summary>
    public string AutoCommitMessageTemplate { get; set; } = string.Empty;
}
