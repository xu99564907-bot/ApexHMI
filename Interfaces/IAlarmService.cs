using System.Collections.Generic;
using System.Threading.Tasks;
using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IAlarmService
{
    Task SaveHistoryAsync(string filePath, IEnumerable<AlarmRecord> alarms);
    Task<List<AlarmRecord>> LoadHistoryAsync(string filePath);
}
