using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class ParameterItem : ObservableObject
{
    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string unit = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private UserRole minRole = UserRole.Engineer;

    [ObservableProperty]
    private bool isReadOnly = true;

    [ObservableProperty]
    private string permissionHint = "只读";

    // P3/P4: 修改追踪 — 加载 / 保存时同步 OriginalValue，IsDirty = (Value != OriginalValue)
    [ObservableProperty]
    private string originalValue = string.Empty;

    [ObservableProperty]
    private bool isDirty;

    // P8: 合法性校验 — MinValue / MaxValue 仅对数值类型生效，bool / 字符串留空跳过
    [ObservableProperty]
    private string minValue = string.Empty;

    [ObservableProperty]
    private string maxValue = string.Empty;

    [ObservableProperty]
    private bool hasValidationError;

    [ObservableProperty]
    private string validationError = string.Empty;

    // P6: 与配方对比时高亮差异行
    [ObservableProperty]
    private bool isHighlighted;

    // P5: 变更历史（保留最近 10 条）
    public ObservableCollection<ParameterChangeRecord> ChangeHistory { get; } = new();

    partial void OnValueChanged(string value)
    {
        IsDirty = !string.Equals(value, OriginalValue, StringComparison.Ordinal);
        ValidateRange();
    }

    partial void OnOriginalValueChanged(string value)
    {
        IsDirty = !string.Equals(this.value, value, StringComparison.Ordinal);
    }

    partial void OnMinValueChanged(string value) => ValidateRange();
    partial void OnMaxValueChanged(string value) => ValidateRange();

    private void ValidateRange()
    {
        if (string.IsNullOrWhiteSpace(MinValue) && string.IsNullOrWhiteSpace(MaxValue))
        {
            HasValidationError = false;
            ValidationError = string.Empty;
            return;
        }

        if (!double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            HasValidationError = true;
            ValidationError = "需要数值";
            return;
        }

        if (!string.IsNullOrWhiteSpace(MinValue)
            && double.TryParse(MinValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var min)
            && val < min)
        {
            HasValidationError = true;
            ValidationError = $"低于最小值 {MinValue}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(MaxValue)
            && double.TryParse(MaxValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var max)
            && val > max)
        {
            HasValidationError = true;
            ValidationError = $"超过最大值 {MaxValue}";
            return;
        }

        HasValidationError = false;
        ValidationError = string.Empty;
    }

    /// <summary>记录一次值变化到历史；保留最近 10 条。</summary>
    public void PushHistory(string user, string oldValue, string newValue)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        ChangeHistory.Insert(0, new ParameterChangeRecord
        {
            Timestamp = DateTime.Now,
            User = user,
            OldValue = oldValue,
            NewValue = newValue
        });

        while (ChangeHistory.Count > 10)
        {
            ChangeHistory.RemoveAt(ChangeHistory.Count - 1);
        }
    }
}

public sealed partial class ParameterCategoryChip : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isCollapsed;
}
