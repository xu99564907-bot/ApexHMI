using System.IO;
using System.Text.Json;
using ApexHMI.Interfaces;
using ApexHMI.Models;

namespace ApexHMI.Services;

public class DesignerLayoutService : IDesignerLayoutService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task SavePageAsync(string filePath, DesignerPage page)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, page, _jsonOptions);
    }

    public async Task<DesignerPage?> LoadPageAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<DesignerPage>(stream, _jsonOptions);
    }
}
