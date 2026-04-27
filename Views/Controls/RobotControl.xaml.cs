using System.Windows;
using System.Windows.Controls;
using ApexHMI.ViewModels;

namespace ApexHMI.Views.Controls;

/// <summary>
/// RobotControl.xaml 的交互逻辑
/// </summary>
public partial class RobotControl : UserControl
{
    public RobotControl()
    {
        InitializeComponent();
    }

    public RobotControl(RobotControlViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
