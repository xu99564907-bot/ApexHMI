using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.Views.Runtime;

public partial class DynamicPageHost : UserControl
{
    public DynamicPageHost()
    {
        InitializeComponent();

        // M3.4: 拦截 Tab/Shift+Tab，把焦点按全局 tabIndex 链流转
        PreviewKeyDown += OnHostPreviewKeyDown;
    }

    private void OnHostPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            TabFocusCoordinator.HandlePreviewKeyDown(this, e);
        }
    }
}
