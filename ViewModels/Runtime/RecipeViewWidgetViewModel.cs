#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using Microsoft.Win32;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P8A 配方视图。挂在 ProjectDocument.Recipes 上的某个 Recipe（通过 recipeId 属性引用）。
/// <para>DataGrid 行 = RecipeField，列 = 数据集。工具栏：新建/删除数据集、读出 PLC、写入 PLC、导出 / 导入 CSV。</para>
/// <para>读 PLC：注册每个字段 TagAddress 的回调，缓存最新值。
/// 写 PLC：通过 <see cref="IWidgetDataContext.ExecuteAction"/> 调用 write-int/write-float/write-bool。</para>
/// </summary>
public partial class RecipeViewWidgetViewModel : WidgetViewModelBase
{
    /// <summary>当前订阅的 Tag 最新值缓存（用于"读出 PLC"动作）。</summary>
    private readonly Dictionary<string, string> _tagLatest = new(StringComparer.OrdinalIgnoreCase);

    public RecipeViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        RefreshRecipe();
        DesignerContext.ResourcesChanged += OnDocChanged;
    }

    private void OnDocChanged() => RefreshRecipe();

    public string RecipeId => Prop("recipeId", "");
    public bool ShowToolbar => string.Equals(Prop("showToolbar", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool AllowEditDataset => string.Equals(Prop("allowEditDataset", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowFieldDescription => string.Equals(Prop("showFieldDescription", "false"), "true", StringComparison.OrdinalIgnoreCase);
    public string Background => Prop("background", "#FFFFFF");
    public string Foreground => Prop("foreground", "#0F172A");

    /// <summary>当前指向的 Recipe（可能为 null：未配置 recipeId / 找不到 / 设计时无 Document）。</summary>
    [ObservableProperty] private Recipe? _currentRecipe;

    /// <summary>当前选中数据集（操作"删除/写PLC/读PLC"针对它）。</summary>
    [ObservableProperty] private RecipeDataset? _selectedDataset;

    /// <summary>用于 DataGrid 绑定的行视图（字段 + 各数据集的对应值代理）。</summary>
    public ObservableCollection<RecipeFieldRow> Rows { get; } = new();

    private void RefreshRecipe()
    {
        var lib = DesignerContext.Document?.Recipes;
        Recipe? r = null;
        if (lib is not null && !string.IsNullOrEmpty(RecipeId))
        {
            r = lib.Recipes.FirstOrDefault(x => string.Equals(x.Id, RecipeId, StringComparison.Ordinal));
        }
        // 兜底：未配 recipeId 且只有一个 Recipe，自动选第一个，便于演示
        if (r is null && lib is not null && lib.Recipes.Count > 0 && string.IsNullOrEmpty(RecipeId))
        {
            r = lib.Recipes[0];
        }
        CurrentRecipe = r;
        SelectedDataset = r?.Datasets.FirstOrDefault();
        RebuildRows();
        RegisterTagCallbacks();
    }

    private void RebuildRows()
    {
        Rows.Clear();
        if (CurrentRecipe is null) return;
        foreach (var f in CurrentRecipe.Fields)
        {
            Rows.Add(new RecipeFieldRow(f, CurrentRecipe));
        }
        OnPropertyChanged(nameof(Datasets));
    }

    public ObservableCollection<RecipeDataset> Datasets
        => CurrentRecipe?.Datasets ?? new ObservableCollection<RecipeDataset>();

    private void RegisterTagCallbacks()
    {
        if (CurrentRecipe is null) return;
        foreach (var f in CurrentRecipe.Fields)
        {
            if (string.IsNullOrWhiteSpace(f.TagAddress)) continue;
            var tag = f.TagAddress;
            _dataContext.RegisterValueCallback(tag, v => _tagLatest[tag] = v);
        }
    }

    // ===== 工具栏命令 =====

    [RelayCommand]
    private void AddDataset()
    {
        if (!AllowEditDataset || CurrentRecipe is null) return;
        var ds = new RecipeDataset
        {
            Name = $"数据集 {CurrentRecipe.Datasets.Count + 1}",
            ModifiedAt = DateTime.Now
        };
        // 初始化默认值
        foreach (var f in CurrentRecipe.Fields)
        {
            ds.Values[f.Key] = f.DefaultValue ?? string.Empty;
        }
        CurrentRecipe.Datasets.Add(ds);
        SelectedDataset = ds;
        RebuildRows();
    }

    [RelayCommand]
    private void RemoveDataset()
    {
        if (!AllowEditDataset || CurrentRecipe is null || SelectedDataset is null) return;
        var ok = MessageBox.Show($"确认删除数据集 \"{SelectedDataset.Name}\"？",
            "删除数据集", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        if (!ok) return;
        CurrentRecipe.Datasets.Remove(SelectedDataset);
        SelectedDataset = CurrentRecipe.Datasets.FirstOrDefault();
        RebuildRows();
    }

    [RelayCommand]
    private void ReadFromPlc()
    {
        if (CurrentRecipe is null || SelectedDataset is null)
        {
            MessageBox.Show("请先选择一个数据集。", "读出 PLC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        int n = 0;
        foreach (var f in CurrentRecipe.Fields)
        {
            if (string.IsNullOrWhiteSpace(f.TagAddress)) continue;
            if (_tagLatest.TryGetValue(f.TagAddress, out var v))
            {
                SelectedDataset.Values[f.Key] = v;
                n++;
            }
        }
        SelectedDataset.ModifiedAt = DateTime.Now;
        RebuildRows();
        MessageBox.Show($"已从 PLC 读取 {n} 个字段到数据集 \"{SelectedDataset.Name}\"。", "读出 PLC");
    }

    [RelayCommand]
    private void WriteToPlc()
    {
        if (CurrentRecipe is null || SelectedDataset is null)
        {
            MessageBox.Show("请先选择一个数据集。", "写入 PLC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var ok = MessageBox.Show(
            $"确认将数据集 \"{SelectedDataset.Name}\" 写入 PLC？此操作会修改设备参数。",
            "写入 PLC", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        if (!ok) return;

        int n = 0;
        foreach (var f in CurrentRecipe.Fields)
        {
            if (string.IsNullOrWhiteSpace(f.TagAddress)) continue;
            if (!SelectedDataset.Values.TryGetValue(f.Key, out var val)) continue;
            var action = f.Type switch
            {
                RecipeFieldType.Boolean => "write-bool",
                RecipeFieldType.Integer => "write-int",
                RecipeFieldType.Number  => "write-float",
                _ => "write-int" // 字符串暂按整数走（OPC UA 字符串写入需后续扩展）
            };
            _dataContext.ExecuteAction(action, $"{f.TagAddress}|{val}");
            n++;
        }
        MessageBox.Show($"已向 PLC 写入 {n} 个字段。", "写入 PLC");
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (CurrentRecipe is null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            FileName = $"recipe-{CurrentRecipe.Name}-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.Append("字段,显示名,类型,Tag地址,单位");
        foreach (var ds in CurrentRecipe.Datasets)
        {
            sb.Append(',').Append(Esc(ds.Name));
        }
        sb.AppendLine();

        foreach (var f in CurrentRecipe.Fields)
        {
            sb.Append(string.Join(",",
                Esc(f.Key), Esc(f.DisplayName), f.Type, Esc(f.TagAddress), Esc(f.Unit)));
            foreach (var ds in CurrentRecipe.Datasets)
            {
                sb.Append(',');
                if (ds.Values.TryGetValue(f.Key, out var v)) sb.Append(Esc(v));
            }
            sb.AppendLine();
        }
        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"已导出到 {dlg.FileName}", "导出 CSV");
    }

    [RelayCommand]
    private void ImportCsv()
    {
        if (!AllowEditDataset || CurrentRecipe is null) return;
        var dlg = new OpenFileDialog { Filter = "CSV 文件|*.csv" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var lines = File.ReadAllLines(dlg.FileName, Encoding.UTF8);
            if (lines.Length < 2) return;
            var header = SplitCsv(lines[0]);
            // 前 5 列固定（字段/显示名/类型/Tag地址/单位），其后为数据集名
            var dsNames = header.Skip(5).ToList();
            // 清空并重建数据集
            CurrentRecipe.Datasets.Clear();
            var dsList = new List<RecipeDataset>();
            foreach (var name in dsNames)
            {
                var ds = new RecipeDataset { Name = name, ModifiedAt = DateTime.Now };
                CurrentRecipe.Datasets.Add(ds);
                dsList.Add(ds);
            }
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = SplitCsv(lines[i]);
                if (cols.Count < 5) continue;
                var key = cols[0];
                for (int j = 0; j < dsList.Count && 5 + j < cols.Count; j++)
                {
                    dsList[j].Values[key] = cols[5 + j];
                }
            }
            SelectedDataset = CurrentRecipe.Datasets.FirstOrDefault();
            RebuildRows();
            MessageBox.Show($"已导入 {dsList.Count} 个数据集。", "导入 CSV");
        }
        catch (Exception ex)
        {
            MessageBox.Show("导入失败：" + ex.Message, "导入 CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string Esc(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s!.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuote)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuote = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else if (c == '"' && sb.Length == 0) inQuote = true;
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}

/// <summary>P8A DataGrid 行视图：字段 + 数据集值代理。每个数据集对应一个 DynamicColumn 通过 Values[Key]。</summary>
public partial class RecipeFieldRow : ObservableObject
{
    public RecipeFieldRow(RecipeField field, Recipe owner)
    {
        Field = field;
        Owner = owner;
    }

    public RecipeField Field { get; }
    public Recipe Owner { get; }

    public string Key => Field.Key;
    public string DisplayName => string.IsNullOrEmpty(Field.DisplayName) ? Field.Key : Field.DisplayName;
    public string Unit => Field.Unit;
    public string TagAddress => Field.TagAddress;
    public string TypeName => Field.Type.ToString();

    /// <summary>列索引取值（用于动态列绑定）。</summary>
    public string GetValue(RecipeDataset ds)
        => ds.Values.TryGetValue(Field.Key, out var v) ? v : string.Empty;

    public void SetValue(RecipeDataset ds, string value)
    {
        ds.Values[Field.Key] = value;
        ds.ModifiedAt = DateTime.Now;
    }
}
