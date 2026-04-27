using System.Threading.Tasks;
using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IConfigurationService
{
    Task SaveAsync(string filePath, AppConfig config);
    Task<AppConfig?> LoadAsync(string filePath);
}
