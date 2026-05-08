using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ApexHMI.Models;
using ApexHMI.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    // P1 搜索：按 HMI变量名 / PLC变量值 / 说明 过滤
    [ObservableProperty]
    private string parameterSearchText = string.Empty;

    partial void OnParameterSearchTextChanged(string value) => ParametersView.Refresh();

    // P2 分组折叠：每个 chip 表示当前子页可见的一个分组，点击切换 IsCollapsed
    public ObservableCollection<ParameterCategoryChip> ParameterCategoryChips { get; } = new();

    // P6 配方对比模式开关（仅影响行 IsHighlighted 渲染，不影响过滤）
    [ObservableProperty]
    private string parameterCompareRecipeName = string.Empty;

    [RelayCommand]
    private async Task SaveParametersAsync()
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            await shell.ParametersModule.SaveParametersAsync();
        }
    }

    [RelayCommand]
    private async Task LoadParametersAsync()
    {
        if (this is Shell.MainWindowViewModel shell)
        {
            await shell.ParametersModule.LoadParametersAsync();
        }
    }

    [RelayCommand]
    private void ToggleParameterCategoryCollapse(string? category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return;
        }

        var chip = ParameterCategoryChips.FirstOrDefault(c => string.Equals(c.Name, category, StringComparison.Ordinal));
        if (chip is null)
        {
            return;
        }

        chip.IsCollapsed = !chip.IsCollapsed;
        ParametersView.Refresh();
    }

    [RelayCommand]
    private void ShowParameterDiff()
    {
        var dirty = Parameters.Where(p => p.IsDirty).ToList();
        var dialog = new ParameterDiffDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = dirty
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ShowParameterHistory(ParameterItem? item)
    {
        if (item is null)
        {
            return;
        }

        var dialog = new ParameterHistoryDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = item
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void CompareParametersWithRecipe()
    {
        if (this is not Shell.MainWindowViewModel shell)
        {
            return;
        }

        var dialog = new ParameterRecipeCompareDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = shell.Recipe.Recipes
        };

        var result = dialog.ShowDialog() == true;
        if (!result || string.IsNullOrEmpty(dialog.SelectedRecipeName))
        {
            ClearParameterHighlights();
            return;
        }

        ParameterCompareRecipeName = dialog.SelectedRecipeName;
        var recipe = shell.Recipe.Recipes.FirstOrDefault(r => string.Equals(r.Name, dialog.SelectedRecipeName, StringComparison.Ordinal));
        if (recipe is null)
        {
            return;
        }

        foreach (var p in Parameters)
        {
            var snap = recipe.Parameters.FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
            p.IsHighlighted = snap is not null && !string.Equals(snap.Value, p.Value, StringComparison.Ordinal);
        }

        if (dialog.ApplyRecipeRequested)
        {
            ApplyRecipeToParameters(recipe);
        }
    }

    private void ApplyRecipeToParameters(RecipeItem recipe)
    {
        foreach (var p in Parameters)
        {
            var snap = recipe.Parameters.FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
            if (snap is not null && CanEditParameter(p))
            {
                p.Value = snap.Value;
            }
        }

        AddLog("参数", $"已应用配方【{recipe.Name}】到当前参数（高亮差异行已更新）", "Info");
        AddAudit("参数对比应用", recipe.Name, "成功", $"应用 {recipe.Parameters.Count} 项配方参数");
    }

    private void ClearParameterHighlights()
    {
        foreach (var p in Parameters)
        {
            p.IsHighlighted = false;
        }
        ParameterCompareRecipeName = string.Empty;
    }

    [RelayCommand]
    private void ExportParametersAsRecipe()
    {
        if (this is not Shell.MainWindowViewModel shell)
        {
            return;
        }

        var dialog = new ParameterExportRecipeDialog
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.RecipeName))
        {
            return;
        }

        var name = dialog.RecipeName.Trim();
        if (shell.Recipe.Recipes.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            ShowPopup("名称冲突", $"配方【{name}】已存在", "Warning");
            return;
        }

        var snapshot = new ObservableCollection<ParameterItem>(Parameters.Select(p => new ParameterItem
        {
            Category = p.Category,
            Name = p.Name,
            Value = p.Value,
            Unit = p.Unit,
            Description = p.Description,
            MinRole = p.MinRole,
            MinValue = p.MinValue,
            MaxValue = p.MaxValue
        }));

        var recipe = new RecipeItem
        {
            Name = name,
            ProductCode = $"EXP-{DateTime.Now:HHmmss}",
            Version = "V1.0",
            Description = "由参数页导出",
            IsActive = false,
            UpdatedAt = DateTime.Now,
            UpdatedBy = LoginUser,
            Parameters = snapshot
        };
        shell.Recipes.Add(recipe);
        shell.SelectedRecipeName = recipe.Name;
        shell.Recipe.RefreshActiveRecipeParameters();
        AddLog("参数", $"已将当前参数导出为配方【{name}】", "Info");
        AddAudit("参数导出配方", name, "成功", $"导出 {snapshot.Count} 项");
    }

    [RelayCommand]
    private void BatchEditParameters(System.Collections.IList? selectedItems)
    {
        if (selectedItems is null || selectedItems.Count == 0)
        {
            ShowPopup("批量编辑", "请先选中一行或多行参数", "Warning");
            return;
        }

        var targets = selectedItems.OfType<ParameterItem>().Where(CanEditParameter).ToList();
        if (targets.Count == 0)
        {
            ShowPopup("批量编辑", "选中行均无编辑权限", "Warning");
            return;
        }

        var dialog = new ParameterBatchEditDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = targets
        };

        if (dialog.ShowDialog() != true || dialog.NewValue is null)
        {
            return;
        }

        foreach (var p in targets)
        {
            p.Value = dialog.NewValue;
        }

        AddLog("参数", $"批量编辑：{targets.Count} 项 → {dialog.NewValue}", "Info");
        AddAudit("参数批量编辑", $"{targets.Count} 项", "成功", $"新值: {dialog.NewValue}");
    }

    private bool FilterParameterItem(object item)
    {
        if (item is not ParameterItem parameter)
        {
            return false;
        }

        // 子页面归类
        var subSectionMatch = CurrentParameterSubSection switch
        {
            "系统参数设定" => parameter.Category is "系统参数" or "联锁规则",
            "轴参数设定" => parameter.Category == "轴参数",
            "气缸参数设定" => parameter.Category == "气缸参数",
            "真空参数设定" => parameter.Category == "真空参数",
            "传感器参数设定" => parameter.Category == "传感器参数",
            _ => true
        };

        if (!subSectionMatch)
        {
            return false;
        }

        // P2 分组折叠
        var chip = ParameterCategoryChips.FirstOrDefault(c => string.Equals(c.Name, parameter.Category, StringComparison.Ordinal));
        if (chip is { IsCollapsed: true })
        {
            return false;
        }

        // P1 搜索过滤
        var keyword = ParameterSearchText?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            return true;
        }

        return Contains(parameter.Name, keyword)
            || Contains(parameter.Value, keyword)
            || Contains(parameter.Description, keyword)
            || Contains(parameter.Category, keyword);

        static bool Contains(string source, string keyword) =>
            !string.IsNullOrEmpty(source)
            && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal void RefreshParameterCategoryChips()
    {
        var visible = Parameters
            .Where(p => CurrentParameterSubSection switch
            {
                "系统参数设定" => p.Category is "系统参数" or "联锁规则",
                "轴参数设定" => p.Category == "轴参数",
                "气缸参数设定" => p.Category == "气缸参数",
                "真空参数设定" => p.Category == "真空参数",
                "传感器参数设定" => p.Category == "传感器参数",
                _ => true
            })
            .Select(p => p.Category)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        // 保留之前折叠状态
        var collapsed = ParameterCategoryChips
            .Where(c => c.IsCollapsed)
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);

        ParameterCategoryChips.Clear();
        foreach (var c in visible)
        {
            ParameterCategoryChips.Add(new ParameterCategoryChip
            {
                Name = c,
                IsCollapsed = collapsed.Contains(c)
            });
        }
    }

    private void SeedParameters()
    {
        Parameters.Add(new ParameterItem { Category = "系统参数", Name = "设备节拍", Value = "3.5", Unit = "s", Description = "设备标准节拍", MinRole = UserRole.Engineer, MinValue = "0.5", MaxValue = "60" });
        Parameters.Add(new ParameterItem { Category = "轴参数", Name = "轴速度", Value = "250", Unit = "mm/s", Description = "轴运行速度", MinRole = UserRole.Engineer, MinValue = "0", MaxValue = "1000" });
        Parameters.Add(new ParameterItem { Category = "气缸参数", Name = "气缸延时", Value = "0.2", Unit = "s", Description = "气缸动作延时", MinRole = UserRole.Engineer, MinValue = "0", MaxValue = "10" });
        Parameters.Add(new ParameterItem { Category = "真空参数", Name = "真空检测超时", Value = "1.0", Unit = "s", Description = "真空建立超时时间", MinRole = UserRole.Administrator, MinValue = "0", MaxValue = "30" });
        Parameters.Add(new ParameterItem { Category = "传感器参数", Name = "滤波时间", Value = "50", Unit = "ms", Description = "传感器滤波时间", MinRole = UserRole.Engineer, MinValue = "0", MaxValue = "1000" });
        Parameters.Add(new ParameterItem { Category = "联锁规则", Name = "自动运行允许手动气缸", Value = "false", Unit = "bool", Description = "决定自动运行时是否允许手动切换气缸", MinRole = UserRole.Administrator });
        Parameters.Add(new ParameterItem { Category = "联锁规则", Name = "自动运行允许手动挡停", Value = "false", Unit = "bool", Description = "决定自动运行时是否允许手动切换挡停", MinRole = UserRole.Administrator });
        Parameters.Add(new ParameterItem { Category = "联锁规则", Name = "机械手运行时允许复位", Value = "false", Unit = "bool", Description = "决定机械手运行中是否允许执行复位", MinRole = UserRole.Administrator });
        Parameters.Add(new ParameterItem { Category = "联锁规则", Name = "轴报警时允许运动", Value = "false", Unit = "bool", Description = "决定轴报警状态下是否允许 Jog/定位/回零", MinRole = UserRole.Administrator });

        // 把所有种子参数的 OriginalValue 同步成当前值（避免示例参数初始化即被标记为 Dirty）
        foreach (var p in Parameters)
        {
            p.OriginalValue = p.Value;
        }

        RefreshParameterCategoryChips();
    }

    public void RefreshParameterPermissions()
    {
        foreach (var parameter in Parameters)
        {
            var canEdit = CanEditParameter(parameter);
            parameter.IsReadOnly = !canEdit;
            parameter.PermissionHint = canEdit ? "可编辑" : $"{parameter.MinRole} 及以上可编辑";
        }
    }

    /// <summary>保存后调用：把每个 Dirty 参数写入 ChangeHistory，并把 OriginalValue 推进到当前值。</summary>
    internal void CommitParameterChanges()
    {
        var user = LoginUser;
        foreach (var p in Parameters.Where(x => x.IsDirty))
        {
            p.PushHistory(user, p.OriginalValue, p.Value);
            p.OriginalValue = p.Value; // 重置基线 → IsDirty 自动归 false
        }
    }

    /// <summary>校验所有可编辑参数是否合法；返回首个非法项或 null。</summary>
    internal ParameterItem? FindInvalidParameter()
    {
        return Parameters.FirstOrDefault(p => p.HasValidationError);
    }

    private void Parameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ParameterItem>())
            {
                item.PropertyChanged += (_, _) => RefreshParameterPermissions();
            }
        }

        RefreshParameterPermissions();
        RefreshParameterCategoryChips();
        ParametersView.Refresh();
    }
}
