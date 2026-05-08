using System;

namespace ApexHMI.Models;

public sealed class ParameterChangeRecord
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string User { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
}
