using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IIoProgramGenerationService
{
    Task<IoGenerationResult> GenerateAsync(IEnumerable<IoTableRow> rows, IoGenerationSettings settings, string projectRoot);

    void OpenOutputDirectory(string outputDirectory);
}
