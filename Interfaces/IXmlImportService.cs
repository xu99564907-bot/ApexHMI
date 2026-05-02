using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IXmlImportService
{
    Task<List<TagItem>> ImportTagsAsync(string xmlFilePath);
}
