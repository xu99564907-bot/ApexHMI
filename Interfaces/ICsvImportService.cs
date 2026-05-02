using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface ICsvImportService
{
    Task<List<TagItem>> ImportTagsAsync(string csvFilePath);
}
