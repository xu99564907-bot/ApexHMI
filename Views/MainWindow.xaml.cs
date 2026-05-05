using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ApexHMI.Models;
using ApexHMI.ViewModels;
using ApexHMI.ViewModels.Shell;
using ApexHMI.Views.Common;
using ApexHMI.Views.Dialogs;

namespace ApexHMI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateWindowBoundsForState();
        EnsureWindowVisibleOnScreen();
        UpdateMaxRestoreButtonIcon();

        if (DataContext is MainViewModel vm)
        {
            vm.PopupRequested -= Vm_PopupRequested;
            vm.PopupRequested += Vm_PopupRequested;
            vm.ConfirmationRequested -= Vm_ConfirmationRequested;
            vm.ConfirmationRequested += Vm_ConfirmationRequested;
            vm.SectionJumpRequested -= Vm_SectionJumpRequested;
            vm.SectionJumpRequested += Vm_SectionJumpRequested;
            vm.HighlightRequested -= Vm_HighlightRequested;
            vm.HighlightRequested += Vm_HighlightRequested;
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateWindowBoundsForState();
        UpdateMaxRestoreButtonIcon();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PopupRequested -= Vm_PopupRequested;
            vm.ConfirmationRequested -= Vm_ConfirmationRequested;
            vm.SectionJumpRequested -= Vm_SectionJumpRequested;
            vm.HighlightRequested -= Vm_HighlightRequested;
            vm.Dispose();
        }
    }

    private void UpdateWindowBoundsForState()
    {
        if (WindowState == WindowState.Maximized)
        {
            MaxWidth = SystemParameters.PrimaryScreenWidth;
            MaxHeight = SystemParameters.PrimaryScreenHeight;
            Left = 0;
            Top = 0;
            return;
        }

        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
    }

    private void UpdateMaxRestoreButtonIcon()
    {
        if (MaximizeIcon is null || RestoreIcon is null)
        {
            return;
        }

        MaximizeIcon.Visibility = WindowState == WindowState.Maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = WindowState == WindowState.Maximized ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EnsureWindowVisibleOnScreen()
    {
        const double margin = 24;
        var workArea = SystemParameters.WorkArea;

        if (Width > workArea.Width - margin)
        {
            Width = Math.Max(1280, workArea.Width - margin);
        }

        if (Height > workArea.Height - margin)
        {
            Height = Math.Max(720, workArea.Height - margin);
        }

        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
    }

    private void Vm_PopupRequested(string title, string message, string level)
    {
        var icon = level switch
        {
            "Error" => MessageBoxImage.Error,
            "Interlock" => MessageBoxImage.Stop,
            "Warning" => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information
        };
        MessageBox.Show(this, message, title, MessageBoxButton.OK, icon);
    }

    private bool Vm_ConfirmationRequested(string title, string message)
    {
        return MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void Vm_SectionJumpRequested(string section, string? keyword)
    {
        var message = string.IsNullOrWhiteSpace(keyword)
            ? $"已跳转到：{section}"
            : $"已跳转到：{section}\n定位关键字：{keyword}";
        MessageBox.Show(this, message, "分析联动", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Vm_HighlightRequested(string targetType, string? keyword)
    {
        // 当前版本先做高亮数据联动，滚动定位保留到后续增强。
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            if (vm.SaveIoTableToSourceCommand.CanExecute(null))
            {
                vm.SaveIoTableToSourceCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // P3.4 全屏切换 (F11) / ESC 退出全屏
        if (vm is ApexHMI.ViewModels.Shell.MainWindowViewModel mvm)
        {
            if (e.Key == Key.F11)
            {
                mvm.ToggleRuntimeFullScreenCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && mvm.IsRuntimeFullScreen)
            {
                mvm.IsRuntimeFullScreen = false;
                e.Handled = true;
                return;
            }
        }
    }

    private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var scrollViewer = FindScrollableAncestor(source, e.Delta);
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollableAncestor(DependencyObject? current, int delta)
    {
        while (current is not null)
        {
            if (current is ScrollViewer scrollViewer && scrollViewer.ScrollableHeight > 0)
            {
                var canScrollUp = delta > 0 && scrollViewer.VerticalOffset > 0;
                var canScrollDown = delta < 0 && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;

                if (canScrollUp || canScrollDown)
                {
                    return scrollViewer;
                }
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        DragMove();
    }

    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaxRestoreWindowButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaxRestoreButtonIcon();
    }

    private void MaximizeWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Maximized;
        UpdateMaxRestoreButtonIcon();
    }

    private void RestoreWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Normal;
        UpdateMaxRestoreButtonIcon();
    }

    private void ToggleTopmostMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            Topmost = menuItem.IsChecked;
        }
    }


    private void ShowUsageHelpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        const string message =
            "文件 > 导入 XML 变量：导入 XML 变量表\r\n" +
            "文件 > 导入 CSV 变量：导入 CSV 变量表\r\n" +
            "文件 > 导入 IO 表：导入四列表头的 IO 地址和注释\r\n" +
            "文件 > 生成手动程序：按模板生成 IO、对象和手动程序文件\r\n" +
            "文件 > 导入流程 CSV：导入流程分析数据\r\n" +
            "窗口：运行/设计态切换、最大化、还原、置顶\r\n" +
            "帮助：查看 README 和关于信息\r\n" +
            "设计器：支持手动程序生成、自动程序生成和结果预览\r\n" +
            "监视画面：可直接浏览 OPC UA 节点并加入变量表";

        MessageBox.Show(this, message, "使用说明", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenReadmeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var readmePath = ResolveReadmePath();
        if (readmePath is null)
        {
            MessageBox.Show(this, "未找到 README.md 文件。", "帮助", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = readmePath,
            UseShellExecute = true
        });
    }

    private void ShowAboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        const string message =
            "PLC OPC UA HMI 组态设计器\n" +
            "当前版本包含 OPC UA 通讯、运行监控、设计器、报警与参数管理，" +
            "并支持内置 OPC UA 节点浏览与调试。";

        MessageBox.Show(this, message, "关于", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpcUaBrowserTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel vm && e.NewValue is OpcUaBrowseNode node && !node.IsPlaceholder)
        {
            vm.SelectedOpcUaBrowseNode = node;
        }
    }

    private async void OpcUaBrowserTreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        await AsyncEventHandler.RunSafe(async () =>
        {
            if (DataContext is not MainViewModel vm || sender is not TreeViewItem treeViewItem)
            {
                return;
            }

            if (treeViewItem.DataContext is OpcUaBrowseNode node && !node.IsPlaceholder)
            {
                await vm.ExpandOpcUaBrowserNodeCommand.ExecuteAsync(node);
            }
        }, nameof(OpcUaBrowserTreeViewItem_Expanded));
    }

    private static string? ResolveReadmePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "README.md"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "README.md"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
