using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

public sealed class RecipeViewModel : ModuleViewModelBase
{
    private readonly IRecipeService _recipeService;

    public RecipeViewModel(MainViewModel shell, IRecipeService recipeService)
        : base(shell, "配方管理")
    {
        _recipeService = recipeService;
        ApplyRecipeCommand = new RelayCommand<string?>(ApplyRecipe);
        CreateRecipeCommand = new RelayCommand(CreateRecipe);
        DuplicateRecipeCommand = new RelayCommand(DuplicateRecipe);
        DeleteRecipeCommand = new RelayCommand(DeleteRecipe);
        CaptureCurrentParametersToRecipeCommand = new RelayCommand(CaptureCurrentParametersToRecipe);
        LoadRecipesCommand = new AsyncRelayCommand(LoadRecipesAsync);
        SaveRecipesCommand = new AsyncRelayCommand(SaveRecipesAsync);
    }

    public IRelayCommand<string?> ApplyRecipeCommand { get; }
    public IRelayCommand CreateRecipeCommand { get; }
    public IRelayCommand DuplicateRecipeCommand { get; }
    public IRelayCommand DeleteRecipeCommand { get; }
    public IRelayCommand CaptureCurrentParametersToRecipeCommand { get; }
    public IAsyncRelayCommand LoadRecipesCommand { get; }
    public IAsyncRelayCommand SaveRecipesCommand { get; }

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

        var highSpeedRecipe = CreateRecipeFromCurrentParameters("产品B", "B-002", "V1.1", "高速工艺", false);
        UpdateRecipeParameterValue(highSpeedRecipe, "设备节拍", "3.0");
        UpdateRecipeParameterValue(highSpeedRecipe, "轴速度", "320");
        UpdateRecipeParameterValue(highSpeedRecipe, "气缸延时", "0.15");
        Recipes.Add(highSpeedRecipe);

        var trialRecipe = CreateRecipeFromCurrentParameters("产品C", "C-003", "V2.0", "试产工艺", false);
        UpdateRecipeParameterValue(trialRecipe, "设备节拍", "4.2");
        UpdateRecipeParameterValue(trialRecipe, "真空检测超时", "1.5");
        UpdateRecipeParameterValue(trialRecipe, "滤波时间", "80");
        Recipes.Add(trialRecipe);

        Shell.SelectedRecipeName = Recipes.FirstOrDefault(recipe => recipe.IsActive)?.Name ?? "产品A";
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
            Recipes.Add(item);
        }

        Shell.SelectedRecipeName = Recipes.FirstOrDefault(recipe => recipe.IsActive)?.Name ?? Recipes.First().Name;
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", "配方加载完成", "Info");
    }

    public void ApplyRecipe(string? recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeName))
        {
            return;
        }

        var recipe = Recipes.FirstOrDefault(item => item.Name.Equals(recipeName, StringComparison.OrdinalIgnoreCase));
        if (recipe is null)
        {
            return;
        }

        foreach (var item in Recipes)
        {
            item.IsActive = item == recipe;
        }

        Shell.SelectedRecipeName = recipe.Name;
        ApplyRecipeParameters(recipe);
        Shell.SetTagValue("Recipe_Name", recipe.Name);
        recipe.UpdatedAt = DateTime.Now;
        recipe.UpdatedBy = Shell.LoginUser;
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
        while (Recipes.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
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
        var source = Recipes.FirstOrDefault(item => item.Name == SelectedRecipeName);
        if (source is null)
        {
            Shell.SystemMessage = "请先选择要复制的配方";
            return;
        }

        var name = $"{source.Name}_Copy";
        var index = 1;
        while (Recipes.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
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

    public void DeleteRecipe()
    {
        var recipe = Recipes.FirstOrDefault(item => item.Name == SelectedRecipeName);
        if (recipe is null)
        {
            Shell.SystemMessage = "请先选择要删除的配方";
            return;
        }

        if (Recipes.Count <= 1)
        {
            Shell.ShowPopup("操作禁止", "至少保留一个配方，当前不能删除最后一个配方。", "Warning");
            return;
        }

        if (!Shell.RequestConfirmation("删除配方", $"确认删除配方【{recipe.Name}】吗？"))
        {
            return;
        }

        Recipes.Remove(recipe);
        var next = Recipes.First();
        next.IsActive = true;
        Shell.SelectedRecipeName = next.Name;
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", $"已删除配方：{recipe.Name}", "Warning");
        Shell.AddAudit("配方删除", recipe.Name, "成功", "已从配方列表移除");
    }

    public void CaptureCurrentParametersToRecipe()
    {
        var recipe = Recipes.FirstOrDefault(item => item.Name == SelectedRecipeName);
        if (recipe is null)
        {
            Shell.SystemMessage = "请先选择要保存的配方";
            return;
        }

        recipe.Parameters = CloneParameters(Shell.Parameters);
        recipe.UpdatedAt = DateTime.Now;
        recipe.UpdatedBy = Shell.LoginUser;
        RefreshActiveRecipeParameters();
        Shell.AddLog("配方", $"已用当前参数覆盖配方：{recipe.Name}", "Info");
        Shell.AddAudit("配方保存", recipe.Name, "成功", $"保存 {recipe.Parameters.Count} 项参数快照");
    }

    public void RefreshActiveRecipeParameters()
    {
        ActiveRecipeParameters.Clear();
        var recipe = Recipes.FirstOrDefault(item => item.Name == SelectedRecipeName) ?? Recipes.FirstOrDefault(item => item.IsActive);
        if (recipe?.Parameters is null)
        {
            return;
        }

        foreach (var parameter in recipe.Parameters)
        {
            ActiveRecipeParameters.Add(CloneParameter(parameter));
        }
    }

    private void ApplyRecipeParameters(RecipeItem recipe)
    {
        foreach (var snapshot in recipe.Parameters)
        {
            var target = Shell.Parameters.FirstOrDefault(item => item.Name.Equals(snapshot.Name, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
            {
                target.Value = snapshot.Value;
            }
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
            Parameters = CloneParameters(source.Parameters)
        };
    }

    private static ObservableCollection<ParameterItem> CloneParameters(IEnumerable<ParameterItem> source)
    {
        return new ObservableCollection<ParameterItem>(source.Select(CloneParameter));
    }

    private static ParameterItem CloneParameter(ParameterItem source)
    {
        return new ParameterItem
        {
            Category = source.Category,
            Name = source.Name,
            Value = source.Value,
            Unit = source.Unit,
            Description = source.Description,
            MinRole = source.MinRole,
            IsReadOnly = source.IsReadOnly,
            PermissionHint = source.PermissionHint
        };
    }

    private static void UpdateRecipeParameterValue(RecipeItem recipe, string parameterName, string value)
    {
        var parameter = recipe.Parameters.FirstOrDefault(item => item.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
        if (parameter is not null)
        {
            parameter.Value = value;
        }
    }
}
