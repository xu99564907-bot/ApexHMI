using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Views.Dialogs;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ApexHMI.ViewModels.Modules;

public sealed class RecipeViewModel : ModuleViewModelBase
{
    private readonly IRecipeService _recipeService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RecipeViewModel(MainViewModel shell, IRecipeService recipeService)
        : base(shell, "配方管理")
    {
        _recipeService = recipeService;
        ApplyRecipeCommand = new RelayCommand<string?>(name => _ = ApplyRecipeWithPreflightAsync(name));
        CreateRecipeCommand = new RelayCommand(CreateRecipe);
        DuplicateRecipeCommand = new RelayCommand(DuplicateRecipe);
        DeleteRecipeCommand = new RelayCommand(DeleteRecipe);
        CaptureCurrentParametersToRecipeCommand = new RelayCommand(CaptureCurrentParametersToRecipe);
        LoadRecipesCommand = new AsyncRelayCommand(LoadRecipesAsync);
        SaveRecipesCommand = new AsyncRelayCommand(SaveRecipesAsync);

        // R1
        CompareRecipesCommand = new RelayCommand(CompareRecipes);
        // R2
        ImportRecipeCsvCommand = new AsyncRelayCommand(ImportRecipeCsvAsync);
        ExportRecipeCsvCommand = new AsyncRelayCommand(ExportRecipeCsvAsync);
        ImportRecipeJsonCommand = new AsyncRelayCommand(ImportRecipeJsonAsync);
        ExportRecipeJsonCommand = new AsyncRelayCommand(ExportRecipeJsonAsync);
        // R4
        SwitchRecipeWizardCommand = new RelayCommand(SwitchRecipeWizard);
        // R5
        ShowRecipeHistoryCommand = new RelayCommand<RecipeItem?>(ShowRecipeHistory);
        // R9
        StartTrialRunCommand = new RelayCommand(StartTrialRun);
    }

    public IRelayCommand<string?> ApplyRecipeCommand { get; }
    public IRelayCommand CreateRecipeCommand { get; }
    public IRelayCommand DuplicateRecipeCommand { get; }
    public IRelayCommand DeleteRecipeCommand { get; }
    public IRelayCommand CaptureCurrentParametersToRecipeCommand { get; }
    public IAsyncRelayCommand LoadRecipesCommand { get; }
    public IAsyncRelayCommand SaveRecipesCommand { get; }
    public IRelayCommand CompareRecipesCommand { get; }
    public IAsyncRelayCommand ImportRecipeCsvCommand { get; }
    public IAsyncRelayCommand ExportRecipeCsvCommand { get; }
    public IAsyncRelayCommand ImportRecipeJsonCommand { get; }
    public IAsyncRelayCommand ExportRecipeJsonCommand { get; }
    public IRelayCommand SwitchRecipeWizardCommand { get; }
    public IRelayCommand<RecipeItem?> ShowRecipeHistoryCommand { get; }
    public IRelayCommand StartTrialRunCommand { get; }

    public ObservableCollection<RecipeItem> Recipes => Shell.Recipes;
    public ObservableCollection<ParameterItem> ActiveRecipeParameters => Shell.ActiveRecipeParameters;
    public string SelectedRecipeName
    {
        get => Shell.SelectedRecipeName;
        set => Shell.SelectedRecipeName = value;
    }

    public void SeedRecipes()
    {
        Recipes.Clear();
        Recipes.Add(CreateRecipeFromCurrentParameters("产品A", "A-001", "V1.0", "标准工艺", true));
        var hi = CreateRecipeFromCurrentParameters("产品B", "B-002", "V1.1", "高速工艺", false);
        UpdateRecipeParameterValue(hi, "设备节拍", "3.0");
        UpdateRecipeParameterValue(hi, "轴速度", "320");
        UpdateRecipeParameterValue(hi, "气缸延时", "0.15");
        Recipes.Add(hi);
        var trial = CreateRecipeFromCurrentParameters("产品C", "C-003", "V2.0", "试产工艺", false);
        UpdateRecipeParameterValue(trial, "设备节拍", "4.2");
        UpdateRecipeParameterValue(trial, "真空检测超时", "1.5");
        UpdateRecipeParameterValue(trial, "滤波时间", "80");
        Recipes.Add(trial);
        Shell.SelectedRecipeName = Recipes.FirstOrDefault(r => r.IsActive)?.Name ?? "产品A";
        RefreshActiveRecipeParameters();
    }

