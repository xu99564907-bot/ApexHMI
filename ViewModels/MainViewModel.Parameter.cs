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
    // ========== 参数设定 ==========

    [RelayCommand]
    private async Task SaveParametersAsync()
    {
        if (!CanEditParameters) { SystemMessage = "当前权限不足，无法修改参数"; return; }
        var illegal = Parameters.FirstOrDefault(p => !CanEditParameter(p));
        if (illegal is not null) { SystemMessage = $"存在超权限参数：{illegal.Name}"; return; }
        var path = Path.Combine(GetProjectRoot(), "config", "parameters.json");
        await _parameterService.SaveAsync(path, Parameters);
        SystemMessage = $"参数已保存：{path}";
        AddLog("参数", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task LoadParametersAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "parameters.json");
        var items = await _parameterService.LoadAsync(path);
        if (items.Count == 0)
        {
            RefreshParameterPermissions();
            SystemMessage = "未找到参数文件，已保留当前示例参数";
            return;
        }

        Parameters.Clear();
        foreach (var item in items) Parameters.Add(item);
        RefreshParameterPermissions();
        SystemMessage = "参数加载完成";
        AddLog("参数", SystemMessage, "Info");
    }

    private bool FilterParameterItem(object item)
    {
        if (item is not ParameterItem parameter)
        {
            return false;
        }

        return CurrentParameterSubSection switch
        {
            "系统参数设定" => parameter.Category is "系统参数" or "联锁规则",
            "轴参数设定" => parameter.Category == "轴参数",
            "气缸参数设定" => parameter.Category == "气缸参数",
            "真空参数设定" => parameter.Category == "真空参数",
            "传感器参数设定" => parameter.Category == "传感器参数",
            _ => true
        };
    }

    private void SeedParameters()
    {
        Parameters.Add(new ParameterItem { Category = "系统参数", Name = "设备节拍", Value = "3.5", Unit = "s", Description = "设备标准节拍", MinRole = UserRole.Engineer });
        Parameters.Add(new ParameterItem { Category = "轴参数", Name = "轴速度", Value = "250", Unit = "mm/s", Description = "轴运行速度", MinRole = UserRole.Engineer });
        Parameters.Add(new ParameterItem { Category = "气缸参数", Name = "气缸延时", Value = "0.2", Unit = "s", Description = "气缸动作延时", MinRole = UserRole.Engineer });
        Parameters.Add(new ParameterItem { Category = "真空参数", Name = "真空检测超时", Value = "1.0", Unit = "s", Description = "真空建立超时时间", MinRole = UserRole.Administrator });
        Parameters.Add(new ParameterItem { Category = "传感器参数", Name = "滤波时间", Value = "50", Unit = "ms", Description = "传感器滤波时间", MinRole = UserRole.Engineer });
        Parameters.Add(new ParameterItem { Category = "联锁规则", Name = "自动运行允许手动气缸", Value = "false", Unit = "bool", Description = "决定自动运行时是否允许手动切换气缸", MinRole = UserRole.Administrator });
        Parameters.Add(new ParameterItem { Category = "联锁规则", Name = "自动运行允许手动挡停", Value = "false", Unit = "bool", Description = "决定自动运行时是否允许手动切换挡停", MinRole = UserRole.Administrator });
        Parameters.Add(new ParameterItem { Category = "联锁规则", Name = "机械手运行时允许复位", Value = "false", Unit = "bool", Description = "决定机械手运行中是否允许执行复位", MinRole = UserRole.Administrator });
        Parameters.Add(new ParameterItem { Category = "联锁规则", Name = "轴报警时允许运动", Value = "false", Unit = "bool", Description = "决定轴报警状态下是否允许 Jog/定位/回零", MinRole = UserRole.Administrator });
    }

    private void RefreshParameterPermissions()
    {
        foreach (var parameter in Parameters)
        {
            var canEdit = CanEditParameter(parameter);
            parameter.IsReadOnly = !canEdit;
            parameter.PermissionHint = canEdit ? "可编辑" : $"{parameter.MinRole} 及以上可编辑";
        }
    }

    private void Parameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ParameterItem>()) item.PropertyChanged += (_, _) => RefreshParameterPermissions();
        }
        RefreshParameterPermissions();
        ParametersView.Refresh();
    }
}
