using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ApexHMI.ViewModels;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Pages;

public partial class DesignerView : UserControl
{
    private readonly Dictionary<ScrollViewer, double> _pageScrollOffsets = new();

    public DesignerView()
    {
        InitializeComponent();
    }

    private MainViewModel? GetShell()
    {
        return (DataContext as DesignerViewModel)?.ShellViewModel;
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

    private static T? FindDescendant<T>(DependencyObject? current) where T : DependencyObject
    {
        if (current is null)
        {
            return null;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
        {
            var child = VisualTreeHelper.GetChild(current, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void IoPreviewDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(dataGrid);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        for (var i = 0; i < 8; i++)
        {
            if (e.Delta < 0)
            {
                scrollViewer.LineDown();
            }
            else
            {
                scrollViewer.LineUp();
            }
        }

        e.Handled = true;
    }

    private void CodePreviewTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(textBox);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        for (var i = 0; i < 8; i++)
        {
            if (e.Delta < 0)
            {
                textBox.LineDown();
            }
            else
            {
                textBox.LineUp();
            }
        }

        e.Handled = true;
    }

    private void ToolboxItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (sender is FrameworkElement element && element.DataContext is string tool)
        {
            DragDrop.DoDragDrop(element, tool, DragDropEffects.Copy);
        }
    }

    private void DesignerCanvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DesignerCanvas_Drop(object sender, DragEventArgs e)
    {
        var vm = GetShell();
        if (vm is null || sender is not Canvas canvas || !e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            return;
        }

        var tool = e.Data.GetData(DataFormats.StringFormat)?.ToString();
        if (string.IsNullOrWhiteSpace(tool))
        {
            return;
        }

        var position = e.GetPosition(canvas);
        vm.AddDesignerElementAtDropCommand.Execute($"{tool}|{position.X}|{position.Y}");
    }

    private void IoPreviewHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        var vm = GetShell();
        if (vm is null)
        {
            return;
        }

        var previewGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserSortColumns = false,
            IsReadOnly = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            BorderBrush = Brushes.LightGray,
            ItemsSource = vm.IoTableRows
        };
        previewGrid.Columns.Add(new DataGridTextColumn { Header = "输入地址", Binding = new System.Windows.Data.Binding("InputAddress"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        previewGrid.Columns.Add(new DataGridTextColumn { Header = "输入注释", Binding = new System.Windows.Data.Binding("InputComment"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        previewGrid.Columns.Add(new DataGridTextColumn { Header = "输出地址", Binding = new System.Windows.Data.Binding("OutputAddress"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        previewGrid.Columns.Add(new DataGridTextColumn { Header = "输出注释", Binding = new System.Windows.Data.Binding("OutputComment"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        ShowPreviewWindow("IO 表预览", previewGrid, 1200, 760);
    }

    private void IoProgramPreviewHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        var vm = GetShell();
        if (vm is null)
        {
            return;
        }

        var previewTextBox = new TextBox
        {
            Text = vm.SelectedGeneratedIoProgramContent,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            Background = Brushes.White,
            Foreground = Brushes.Black,
            BorderBrush = Brushes.LightGray
        };

        var title = vm.SelectedGeneratedIoProgram?.DisplayName is { Length: > 0 } displayName
            ? $"程序预览 - {displayName}"
            : "程序预览";
        ShowPreviewWindow(title, previewTextBox, 1100, 760);
    }

    private void CopyCurrentIoProgramButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetShell();
        if (vm is null)
        {
            return;
        }

        var content = vm.SelectedGeneratedIoProgramContent;
        if (string.IsNullOrWhiteSpace(content))
        {
            MessageBox.Show("当前没有可复制的程序内容。", "复制程序", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(content);
        MessageBox.Show("当前程序内容已复制到剪贴板。", "复制程序", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCurrentAutoProgramButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetShell();
        if (vm is null)
        {
            return;
        }

        var content = vm.SelectedGeneratedAutoProgramContent;
        if (string.IsNullOrWhiteSpace(content))
        {
            MessageBox.Show("当前没有可复制的自动程序内容。", "复制程序", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(content);
        MessageBox.Show("当前自动程序内容已复制到剪贴板。", "复制程序", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowPreviewWindow(string title, FrameworkElement content, double width, double height)
    {
        var window = new Window
        {
            Owner = Window.GetWindow(this),
            Title = title,
            Width = width,
            Height = height,
            MinWidth = 900,
            MinHeight = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            Content = new Border
            {
                Padding = new Thickness(12),
                Background = Brushes.White,
                Child = content
            }
        };
        window.ShowDialog();
    }
}
