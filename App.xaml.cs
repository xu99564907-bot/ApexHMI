using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ApexHMI.Services.Diagnostics;
using ApexHMI.Services.Logging;
using ApexHMI.Views;
using Serilog;

namespace ApexHMI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 日志必须最先初始化，保证后续所有异常都能被记录
        LoggingBootstrapper.Configure();
        RegisterGlobalExceptionHandlers();
        RegisterEnterCommitsBindingForTextBoxes();

        var coldStartSw = Stopwatch.StartNew();

        base.OnStartup(e);

        try
        {
            _serviceProvider = Bootstrapper.BuildServiceProvider();
            MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow.ContentRendered += (_, _) =>
            {
                Log.Information("冷启动完成 elapsedMs={ElapsedMs}", coldStartSw.ElapsedMilliseconds);
            };
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
            CrashReporter.Report(ex, "App.OnStartup");
            MessageBox.Show($"应用启动失败：{ex.Message}\r\n详情请查看 logs 目录。",
                "ApexHMI", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        LoggingBootstrapper.Shutdown();
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error(args.Exception, "[Dispatcher] 未处理异常");
            CrashReporter.Report(args.Exception, "DispatcherUnhandledException");
            try
            {
                MessageBox.Show($"发生未处理错误：{args.Exception.Message}\r\n应用将尝试继续运行，详情请查看日志。",
                    "ApexHMI", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { /* 弹窗失败也不能再抛 */ }
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "[AppDomain] 未处理异常 (IsTerminating={Terminating})", args.IsTerminating);
                CrashReporter.Report(ex, "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Log.Error(args.Exception, "[TaskScheduler] 未观察的任务异常");
            CrashReporter.Report(args.Exception, "UnobservedTaskException");
            args.SetObserved();
        };
    }

    /// <summary>
    /// 全局类处理器：所有 TextBox 按 Enter 时立即提交 Text 的 binding（UpdateSource）。
    /// 解决"输入完按回车没反应、只有切焦才生效"问题。
    /// </summary>
    private static void RegisterEnterCommitsBindingForTextBoxes()
    {
        EventManager.RegisterClassHandler(typeof(TextBox), UIElement.KeyDownEvent,
            new KeyEventHandler((sender, e) =>
            {
                if (e.Key != Key.Enter && e.Key != Key.Return) return;
                if (sender is not TextBox tb) return;
                // 多行 TextBox 的 Enter 是换行，不能提交
                if (tb.AcceptsReturn) return;
                var be = tb.GetBindingExpression(TextBox.TextProperty);
                be?.UpdateSource();
                // 顺便把焦点移走，让用户视觉上感受到"已提交"
                var scope = FocusManager.GetFocusScope(tb);
                FocusManager.SetFocusedElement(scope, null);
                Keyboard.ClearFocus();
                e.Handled = true;
            }));
    }
}
