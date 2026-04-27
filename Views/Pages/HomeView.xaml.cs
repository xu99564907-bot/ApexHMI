using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ApexHMI.ViewModels;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Pages;

public partial class HomeView : UserControl
{
    private readonly DispatcherTimer _startPressTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Dictionary<ScrollViewer, double> _pageScrollOffsets = new();
    private bool _startHandled;
    private int _startHoldTicks;

    public HomeView()
    {
        InitializeComponent();
        _startPressTimer.Tick += StartPressTimer_Tick;
    }

    private MainViewModel? GetShell()
    {
        return (DataContext as HomeViewModel)?.ShellViewModel;
    }

    private void StartPressTimer_Tick(object? sender, EventArgs e)
    {
        var vm = GetShell();
        if (vm is null)
        {
            _startPressTimer.Stop();
            return;
        }

        _startHoldTicks++;
        vm.UpdateStartHoldProgress(Math.Min(100, _startHoldTicks * 10));

        if (_startHoldTicks < 10 || _startHandled)
        {
            return;
        }

        _startHandled = true;
        _startPressTimer.Stop();
        vm.UpdateStartHoldProgress(100);
        vm.StartDeviceCommand.Execute(null);
    }

    private void StartDeviceButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startHandled = false;
        _startHoldTicks = 0;
        _startPressTimer.Stop();
        var vm = GetShell();
        vm?.UpdateStartHoldProgress(0);
        _startPressTimer.Start();
    }

    private void StartDeviceButton_PreviewMouseLeftButtonUp(object sender, RoutedEventArgs e)
    {
        _startPressTimer.Stop();
        _startHoldTicks = 0;
        var vm = GetShell();
        vm?.UpdateStartHoldProgress(0);
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
}
