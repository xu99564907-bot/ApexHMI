using System.IO;
using System.Text.Json;
using ApexHMI.Interfaces;
using ApexHMI.Models;

namespace ApexHMI.Services;

public class DesignerProjectService : IDesignerProjectService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task SaveProjectAsync(string filePath, DesignerProject project)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, project, _jsonOptions);
    }

    public async Task<DesignerProject?> LoadProjectAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<DesignerProject>(stream, _jsonOptions);
    }
}
