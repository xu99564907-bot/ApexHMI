using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ApexHMI.Interfaces;
using ApexHMI.Models;

namespace ApexHMI.Services;

public class ParameterService : IParameterService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task SaveAsync(string filePath, IEnumerable<ParameterItem> parameters)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, parameters, _jsonOptions);
    }

    public async Task<List<ParameterItem>> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new List<ParameterItem>();
        }

        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<ParameterItem>>(stream, _jsonOptions) ?? new List<ParameterItem>();
    }
}
