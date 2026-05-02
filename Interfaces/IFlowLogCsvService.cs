using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IFlowLogCsvService
{
    Task AppendAsync(string filePath, FlowStepRecord step);

    Task<List<FlowStepRecord>> LoadAsync(string filePath);
}
