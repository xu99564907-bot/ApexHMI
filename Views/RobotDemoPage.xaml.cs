using System.Windows.Controls;
using ApexHMI.ViewModels;

namespace ApexHMI.Views;

/// <summary>
/// RobotDemoPage.xaml 的交互逻辑
/// </summary>
public partial class RobotDemoPage : Page
{
    public RobotDemoPage()
    {
        InitializeComponent();
        DataContext = new RobotDemoViewModel();
    }
}
