using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ApexHMI.Services.Diagnostics;

/// <summary>
/// 在异常发生时写出独立的 crash dump 文件，用于离线诊断。
/// 与日志系统并行：即便日志系统未初始化也能落盘。
/// </summary>
public static class CrashReporter
{
    public static void Report(Exception ex, string? source = null)
    {
        if (ex is null) return;

        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs", "crash");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            var sb = new StringBuilder();
            sb.AppendLine("===== ApexHMI Crash Report =====");
            sb.AppendLine($"Time     : {DateTime.Now:O}");
            sb.AppendLine($"Source   : {source ?? "(unspecified)"}");
            sb.AppendLine($"OS       : {Environment.OSVersion}");
            sb.AppendLine($".NET     : {Environment.Version}");
            sb.AppendLine($"64-bit   : {Environment.Is64BitProcess}");
            sb.AppendLine($"WorkSet  : {Environment.WorkingSet / (1024 * 1024)} MB");
            sb.AppendLine($"CmdLine  : {Environment.CommandLine}");
            sb.AppendLine($"Process  : {Process.GetCurrentProcess().ProcessName} (PID {Process.GetCurrentProcess().Id})");
            sb.AppendLine();
            sb.AppendLine("===== Exception =====");
            sb.AppendLine(ex.ToString());

            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // 报告本身不能再抛异常
        }
    }
}
