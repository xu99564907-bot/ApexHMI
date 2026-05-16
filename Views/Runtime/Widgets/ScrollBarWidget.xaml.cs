using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class ScrollBarWidget : UserControl
{
    public ScrollBarWidget()
    {
        InitializeComponent();
    }

    private void ValueScrollBar_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is SliderWidgetViewModel vm && !vm.WriteOnChange)
            vm.CommitCommand.Execute(null);
    }
}
