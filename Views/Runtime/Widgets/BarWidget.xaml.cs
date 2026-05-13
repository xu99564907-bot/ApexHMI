using System.ComponentModel;
using System.Windows.Controls;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class BarWidget : UserControl
{
    public BarWidget()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookVm();
        SizeChanged += (_, _) => UpdateFill();
    }

    private BarWidgetViewModel? _vm;

    private void HookVm()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = DataContext as BarWidgetViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        UpdateFill();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BarWidgetViewModel.Ratio)
                          or nameof(BarWidgetViewModel.IsVertical))
            UpdateFill();
    }

    private void UpdateFill()
    {
        if (_vm is null) return;
        var ratio = _vm.Ratio;
        if (_vm.IsVertical)
        {
            VerticalFill.Height = ActualHeight * ratio;
        }
        else
        {
            HorizontalFill.Width = ActualWidth * ratio;
        }
    }
}
