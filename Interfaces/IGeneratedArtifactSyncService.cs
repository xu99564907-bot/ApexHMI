using ApexHMI.Models;
using ApexHMI.Services;

namespace ApexHMI.Interfaces;

public interface IGeneratedArtifactSyncService
{
    Task<GeneratedArtifactSyncResult> AppendArtifactsAsync(
        IReadOnlyList<GeneratedProgramArtifact> artifacts,
        string gitRootFolder,
        string operationNumber);
}
