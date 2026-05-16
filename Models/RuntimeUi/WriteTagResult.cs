using System;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// M3.2: 写 PLC 同步确认结果。WinCC 真实行为是同步等待 OPC UA Write 响应；
/// fire-and-forget 失败会让 HMI 与 PLC 状态长期不一致——本结构记录成功/失败/超时与耗时。
/// </summary>
public sealed class WriteTagResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Elapsed { get; init; }
    public bool TimedOut { get; init; }

    public static WriteTagResult Ok(TimeSpan elapsed) => new() { Success = true, Elapsed = elapsed };
    public static WriteTagResult Fail(string err, TimeSpan elapsed) => new() { Success = false, ErrorMessage = err, Elapsed = elapsed };
    public static WriteTagResult Timeout(TimeSpan elapsed) => new() { Success = false, TimedOut = true, ErrorMessage = "Write timeout", Elapsed = elapsed };
}
