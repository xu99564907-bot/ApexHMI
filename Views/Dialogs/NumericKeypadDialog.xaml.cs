using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApexHMI.Views.Dialogs;

/// <summary>
/// M3.3: 触屏数字键盘。
/// 用法：var dlg = new NumericKeypadDialog { InitialValue = "12.34" };
///       if (dlg.ShowDialog() == true) { var result = dlg.Result; }
/// WinCC 真实行为：数字 I/O 域获焦时弹此对话框，Esc 取消、Enter 提交。
/// </summary>
public partial class NumericKeypadDialog : Window
{
    public NumericKeypadDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => PreviewBox.Focus();
        PreviewKeyDown += OnKey;
    }

    /// <summary>对话框打开时的初值（一般是 EditText）。</summary>
    public string InitialValue
    {
        get => PreviewBox.Text;
        set => PreviewBox.Text = value ?? string.Empty;
    }

    /// <summary>用户按 Enter 提交的结果。取消时为 null。</summary>
    public string? Result { get; private set; }

    private void OnDigit(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Content is string s) PreviewBox.Text += s;
    }

    private void OnDot(object sender, RoutedEventArgs e)
    {
        if (!PreviewBox.Text.Contains('.')) PreviewBox.Text += ".";
    }

    private void OnSign(object sender, RoutedEventArgs e)
    {
        var t = PreviewBox.Text;
        PreviewBox.Text = t.StartsWith('-') ? t.Substring(1) : "-" + t;
    }

    private void OnBackspace(object sender, RoutedEventArgs e)
    {
        if (PreviewBox.Text.Length > 0)
            PreviewBox.Text = PreviewBox.Text.Substring(0, PreviewBox.Text.Length - 1);
    }

    private void OnClear(object sender, RoutedEventArgs e) => PreviewBox.Text = string.Empty;

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
