using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IDesignerLayoutService
{
    Task SavePageAsync(string filePath, DesignerPage page);

    Task<DesignerPage?> LoadPageAsync(string filePath);
}
