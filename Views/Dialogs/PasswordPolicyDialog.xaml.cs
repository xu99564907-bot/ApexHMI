#nullable enable
using System.Globalization;
using System.Windows;
using ApexHMI.Services.Security;

namespace ApexHMI.Views.Dialogs;

/// <summary>
/// M7.2: 密码策略配置 Dialog。
/// 暴露 7 字段：MinLength / RequireDigits / RequireLetters / RequireSpecial /
/// HistoryCount / MaxAgeDays / WarnDaysBeforeExpire。
/// 入口：UserViewWidget 工具栏「密码策略」按钮（仅 admin 可见）。
/// </summary>
public partial class PasswordPolicyDialog : Window
{
    private readonly PasswordPolicy _policy;

    public PasswordPolicyDialog(PasswordPolicy policy)
    {
        InitializeComponent();
        _policy = policy;
        var c = policy.Config;
        TxtMinLength.Text            = c.MinLength.ToString(CultureInfo.InvariantCulture);
        ChkRequireDigits.IsChecked   = c.RequireDigits;
        ChkRequireLetters.IsChecked  = c.RequireLetters;
        ChkRequireSpecial.IsChecked  = c.RequireSpecial;
        TxtHistoryCount.Text         = c.HistoryCount.ToString(CultureInfo.InvariantCulture);
        TxtMaxAgeDays.Text           = c.MaxAgeDays.ToString(CultureInfo.InvariantCulture);
        TxtWarnDaysBeforeExpire.Text = c.WarnDaysBeforeExpire.ToString(CultureInfo.InvariantCulture);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseNonNegativeInt(TxtMinLength.Text, out var minLen) || minLen < 1)
        {
            ShowError("最小长度必须为正整数（建议 ≥ 6）。");
            return;
        }
        if (!TryParseNonNegativeInt(TxtHistoryCount.Text, out var hist))
        {
            ShowError("历史不重复条数必须为非负整数（0 = 关闭）。");
            return;
        }
        if (!TryParseNonNegativeInt(TxtMaxAgeDays.Text, out var maxAge))
        {
            ShowError("最长有效期必须为非负整数（0 = 永不过期）。");
            return;
        }
        if (!TryParseNonNegativeInt(TxtWarnDaysBeforeExpire.Text, out var warn))
        {
            ShowError("过期前提醒天数必须为非负整数。");
            return;
        }
        if (maxAge > 0 && warn > maxAge)
        {
            ShowError("过期前提醒天数不应超过最长有效期。");
            return;
        }

        _policy.UpdateConfig(cfg =>
        {
            cfg.MinLength            = minLen;
            cfg.RequireDigits        = ChkRequireDigits.IsChecked == true;
            cfg.RequireLetters       = ChkRequireLetters.IsChecked == true;
            cfg.RequireSpecial       = ChkRequireSpecial.IsChecked == true;
            cfg.HistoryCount         = hist;
            cfg.MaxAgeDays           = maxAge;
            cfg.WarnDaysBeforeExpire = warn;
        });
        DialogResult = true;
        Close();
    }

    private static bool TryParseNonNegativeInt(string? s, out int value)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;

    private void ShowError(string msg)
        => MessageBox.Show(this, msg, "密码策略", MessageBoxButton.OK, MessageBoxImage.Warning);
}
