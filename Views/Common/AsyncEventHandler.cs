using System;
using System.Threading.Tasks;
using System.Windows;
using ApexHMI.Services.Diagnostics;
using Serilog;

namespace ApexHMI.Views.Common;

/// <summary>
/// 事件处理器若必须使用 async void，统一通过此封装捕获异常并写入日志。
/// 用法（事件处理器）：
///     private async void OnClick(object s, RoutedEventArgs e)
///         => await AsyncEventHandler.RunSafe(DoWorkAsync, nameof(OnClick));
/// </summary>
public static class AsyncEventHandler
{
    /// <summary>
    /// 安全执行异步操作，捕获所有异常并记录。
    /// </summary>
    public static async Task RunSafe(Func<Task> action, string operationName, bool showUserMessage = true)
    {
        if (action is null) return;

        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "事件处理器 {Op} 抛出异常", operationName);
            CrashReporter.Report(ex, $"AsyncEventHandler:{operationName}");

            if (showUserMessage)
            {
                try
                {
                    MessageBox.Show($"操作 {operationName} 失败：{ex.Message}\r\n详情请查看日志。",
                        "ApexHMI", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { /* 弹窗失败不再抛 */ }
            }
        }
    }
}