    public async Task SaveRecipesAsync()
    {
        var path = Path.Combine(Shell.GetProjectRoot(), "config", "recipes.json");
        await _recipeService.SaveAsync(path, Recipes);
        Shell.AddLog("配方", $"配方已保存：{path}", "Info");
    }

    public async Task LoadRecipesAsync()
    {
        var path = Path.Combine(Shell.GetProjectRoot(), "config", "recipes.json");
        var items = await _recipeService.LoadAsync(path);
        if (items.Count == 0)
        {
            Shell.AddLog("配方", "未找到配方文件，保留当前示例配方", "Info");
            return;
        }

        Recipes.Clear();
        foreach (var item in items)
        {
            item.Parameters ??= new ObservableCollection<ParameterItem>();
            item.History ??= new ObservableCollection<RecipeSnapshot>();
            Recipes.Add(item);
        }

        Shell.SelectedRecipeName = Recipes.FirstOrDefault(r => r.IsActive)?.Name ?? Recipes.First().Name;
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", "配方加载完成", "Info");
    }

    /// <summary>R3 + R4: 加载到参数前先做预校验（合法性 + 兼容性）。</summary>
    private async Task ApplyRecipeWithPreflightAsync(string? recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeName)) return;
        var recipe = Recipes.FirstOrDefault(r => r.Name.Equals(recipeName, StringComparison.OrdinalIgnoreCase));
        if (recipe is null) return;

        // R3: 参数合法性
        var invalidParam = recipe.Parameters.FirstOrDefault(p => p.HasValidationError);
        if (invalidParam is not null)
        {
            Shell.ShowPopup("预校验失败", $"配方【{recipe.Name}】存在非法参数：{invalidParam.Name} - {invalidParam.ValidationError}", "Warning");
            return;
        }

        // R3: 兼容性 — 当前生产中（NgCount > 0 / Active 报警）时禁止切配方
        var hasActiveAlarm = Shell.CurrentAlarms.Any(a => a.Active && (a.Level == "Alarm" || a.Level == "Error"));
        if (hasActiveAlarm)
        {
            if (!Shell.RequestConfirmation("兼容性警告", "当前存在未恢复的高级别报警，强烈建议先排除报警再切换配方。继续切换？"))
            {
                Shell.AddLog("配方", $"配方切换被取消（存在未恢复报警）", "Warning");
                return;
            }
        }

        ApplyRecipe(recipeName);
        await Task.CompletedTask;
    }

    public void ApplyRecipe(string? recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeName)) return;
        var recipe = Recipes.FirstOrDefault(r => r.Name.Equals(recipeName, StringComparison.OrdinalIgnoreCase));
        if (recipe is null) return;

        foreach (var item in Recipes) item.IsActive = item == recipe;
        Shell.SelectedRecipeName = recipe.Name;
        ApplyRecipeParameters(recipe);
        Shell.SetTagValue("Recipe_Name", recipe.Name);
        recipe.UpdatedAt = DateTime.Now;
        recipe.UpdatedBy = Shell.LoginUser;
        recipe.LastUsedAt = DateTime.Now; // R7
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", $"已切换配方：{recipe.Name}", "Info");
        Shell.AddAudit("配方切换", recipe.Name, "成功", $"加载 {recipe.Parameters.Count} 项工艺参数");
        Shell.UpdateRuntimeVisuals();
    }

    public void CreateRecipe()
    {
        var baseName = $"新配方{Recipes.Count + 1}";
        var name = baseName;
        var index = 1;
        while (Recipes.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            name = $"{baseName}_{index}";
        }
        var recipe = CreateRecipeFromCurrentParameters(name, $"NEW-{DateTime.Now:HHmmss}", "V1.0", "从当前参数新建", false);
        Recipes.Add(recipe);
        Shell.SelectedRecipeName = recipe.Name;
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", $"已新建配方：{recipe.Name}", "Info");
        Shell.AddAudit("配方新建", recipe.Name, "成功", "基于当前参数创建");
    }

    public void DuplicateRecipe()
    {
        var source = Recipes.FirstOrDefault(r => r.Name == SelectedRecipeName);
        if (source is null) { Shell.SystemMessage = "请先选择要复制的配方"; return; }

        var name = $"{source.Name}_Copy";
        var index = 1;
        while (Recipes.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            name = $"{source.Name}_Copy{index}";
        }

        var clone = CloneRecipe(source);
        clone.Name = name;
        clone.IsActive = false;
        clone.UpdatedAt = DateTime.Now;
        clone.UpdatedBy = Shell.LoginUser;
        Recipes.Add(clone);
        Shell.SelectedRecipeName = clone.Name;
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", $"已复制配方：{source.Name} -> {clone.Name}", "Info");
        Shell.AddAudit("配方复制", clone.Name, "成功", $"来源：{source.Name}");
    }

    /// <summary>R6: 删除二次确认 + 删除原因输入。</summary>
    public void DeleteRecipe()
    {
        var recipe = Recipes.FirstOrDefault(r => r.Name == SelectedRecipeName);
        if (recipe is null) { Shell.SystemMessage = "请先选择要删除的配方"; return; }
        if (Recipes.Count <= 1)
        {
            Shell.ShowPopup("操作禁止", "至少保留一个配方，当前不能删除最后一个配方。", "Warning");
            return;
        }

        var reasonDialog = new RecipeDeleteReasonDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = recipe
        };

        if (reasonDialog.ShowDialog() != true)
        {
            return;
        }

        var reason = string.IsNullOrWhiteSpace(reasonDialog.DeleteReason) ? "未填写" : reasonDialog.DeleteReason;
        Recipes.Remove(recipe);
        var next = Recipes.First();
        next.IsActive = true;
        Shell.SelectedRecipeName = next.Name;
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", $"已删除配方：{recipe.Name}（原因：{reason}）", "Warning");
        Shell.AddAudit("配方删除", recipe.Name, "成功", $"原因：{reason}");
    }

    public void CaptureCurrentParametersToRecipe()
    {
        var recipe = Recipes.FirstOrDefault(r => r.Name == SelectedRecipeName);
        if (recipe is null) { Shell.SystemMessage = "请先选择要保存的配方"; return; }

        // R5: 覆盖前先把现配方推进 History 栈，最多保留 10 条
        var snapshot = new RecipeSnapshot
        {
            Timestamp = DateTime.Now,
            User = Shell.LoginUser,
            Description = $"覆盖前快照（{recipe.Parameters.Count} 项）",
            Parameters = CloneParameters(recipe.Parameters)
        };
        recipe.History.Insert(0, snapshot);
        while (recipe.History.Count > 10) recipe.History.RemoveAt(recipe.History.Count - 1);

        recipe.Parameters = CloneParameters(Shell.Parameters);
        recipe.UpdatedAt = DateTime.Now;
        recipe.UpdatedBy = Shell.LoginUser;
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", $"已用当前参数覆盖配方：{recipe.Name}（已留 {recipe.History.Count} 条历史快照）", "Info");
        Shell.AddAudit("配方保存", recipe.Name, "成功", $"保存 {recipe.Parameters.Count} 项参数快照");
    }

    public void RefreshActiveRecipeParameters()
    {
        ActiveRecipeParameters.Clear();
        var recipe = Recipes.FirstOrDefault(r => r.Name == SelectedRecipeName) ?? Recipes.FirstOrDefault(r => r.IsActive);
        if (recipe?.Parameters is null) return;
        foreach (var p in recipe.Parameters) ActiveRecipeParameters.Add(CloneParameter(p));
    }

    /// <summary>R1: 弹层选两个配方对比，差异表显示出来。</summary>
    private void CompareRecipes()
    {
        if (Recipes.Count < 2) { Shell.ShowPopup("配方对比", "需要至少 2 个配方才能对比", "Warning"); return; }
        var dialog = new RecipeCompareDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = Recipes
        };
        dialog.ShowDialog();
    }

    /// <summary>R2: 导入 CSV（格式：配方名,产品编码,版本,说明,参数名,参数值）。</summary>
    private async Task ImportRecipeCsvAsync()
    {
        var dialog = new OpenFileDialog { Filter = "CSV 文件|*.csv|所有文件|*.*" };
        if (dialog.ShowDialog() != true) return;

        var lines = File.ReadAllLines(dialog.FileName, Encoding.UTF8);
        if (lines.Length < 2) { Shell.ShowPopup("导入失败", "CSV 内容为空或格式不正确", "Warning"); return; }

        var grouped = lines.Skip(1)
            .Select(line => line.Split(','))
            .Where(parts => parts.Length >= 6)
            .GroupBy(parts => parts[0]);

        var added = 0;
        foreach (var g in grouped)
        {
            var name = g.Key;
            if (Recipes.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            var first = g.First();
            var recipe = new RecipeItem
            {
                Name = name,
                ProductCode = first[1],
                Version = first[2],
                Description = first[3],
                IsActive = false,
                UpdatedAt = DateTime.Now,
                UpdatedBy = Shell.LoginUser,
                Parameters = new ObservableCollection<ParameterItem>(g.Select(parts => new ParameterItem
                {
                    Name = parts[4],
                    Value = parts[5]
                }))
            };
            Recipes.Add(recipe);
            added++;
        }

        Shell.AddLog("配方", $"已从 CSV 导入 {added} 个配方", "Info");
        Shell.AddAudit("配方导入CSV", Path.GetFileName(dialog.FileName), "成功", $"新增 {added} 个");
        await Task.CompletedTask;
    }

    private async Task ExportRecipeCsvAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            FileName = $"recipes-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("配方名,产品编码,版本,说明,参数名,参数值");
        foreach (var r in Recipes)
        {
            foreach (var p in r.Parameters)
            {
                sb.AppendLine($"{r.Name},{r.ProductCode},{r.Version},{r.Description},{p.Name},{p.Value}");
            }
        }
        await Compat.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
        Shell.SystemMessage = $"已导出 {Recipes.Count} 个配方到 CSV：{dialog.FileName}";
        Shell.AddLog("配方", Shell.SystemMessage, "Info");
    }

    private async Task ImportRecipeJsonAsync()
    {
        var dialog = new OpenFileDialog { Filter = "JSON 文件|*.json" };
        if (dialog.ShowDialog() != true) return;

        var json = await Compat.ReadAllTextAsync(dialog.FileName);
        var items = JsonSerializer.Deserialize<List<RecipeItem>>(json, JsonOptions);
        if (items is null || items.Count == 0) { Shell.ShowPopup("导入失败", "JSON 内容为空", "Warning"); return; }

        var added = 0;
        foreach (var item in items)
        {
            if (Recipes.Any(r => r.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase))) continue;
            item.Parameters ??= new ObservableCollection<ParameterItem>();
            item.History ??= new ObservableCollection<RecipeSnapshot>();
            item.IsActive = false;
            Recipes.Add(item);
            added++;
        }
        Shell.AddLog("配方", $"已从 JSON 导入 {added} 个配方", "Info");
        Shell.AddAudit("配方导入JSON", Path.GetFileName(dialog.FileName), "成功", $"新增 {added} 个");
    }

    private async Task ExportRecipeJsonAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON 文件|*.json",
            FileName = $"recipes-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog() != true) return;
        var json = JsonSerializer.Serialize(Recipes, JsonOptions);
        await Compat.WriteAllTextAsync(dialog.FileName, json, Encoding.UTF8);
        Shell.SystemMessage = $"已导出 {Recipes.Count} 个配方到 JSON：{dialog.FileName}";
        Shell.AddLog("配方", Shell.SystemMessage, "Info");
    }

    /// <summary>R4: 切换配方流程化向导（停机 → 切换 → 启动 三步确认）。</summary>
    private void SwitchRecipeWizard()
    {
        var name = SelectedRecipeName;
        if (string.IsNullOrWhiteSpace(name)) { Shell.ShowPopup("切换向导", "请先在下拉中选择目标配方", "Warning"); return; }

        if (!Shell.RequestConfirmation("Step 1/3 停机", "向导将先停机，确认设备已可安全停机？")) return;
        Shell.SetTagValue("Device_Start", "False");
        Shell.AddLog("配方", "Step 1/3 完成：设备已停机", "Info");

        if (!Shell.RequestConfirmation("Step 2/3 切换", $"现在将切换到配方【{name}】，确认？")) return;
        ApplyRecipe(name);
        Shell.AddLog("配方", "Step 2/3 完成：配方切换", "Info");

        if (!Shell.RequestConfirmation("Step 3/3 启动", "确认启动设备？")) return;
        Shell.SetTagValue("Device_Start", "True");
        Shell.AddLog("配方", "Step 3/3 完成：设备已启动，向导结束", "Info");
        Shell.AddAudit("配方切换向导", name, "成功", "三步流程化完成");
    }

    /// <summary>R5: 显示配方历史版本列表，可回滚。</summary>
    private void ShowRecipeHistory(RecipeItem? recipe)
    {
        recipe ??= Recipes.FirstOrDefault(r => r.Name == SelectedRecipeName);
        if (recipe is null) return;

        var dialog = new RecipeHistoryDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = recipe
        };

        if (dialog.ShowDialog() == true && dialog.RestoreSnapshot is not null)
        {
            // 回滚：把当前 Parameters 推一条 history，再用快照覆盖
            recipe.History.Insert(0, new RecipeSnapshot
            {
                Timestamp = DateTime.Now,
                User = Shell.LoginUser,
                Description = "回滚前自动快照",
                Parameters = CloneParameters(recipe.Parameters)
            });
            recipe.Parameters = CloneParameters(dialog.RestoreSnapshot.Parameters);
            recipe.UpdatedAt = DateTime.Now;
            recipe.UpdatedBy = Shell.LoginUser;
            RefreshActiveRecipeParameters();
            Shell.AddLog("配方", $"已回滚配方【{recipe.Name}】到 {dialog.RestoreSnapshot.Timestamp:yyyy-MM-dd HH:mm:ss} 版本", "Info");
            Shell.AddAudit("配方回滚", recipe.Name, "成功", $"目标版本：{dialog.RestoreSnapshot.Timestamp:yyyy-MM-dd HH:mm:ss}");
        }
    }

    /// <summary>R9: 试运行模式，输入空跑件数 N。</summary>
    private void StartTrialRun()
    {
        var recipe = Recipes.FirstOrDefault(r => r.Name == SelectedRecipeName);
        if (recipe is null) { Shell.ShowPopup("试运行", "请先选择配方", "Warning"); return; }

        var dialog = new RecipeTrialRunDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = recipe
        };

        if (dialog.ShowDialog() != true) return;
        recipe.IsTrialRun = true;
        recipe.TrialRunQuantity = dialog.Quantity;
        // 试运行：加载到参数但不切 IsActive
        ApplyRecipeParameters(recipe);
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", $"试运行模式启动：配方【{recipe.Name}】空跑 {dialog.Quantity} 件", "Info");
        Shell.AddAudit("配方试运行", recipe.Name, "成功", $"空跑数量：{dialog.Quantity}");
    }

    private void ApplyRecipeParameters(RecipeItem recipe)
    {
        foreach (var snap in recipe.Parameters)
        {
            var target = Shell.Parameters.FirstOrDefault(p => p.Name.Equals(snap.Name, StringComparison.OrdinalIgnoreCase));
            if (target is not null) target.Value = snap.Value;
        }
    }

    private RecipeItem CreateRecipeFromCurrentParameters(string name, string productCode, string version, string description, bool isActive)
    {
        return new RecipeItem
        {
            Name = name,
            ProductCode = productCode,
            Version = version,
            Description = description,
            IsActive = isActive,
            UpdatedAt = DateTime.Now,
            UpdatedBy = Shell.LoginUser,
            LineCompatibility = "全工位",
            Parameters = CloneParameters(Shell.Parameters)
        };
    }

    private RecipeItem CloneRecipe(RecipeItem source)
    {
        return new RecipeItem
        {
            Name = source.Name,
            ProductCode = source.ProductCode,
            Version = source.Version,
            Description = source.Description,
            IsActive = source.IsActive,
            UpdatedAt = source.UpdatedAt,
            UpdatedBy = source.UpdatedBy,
            LastUsedAt = source.LastUsedAt,
            LineCompatibility = source.LineCompatibility,
            Parameters = CloneParameters(source.Parameters)
        };
    }

    private static ObservableCollection<ParameterItem> CloneParameters(IEnumerable<ParameterItem> source)
        => new(source.Select(CloneParameter));

    private static ParameterItem CloneParameter(ParameterItem s) => new()
    {
        Category = s.Category,
        Name = s.Name,
        Value = s.Value,
        Unit = s.Unit,
        Description = s.Description,
        MinRole = s.MinRole,
        IsReadOnly = s.IsReadOnly,
        PermissionHint = s.PermissionHint,
        MinValue = s.MinValue,
        MaxValue = s.MaxValue
    };

    private static void UpdateRecipeParameterValue(RecipeItem recipe, string parameterName, string value)
    {
        var p = recipe.Parameters.FirstOrDefault(item => item.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
        if (p is not null) p.Value = value;
    }
}
