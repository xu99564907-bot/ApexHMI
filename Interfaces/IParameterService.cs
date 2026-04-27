using System.Collections.Generic;
using System.Threading.Tasks;
using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IParameterService
{
    Task SaveAsync(string filePath, IEnumerable<ParameterItem> parameters);
    Task<List<ParameterItem>> LoadAsync(string filePath);
}
