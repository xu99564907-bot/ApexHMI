using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace ApexHMI.Services.Logging;

/// <summary>
/// 配置 Serilog 日志：滚动文件 + Console。
/// 在 App 启动最早期调用一次 <see cref="Configure"/>。
/// </summary>
public static class LoggingBootstrapper
{
    private const string OutputTemplate =
        "{Timestamp:HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj} {Exception}{NewLine}";

    private static bool _initialized;

    public static void Configure(string? logsDirectory = null)
    {
        if (_initialized) return;

        var dir = logsDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs");
        try { Directory.CreateDirectory(dir); } catch { /* 日志目录创建失败不阻塞启动 */ }

#if DEBUG
        var minLevel = LogEventLevel.Debug;
#else
        var minLevel = LogEventLevel.Information;
#endif

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.File(
                path: Path.Combine(dir, "apexhmi-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 20 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: OutputTemplate)
            .CreateLogger();

        _initialized = true;
        Log.Information("=== ApexHMI 日志系统启动 ===");
    }

    public static void Shutdown()
    {
        if (!_initialized) return;
        Log.Information("=== ApexHMI 日志系统关闭 ===");
        Log.CloseAndFlush();
        _initialized = false;
    }
}
