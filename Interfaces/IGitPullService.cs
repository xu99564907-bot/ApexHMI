using ApexHMI.Models;
using ApexHMI.Services;

namespace ApexHMI.Interfaces;

public interface IGitPullService
{
    Task<GitPullResult> PullAsync(GitPullSettings settings, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    Task<GitCommitAndPushResult> CommitAndPushAsync(GitCommitAndPushOptions options, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    void OpenTargetFolder(string targetFolder);
}
