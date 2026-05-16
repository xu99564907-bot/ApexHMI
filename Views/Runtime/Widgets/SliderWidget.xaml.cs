using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class SliderWidget : UserControl
{
    public SliderWidget()
    {
        InitializeComponent();
    }

    private void ValueSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is SliderWidgetViewModel vm && !vm.WriteOnChange)
            vm.CommitCommand.Execute(null);
    }
}
