#nullable enable
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class MediaPlayerWidget : UserControl
{
    public MediaPlayerWidget()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MediaPlayerWidgetViewModel vm) return;
        if (vm.IsDesignTimeView) return;
        try
        {
            if (vm.MediaUri is { } uri)
            {
                Player.Source = uri;
                if (vm.AutoPlay) Player.Play();
            }
        }
        catch { /* ignore */ }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        try { Player.Stop(); Player.Close(); } catch { /* ignore */ }
    }

    private void OnPlay(object sender, RoutedEventArgs e) => Player.Play();
    private void OnPause(object sender, RoutedEventArgs e) => Player.Pause();
    private void OnStop(object sender, RoutedEventArgs e) => Player.Stop();

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MediaPlayerWidgetViewModel { Loop: true })
        {
            Player.Position = TimeSpan.Zero;
            Player.Play();
        }
    }
}
