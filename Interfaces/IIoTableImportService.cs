using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IIoTableImportService
{
    Task<IoTableImportResult> ImportAsync(string filePath);

    Task SaveAsync(string filePath, IEnumerable<IoTableRow> rows, IReadOnlyList<string>? headers, int encodingCodePage);
}
