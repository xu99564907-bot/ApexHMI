using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ApexHMI.Models;
using ApexHMI.ViewModels;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Pages;

public partial class MonitorView : UserControl
{
    private bool _isProgramTraceSelecting;
    private double _programTraceSelectionStartX;
    private readonly Dictionary<ScrollViewer, double> _pageScrollOffsets = new();

    public MonitorView()
    {
        InitializeComponent();
    }

    /// <summary>M22 程序监控 trace 图导出 PNG 截图（保存当前可视区域）。</summary>
    private void ExportProgramTracePng_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var element = ProgramTraceChartHost;
            if (element == null || element.ActualWidth < 1 || element.ActualHeight < 1)
            {
                MessageBox.Show("trace 图未渲染完成，无法导出。", "ApexHMI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG 文件|*.png",
                FileName = $"program-trace-{DateTime.Now:yyyyMMdd-HHmmss}.png"
            };
            if (dlg.ShowDialog() != true) return;
            var rtb = new RenderTargetBitmap(
                (int)Math.Ceiling(element.ActualWidth),
                (int)Math.Ceiling(element.ActualHeight),
                96, 96, PixelFormats.Pbgra32);
            rtb.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(dlg.FileName);
            encoder.Save(fs);
            var shell = GetShell();
            if (shell != null) shell.SystemMessage = $"已导出 trace PNG：{dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出 PNG 失败：{ex.Message}", "ApexHMI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private MainViewModel? GetShell()
    {
        return (DataContext as MonitorViewModel)?.ShellViewModel;
    }

    private void PageScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            _pageScrollOffsets[scrollViewer] = scrollViewer.VerticalOffset;
        }
    }

    private void PageScrollViewer_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            _pageScrollOffsets[scrollViewer] = scrollViewer.VerticalOffset;
        }
    }

    private void PageScrollViewer_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer
            || e.OriginalSource is not FrameworkElement target
            || ReferenceEquals(scrollViewer, target))
        {
            return;
        }

        var offset = _pageScrollOffsets.TryGetValue(scrollViewer, out var rememberedOffset)
            ? rememberedOffset
            : scrollViewer.VerticalOffset;

        Dispatcher.BeginInvoke(() => scrollViewer.ScrollToVerticalOffset(offset), DispatcherPriority.Background);
        e.Handled = true;
    }

    private void ProgramTraceChartHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (GetShell() is not MainViewModel vm || sender is not FrameworkElement element || element.ActualWidth <= 0)
        {
            return;
        }

        var point = e.GetPosition(element);
        var normalized = point.X / element.ActualWidth;
        vm.UpdateProgramMonitorCursor(normalized);
        UpdateProgramTraceHoverLines(element, point.X, point.Y);

        if (_isProgramTraceSelecting)
        {
            UpdateProgramTraceSelection(element, point.X);
        }
    }

    private void ProgramTraceChartHost_MouseLeave(object sender, MouseEventArgs e)
    {
        HideProgramTraceHoverLines(sender as FrameworkElement);
    }

    private void ProgramTraceChartHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (GetShell() is not MainViewModel vm || sender is not FrameworkElement element || element.ActualWidth <= 0)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            vm.ResetProgramMonitorTraceZoom();
            HideProgramTraceSelection(element);
            e.Handled = true;
            return;
        }

        var point = e.GetPosition(element);
        var normalized = point.X / element.ActualWidth;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            _isProgramTraceSelecting = true;
            _programTraceSelectionStartX = point.X;
            element.CaptureMouse();
            UpdateProgramTraceSelection(element, point.X);
            e.Handled = true;
            return;
        }

        vm.SetProgramMonitorCursorA(normalized);
        UpdateProgramTraceCursorLine(ProgramTraceCursorALine, element, point.X);
    }

    private void ProgramTraceChartHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isProgramTraceSelecting || GetShell() is not MainViewModel vm || sender is not FrameworkElement element || element.ActualWidth <= 0)
        {
            return;
        }

        var point = e.GetPosition(element);
        var startNormalized = _programTraceSelectionStartX / element.ActualWidth;
        var endNormalized = point.X / element.ActualWidth;
        HideProgramTraceSelection(element);
        element.ReleaseMouseCapture();
        _isProgramTraceSelecting = false;

        if (Math.Abs(point.X - _programTraceSelectionStartX) >= 8)
        {
            vm.ZoomProgramMonitorTraceRange(startNormalized, endNormalized);
        }

        e.Handled = true;
    }

    private void ProgramTraceChartHost_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (GetShell() is not MainViewModel vm || sender is not FrameworkElement element || element.ActualWidth <= 0)
        {
            return;
        }

        var point = e.GetPosition(element);
        var normalized = point.X / element.ActualWidth;
        vm.SetProgramMonitorCursorB(normalized);
        UpdateProgramTraceCursorLine(ProgramTraceCursorBLine, element, point.X);
        e.Handled = true;
    }

    private void ProgramTraceChartHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (GetShell() is not MainViewModel vm)
        {
            return;
        }

        vm.ZoomProgramMonitorTrace(e.Delta);
        e.Handled = true;
    }

    private static void UpdateProgramTraceCursorLine(System.Windows.Shapes.Line? line, FrameworkElement host, double x)
    {
        if (line is null)
        {
            return;
        }

        var clampedX = Math.Max(0, Math.Min(host.ActualWidth, x));
        line.Visibility = Visibility.Visible;
        line.X1 = clampedX;
        line.X2 = clampedX;
        line.Y1 = 0;
        line.Y2 = Math.Max(0, host.ActualHeight);
    }

    private void UpdateProgramTraceHoverLines(FrameworkElement host, double x, double y)
    {
        var clampedX = Math.Max(0, Math.Min(host.ActualWidth, x));
        var clampedY = Math.Max(0, Math.Min(host.ActualHeight, y));

        if (ProgramTraceHoverXLine is not null)
        {
            ProgramTraceHoverXLine.Visibility = Visibility.Visible;
            ProgramTraceHoverXLine.X1 = clampedX;
            ProgramTraceHoverXLine.X2 = clampedX;
            ProgramTraceHoverXLine.Y1 = 0;
            ProgramTraceHoverXLine.Y2 = Math.Max(0, host.ActualHeight);
        }

        if (ProgramTraceHoverYLine is not null)
        {
            ProgramTraceHoverYLine.Visibility = Visibility.Visible;
            ProgramTraceHoverYLine.X1 = 0;
            ProgramTraceHoverYLine.X2 = Math.Max(0, host.ActualWidth);
            ProgramTraceHoverYLine.Y1 = clampedY;
            ProgramTraceHoverYLine.Y2 = clampedY;
        }
    }

    private void HideProgramTraceHoverLines(FrameworkElement? host)
    {
        if (ProgramTraceHoverXLine is not null)
        {
            ProgramTraceHoverXLine.Visibility = Visibility.Collapsed;
            ProgramTraceHoverXLine.X1 = 0;
            ProgramTraceHoverXLine.X2 = 0;
            ProgramTraceHoverXLine.Y1 = 0;
            ProgramTraceHoverXLine.Y2 = Math.Max(0, host?.ActualHeight ?? 0);
        }

        if (ProgramTraceHoverYLine is not null)
        {
            ProgramTraceHoverYLine.Visibility = Visibility.Collapsed;
            ProgramTraceHoverYLine.X1 = 0;
            ProgramTraceHoverYLine.X2 = Math.Max(0, host?.ActualWidth ?? 0);
            ProgramTraceHoverYLine.Y1 = 0;
            ProgramTraceHoverYLine.Y2 = 0;
        }
    }

    private void UpdateProgramTraceSelection(FrameworkElement host, double currentX)
    {
        if (ProgramTraceSelectionRect is null)
        {
            return;
        }

        var left = Math.Max(0, Math.Min(_programTraceSelectionStartX, currentX));
        var right = Math.Min(host.ActualWidth, Math.Max(_programTraceSelectionStartX, currentX));

        ProgramTraceSelectionRect.Visibility = Visibility.Visible;
        ProgramTraceSelectionRect.Width = Math.Max(0, right - left);
        ProgramTraceSelectionRect.Height = Math.Max(0, host.ActualHeight);
        Canvas.SetLeft(ProgramTraceSelectionRect, left);
        Canvas.SetTop(ProgramTraceSelectionRect, 0);
    }

    private void HideProgramTraceSelection(FrameworkElement host)
    {
        if (ProgramTraceSelectionRect is null)
        {
            return;
        }

        ProgramTraceSelectionRect.Visibility = Visibility.Collapsed;
        ProgramTraceSelectionRect.Width = 0;
        ProgramTraceSelectionRect.Height = Math.Max(0, host.ActualHeight);
        Canvas.SetLeft(ProgramTraceSelectionRect, 0);
        Canvas.SetTop(ProgramTraceSelectionRect, 0);
    }

    private void OpcUaBrowserTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (GetShell() is MainViewModel vm && e.NewValue is OpcUaBrowseNode node && !node.IsPlaceholder)
        {
            vm.SelectedOpcUaBrowseNode = node;
        }
    }

    private async void OpcUaBrowserTreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (GetShell() is not MainViewModel vm || sender is not TreeViewItem treeViewItem)
        {
            return;
        }

        if (treeViewItem.DataContext is OpcUaBrowseNode node && !node.IsPlaceholder)
        {
            await vm.ExpandOpcUaBrowserNodeCommand.ExecuteAsync(node);
        }
    }
}
