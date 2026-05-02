using System;
using System.Threading.Tasks;
using System.Windows;
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

        base.OnStartup(e);

        try
        {
            _serviceProvider = Bootstrapper.BuildServiceProvider();
            MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
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
}
