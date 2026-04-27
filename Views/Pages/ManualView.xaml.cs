using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using ApexHMI.Models;
using ApexHMI.ViewModels;
using ApexHMI.ViewModels.Modules;
using ApexHMI.Views.Dialogs;

namespace ApexHMI.Views.Pages;

public partial class ManualView : UserControl
{
    private readonly Dictionary<ScrollViewer, double> _pageScrollOffsets = new();

    public ManualView()
    {
        InitializeComponent();
    }

    private MainViewModel? GetShell()
    {
        return (DataContext as ManualViewModel)?.ShellViewModel;
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

    private void OpenCylinderSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetShell();
        if (vm is not null)
        {
            var clickedBlock = (sender as FrameworkElement)?.Tag as ManualCylinderBlockItem
                ?? (sender as FrameworkElement)?.DataContext as ManualCylinderBlockItem
                ?? FindAncestorDataContext<ManualCylinderBlockItem>(e.OriginalSource as DependencyObject)
                ?? vm.ManualCylinderBlockCards.FirstOrDefault()
                ?? vm.ManualCylinderBlocks.FirstOrDefault();

            vm.SelectedCylinderSettingsBlock = clickedBlock;
        }

        var window = new CylinderSettingsWindow
        {
            Owner = Window.GetWindow(this),
            DataContext = vm
        };
        window.ShowDialog();
    }

    private void OpenAxisSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetShell();
        if (vm is not null)
        {
            var clickedBlock = (sender as FrameworkElement)?.Tag as ManualAxisBlockItem
                ?? (sender as FrameworkElement)?.DataContext as ManualAxisBlockItem
                ?? FindAncestorDataContext<ManualAxisBlockItem>(e.OriginalSource as DependencyObject)
                ?? vm.ManualAxisBlockCards.FirstOrDefault()
                ?? vm.ManualAxisBlocks.FirstOrDefault();

            vm.SelectedAxisSettingsBlock = clickedBlock;
        }

        var window = new AxisSettingsWindow
        {
            Owner = Window.GetWindow(this),
            DataContext = vm
        };
        window.ShowDialog();
    }

    private static T? FindAncestorDataContext<T>(DependencyObject? source) where T : class
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement element && element.DataContext is T dataContext)
            {
                return dataContext;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => null
            };
        }

        return null;
    }
}
