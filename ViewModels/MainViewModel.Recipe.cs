using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ApexHMI.Models;
using ApexHMI.Services;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    // ========== 配方管理 ==========

    private void SeedRecipes()
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

        SelectedRecipeName = Recipes.FirstOrDefault(x => x.IsActive)?.Name ?? "产品A";
        RefreshActiveRecipeParameters();
    }

    [RelayCommand]
    private async Task SaveRecipesAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "recipes.json");
        await _recipeService.SaveAsync(path, Recipes);
        AddLog("配方", $"配方已保存：{path}", "Info");
    }

    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "recipes.json");
        var items = await _recipeService.LoadAsync(path);
        if (items.Count == 0)
        {
            AddLog("配方", "未找到配方文件，保留当前示例配方", "Info");
            return;
        }
        Recipes.Clear();
        foreach (var item in items)
        {
            if (item.Parameters is null)
            {
                item.Parameters = new ObservableCollection<ParameterItem>();
            }
            Recipes.Add(item);
        }
        SelectedRecipeName = Recipes.FirstOrDefault(x => x.IsActive)?.Name ?? Recipes.First().Name;
        RefreshActiveRecipeParameters();
        AddLog("配方", "配方加载完成", "Info");
    }

    [RelayCommand]
    private void ApplyRecipe(string? recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeName)) return;
        var recipe = Recipes.FirstOrDefault(x => x.Name.Equals(recipeName, StringComparison.OrdinalIgnoreCase));
        if (recipe is null) return;

        foreach (var item in Recipes) item.IsActive = item == recipe;
        SelectedRecipeName = recipe.Name;
        ApplyRecipeParameters(recipe);
        SetTagValue("Recipe_Name", recipe.Name);
        recipe.UpdatedAt = DateTime.Now;
        recipe.UpdatedBy = LoginUser;
        RefreshActiveRecipeParameters();
        AddLog("配方", $"已切换配方：{recipe.Name}", "Info");
        AddAudit("配方切换", recipe.Name, "成功", $"加载 {recipe.Parameters.Count} 项工艺参数");
        UpdateRuntimeVisuals();
    }

    [RelayCommand]
    private void CreateRecipe()
    {
        var baseName = $"新配方{Recipes.Count + 1}";
        var name = baseName;
        var index = 1;
        while (Recipes.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            name = $"{baseName}_{index}";
        }

        var recipe = CreateRecipeFromCurrentParameters(name, $"NEW-{DateTime.Now:HHmmss}", "V1.0", "从当前参数新建", false);
        Recipes.Add(recipe);
        SelectedRecipeName = recipe.Name;
        RefreshActiveRecipeParameters();
        AddLog("配方", $"已新建配方：{recipe.Name}", "Info");
        AddAudit("配方新建", recipe.Name, "成功", "基于当前参数创建");
    }

    [RelayCommand]
    private void DuplicateRecipe()
    {
        var source = Recipes.FirstOrDefault(x => x.Name == SelectedRecipeName);
        if (source is null)
        {
            SystemMessage = "请先选择要复制的配方";
            return;
        }

        var name = $"{source.Name}_Copy";
        var index = 1;
        while (Recipes.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            name = $"{source.Name}_Copy{index}";
        }

        var clone = CloneRecipe(source);
        clone.Name = name;
        clone.IsActive = false;
        clone.UpdatedAt = DateTime.Now;
        clone.UpdatedBy = LoginUser;
        Recipes.Add(clone);
        SelectedRecipeName = clone.Name;
        RefreshActiveRecipeParameters();
        AddLog("配方", $"已复制配方：{source.Name} -> {clone.Name}", "Info");
        AddAudit("配方复制", clone.Name, "成功", $"来源：{source.Name}");
    }

    [RelayCommand]
    private void DeleteRecipe()
    {
        var recipe = Recipes.FirstOrDefault(x => x.Name == SelectedRecipeName);
        if (recipe is null)
        {
            SystemMessage = "请先选择要删除的配方";
            return;
        }

        if (Recipes.Count <= 1)
        {
            ShowPopup("操作禁止", "至少保留一个配方，当前不能删除最后一个配方。", "Warning");
            return;
        }

        if (!RequestConfirmation("删除配方", $"确认删除配方【{recipe.Name}】吗？"))
        {
            return;
        }

        Recipes.Remove(recipe);
        var next = Recipes.First();
        next.IsActive = true;
        SelectedRecipeName = next.Name;
        RefreshActiveRecipeParameters();
        AddLog("配方", $"已删除配方：{recipe.Name}", "Warning");
        AddAudit("配方删除", recipe.Name, "成功", "已从配方列表移除");
    }

    [RelayCommand]
    private void CaptureCurrentParametersToRecipe()
    {
        var recipe = Recipes.FirstOrDefault(x => x.Name == SelectedRecipeName);
        if (recipe is null)
        {
            SystemMessage = "请先选择要保存的配方";
            return;
        }

        recipe.Parameters = CloneParameters(Parameters);
        recipe.UpdatedAt = DateTime.Now;
        recipe.UpdatedBy = LoginUser;
        RefreshActiveRecipeParameters();
        AddLog("配方", $"已用当前参数覆盖配方：{recipe.Name}", "Info");
        AddAudit("配方保存", recipe.Name, "成功", $"保存 {recipe.Parameters.Count} 项参数快照");
    }

    private void RefreshActiveRecipeParameters()
    {
        ActiveRecipeParameters.Clear();
        var recipe = Recipes.FirstOrDefault(x => x.Name == SelectedRecipeName) ?? Recipes.FirstOrDefault(x => x.IsActive);
        if (recipe?.Parameters is null) return;
        foreach (var parameter in recipe.Parameters)
        {
            ActiveRecipeParameters.Add(CloneParameter(parameter));
        }
    }

    private void ApplyRecipeParameters(RecipeItem recipe)
    {
        foreach (var snapshot in recipe.Parameters)
        {
            var target = Parameters.FirstOrDefault(x => x.Name.Equals(snapshot.Name, StringComparison.OrdinalIgnoreCase));
            if (target is null) continue;
            target.Value = snapshot.Value;
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
            UpdatedBy = LoginUser,
            Parameters = CloneParameters(Parameters)
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

    private ObservableCollection<ParameterItem> CloneParameters(IEnumerable<ParameterItem> source)
    {
        return new ObservableCollection<ParameterItem>(source.Select(CloneParameter));
    }

    private ParameterItem CloneParameter(ParameterItem source)
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

    private void UpdateRecipeParameterValue(RecipeItem recipe, string parameterName, string value)
    {
        var parameter = recipe.Parameters.FirstOrDefault(x => x.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
        if (parameter is not null)
        {
            parameter.Value = value;
        }
    }
}
