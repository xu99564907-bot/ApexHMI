using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApexHMI.Views.Dialogs;

/// <summary>
/// M3.3: 触屏全键盘。Shift 切换大小写；Esc/Enter 同数字键盘。
/// 用法：var dlg = new AlphanumericKeyboardDialog { InitialValue = "abc" };
///       if (dlg.ShowDialog() == true) { var s = dlg.Result; }
/// </summary>
public partial class AlphanumericKeyboardDialog : Window
{
    private bool _shift;

    private static readonly string[] LowerRows =
    {
        "1234567890-",
        "qwertyuiop",
        "asdfghjkl;",
        "zxcvbnm,./",
    };

    private static readonly string[] UpperRows =
    {
        "!@#$%^&*()_",
        "QWERTYUIOP",
        "ASDFGHJKL:",
        "ZXCVBNM<>?",
    };

    public AlphanumericKeyboardDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => { BuildKeys(); PreviewBox.Focus(); };
        PreviewKeyDown += OnKey;
    }

    public string InitialValue
    {
        get => PreviewBox.Text;
        set => PreviewBox.Text = value ?? string.Empty;
    }

    public string? Result { get; private set; }

    private void BuildKeys()
    {
        KeyRows.Items.Clear();
        var src = _shift ? UpperRows : LowerRows;
        foreach (var row in src)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var c in row)
            {
                var b = new Button { Content = c.ToString(), Style = (Style)FindResource("KeyBtn") };
                b.Click += OnChar;
                sp.Children.Add(b);
            }

            if (row == src[^1])
            {
                var space = new Button { Content = "Space", MinWidth = 200, Style = (Style)FindResource("KeyBtn") };
                space.Click += (_, _) => Insert(" ");
                sp.Children.Add(space);

                var bs = new Button { Content = "←", MinWidth = 80, Style = (Style)FindResource("KeyBtn") };
                bs.Click += (_, _) =>
                {
                    if (PreviewBox.Text.Length > 0)
                        PreviewBox.Text = PreviewBox.Text.Substring(0, PreviewBox.Text.Length - 1);
                };
                sp.Children.Add(bs);
            }

            KeyRows.Items.Add(sp);
        }
    }

    private void OnChar(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Content is string s) Insert(s);
    }

    private void Insert(string s) => PreviewBox.Text += s;

    private void OnShift(object sender, RoutedEventArgs e)
    {
        _shift = !_shift;
        BuildKeys();
    }

    private void OnEnter(object sender, RoutedEventArgs e)
    {
        Result = PreviewBox.Text;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
        Close();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter: OnEnter(this, e); e.Handled = true; break;
            case Key.Escape: OnCancel(this, e); e.Handled = true; break;
        }
    }
}
