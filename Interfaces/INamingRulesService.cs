using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface INamingRulesService
{
    Task<NamingRulesConfig> LoadOrCreateAsync(string filePath);

    Task SaveAsync(string filePath, NamingRulesConfig config);
}
