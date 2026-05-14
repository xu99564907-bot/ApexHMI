#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using Serilog;

namespace ApexHMI.Services;

/// <summary>
/// M3.2: 默认审计实现 — 同时写入：
/// 1. 内存 OperationAuditRecord 集合（通过 mainViewModelCallback 回调）— 兼容已有 Audit 页面。
/// 2. CSV 追加日志（audit.csv，UTF-8 BOM）— 持久化兜底。
/// 不引入 SQLite 是为了：避开 Schema 迁移 + 现场可直接 Excel 打开。
/// CSV 字段：timestamp,user,action,target,success,detail
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly string _csvPath;
    private Action<string, string, string, string>? _memorySink;

    /// <summary>M3.2: 运行时注入内存 sink（由 MainWindowViewModel 在构造完后调用，避免循环依赖）。</summary>
    public void AttachMemorySink(Action<string, string, string, string> sink) => _memorySink = sink;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// <param name="csvDirectory">CSV 写入目录（如 AppContext.BaseDirectory）</param>
    /// <param name="memorySink">可选：内存审计集合写入回调（action,target,result,detail）— 由 MainViewModel.AddAudit 适配</param>
    /// </summary>
    public AuditService(string csvDirectory, Action<string, string, string, string>? memorySink = null)
    {
        Directory.CreateDirectory(csvDirectory);
        _csvPath = Path.Combine(csvDirectory, "audit.csv");
        _memorySink = memorySink;

        if (!File.Exists(_csvPath))
        {
            try
            {
                File.WriteAllText(_csvPath, "timestamp,user,action,target,success,detail\n", new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AuditService: 初始化 CSV 失败 path={Path}", _csvPath);
            }
        }
    }

    public async Task LogOperationAsync(string user, string action, string target, bool success, string? detail = null)
    {
        var line = string.Join(",",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Escape(user),
            Escape(action),
            Escape(target),
            success ? "Y" : "N",
            Escape(detail ?? string.Empty));

        // 1) 内存（同步派发到 UI 集合）
        try
        {
            _memorySink?.Invoke(action, target, success ? "成功" : "失败", detail ?? string.Empty);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "AuditService: 内存 sink 写入失败");
        }

        // 2) CSV
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            File.AppendAllText(_csvPath, line + "\n", new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AuditService: CSV 追加失败 path={Path}", _csvPath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }
}
