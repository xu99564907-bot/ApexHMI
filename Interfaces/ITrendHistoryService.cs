using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface ITrendHistoryService
{
    Task AppendAsync(string path, IEnumerable<TrendSample> samples);

    Task<List<TrendSample>> LoadAsync(string path);
}
