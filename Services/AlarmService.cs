using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ApexHMI.Interfaces;
using ApexHMI.Models;

namespace ApexHMI.Services;

public class AlarmService : IAlarmService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task SaveHistoryAsync(string filePath, IEnumerable<AlarmRecord> alarms)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, alarms, _jsonOptions);
    }

    public async Task<List<AlarmRecord>> LoadHistoryAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new List<AlarmRecord>();
        }

        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<AlarmRecord>>(stream, _jsonOptions) ?? new List<AlarmRecord>();
    }
}
