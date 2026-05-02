using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IDesignerProjectService
{
    Task SaveProjectAsync(string filePath, DesignerProject project);

    Task<DesignerProject?> LoadProjectAsync(string filePath);
}
