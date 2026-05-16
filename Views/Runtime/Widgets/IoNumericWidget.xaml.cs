using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class IoNumericWidget : UserControl
{
    public IoNumericWidget()
    {
        InitializeComponent();
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is IoNumericWidgetViewModel vm)
        {
            vm.CommitCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void EditBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        // B2B: acceptOnExit 由 VM 内部判断
        if (DataContext is IoNumericWidgetViewModel vm)
        {
            vm.OnLostFocus();
        }
    }

    private void EditBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        // B2B: clearOnFocus 由 VM 内部判断
        if (DataContext is IoNumericWidgetViewModel vm)
        {
            vm.OnFocus();

            // M3.3: showTouchKeyboard=true → 弹出对应键盘（DataFormat=String 用全键盘，否则数字键盘）
            if (vm.ShowTouchKeyboard)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    string? result = null;
                    if (vm.DataFormat == "String")
                    {
                        var dlg = new ApexHMI.Views.Dialogs.AlphanumericKeyboardDialog
                        {
                            Owner = System.Windows.Window.GetWindow(this),
                            InitialValue = vm.EditText ?? string.Empty,
                        };
                        if (dlg.ShowDialog() == true) result = dlg.Result;
                    }
                    else
                    {
                        var dlg = new ApexHMI.Views.Dialogs.NumericKeypadDialog
                        {
                            Owner = System.Windows.Window.GetWindow(this),
                            InitialValue = vm.EditText ?? string.Empty,
                        };
                        if (dlg.ShowDialog() == true) result = dlg.Result;
                    }

                    if (result is not null)
                    {
                        vm.EditText = result;
                        vm.CommitCommand.Execute(null);
                    }
                    System.Windows.Input.Keyboard.ClearFocus();
                }));
            }
        }

        // B2B: editOnFocus + acceptOnFull 触发 OnTextChanged 链路放在 TextChanged 事件
    }

    private void EditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is IoNumericWidgetViewModel vm)
        {
            vm.OnTextChanged();
        }
    }
}
