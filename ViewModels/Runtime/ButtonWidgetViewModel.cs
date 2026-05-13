#nullable enable
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using Serilog;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P1 多动作链版本：按钮按事件顺序执行 <see cref="WidgetInstance.Events"/> 中挂的 <see cref="ActionStep"/> 列表。
/// <para>事件名：click / press / release。</para>
/// <para>向后兼容：如果新 Events 为空且 <see cref="WidgetInstance.ActionType"/> 非空，
/// click 事件 fallback 到旧 ActionType+ActionParam（工程加载时通常已迁移）。</para>
/// <para>读取地址 = <c>Model.Binding.TagId</c>，留空时 fallback 到 click 事件下第一个 step 的 address。</para>
/// </summary>
public partial class ButtonWidgetViewModel : WidgetViewModelBase
{
    private bool _currentBoolValue;

    public ButtonWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var readTag = ResolveReadTag();
        if (!string.IsNullOrEmpty(readTag))
        {
            _dataContext.RegisterValueCallback(readTag!, v => _currentBoolValue = ParseBool(v));
        }
    }

    public string Text       => Prop("text",       "按钮");
    public string Background => Prop("background", "#2563EB");
    public string Foreground => Prop("foreground", "#FFFFFF");

    /// <summary>读取地址优先级：Binding.TagId > 新 Events["click"][0].Args["address"] > 旧 ActionParam。</summary>
    private string? ResolveReadTag()
    {
        var t = Model.Binding?.TagId;
        if (!string.IsNullOrWhiteSpace(t)) return t;

        if (Model.Events.TryGetValue("click", out var steps))
        {
            foreach (var s in steps)
            {
                if (IsButtonModeId(s.FunctionId)
                    && s.Args.TryGetValue("address", out var addr)
                    && !string.IsNullOrWhiteSpace(addr))
                    return addr;
            }
        }
        if (IsButtonModeId(Model.ActionType) && !string.IsNullOrWhiteSpace(Model.ActionParam))
            return Model.ActionParam;
        return null;
    }

    private static bool IsButtonModeId(string? id) =>
        id is "set-on" or "set-off" or "toggle" or "momentary"
            or "set-bit" or "reset-bit" or "toggle-bit";

    private static bool ParseBool(string s) =>
        !string.IsNullOrEmpty(s) &&
        (string.Equals(s, "True", StringComparison.OrdinalIgnoreCase) || s == "1");

    private bool CheckPermission()
    {
        if (string.IsNullOrWhiteSpace(Model.RequiredRole) ||
            _dataContext.Shell is not ApexHMI.ViewModels.MainViewModel shell) return true;
        if (!Enum.TryParse<ApexHMI.Models.UserRole>(Model.RequiredRole, true, out var required))
            required = ApexHMI.Models.UserRole.Operator;
        if (shell.CurrentUserRole < required)
        {
            shell.SystemMessage = $"权限不足：操作需要 {required} 角色";
            return false;
        }
        return true;
    }

    /// <summary>顺序执行 event 名下挂的所有动作步骤。</summary>
    private void ExecuteEvent(string eventName)
    {
        // 新格式：Events 字典
        if (Model.Events.TryGetValue(eventName, out var steps) && steps.Count > 0)
        {
            foreach (var step in steps) RunStep(eventName, step);
            return;
        }

        // 兼容旧字段：仅 click 事件 fallback 到 ActionType + ActionParam
        if (eventName == "click" && !string.IsNullOrEmpty(Model.ActionType))
        {
            RunLegacyClick();
        }
    }

    /// <summary>执行单个 ActionStep。momentary 在 click 阶段跳过，由 press/release 处理。</summary>
    private void RunStep(string eventName, ActionStep step)
    {
        var args = step.Args ?? new Dictionary<string, string>();
        args.TryGetValue("address", out var addr); addr ??= string.Empty;
        args.TryGetValue("value",   out var value); value ??= string.Empty;
        args.TryGetValue("routeKey",out var route); route ??= string.Empty;
        args.TryGetValue("text",    out var text);  text ??= string.Empty;

        // momentary：click 跳过；press 写 True；release 写 False
        if (step.FunctionId == "momentary")
        {
            if (string.IsNullOrWhiteSpace(addr)) return;
            switch (eventName)
            {
                case "press":
                    if (!CheckPermission()) return;
                    _dataContext.ExecuteAction("write-bool", $"{addr}|True");
                    break;
                case "release":
                    _dataContext.ExecuteAction("write-bool", $"{addr}|False");
                    break;
                // click: 跳过
            }
            return;
        }

        // 非 momentary：仅在 click 中执行；press/release 跳过（保留扩展）
        if (eventName != "click") return;

        if (!CheckPermission()) return;

        switch (step.FunctionId)
        {
            case "set-bit":
            case "set-on":
                if (!string.IsNullOrWhiteSpace(addr))
                    _dataContext.ExecuteAction("write-bool", $"{addr}|True");
                break;
            case "reset-bit":
            case "set-off":
                if (!string.IsNullOrWhiteSpace(addr))
                    _dataContext.ExecuteAction("write-bool", $"{addr}|False");
                break;
            case "toggle-bit":
            case "toggle":
                if (!string.IsNullOrWhiteSpace(addr))
                    _dataContext.ExecuteAction("write-bool", $"{addr}|{!_currentBoolValue}");
                break;
            case "write-bool":
                if (!string.IsNullOrWhiteSpace(addr))
                    _dataContext.ExecuteAction("write-bool", $"{addr}|{value}");
                break;
            case "write-int":
                if (!string.IsNullOrWhiteSpace(addr))
                    _dataContext.ExecuteAction("write-int", $"{addr}|{value}");
                break;
            case "write-float":
                if (!string.IsNullOrWhiteSpace(addr))
                    _dataContext.ExecuteAction("write-float", $"{addr}|{value}");
                break;
            case "navigate":
            case "popup":
                if (!string.IsNullOrWhiteSpace(route))
                    _dataContext.ExecuteAction("navigate", route);
                break;
            case "show-dialog":
                _dataContext.ExecuteAction("show-dialog", text);
                break;
            case "back":
            case "ack-current":
            case "clear-buffer":
            case "increment":
            case "decrement":
            case "play-sound":
                Log.Warning("ButtonWidget: 未实现的动作 {FunctionId}（占位）", step.FunctionId);
                break;
            default:
                // 未知 FunctionId：保底走旧 ExecuteAction
                if (!string.IsNullOrEmpty(step.FunctionId))
                {
                    var legacyParam = !string.IsNullOrEmpty(addr) ? addr
                        : !string.IsNullOrEmpty(route) ? route
                        : !string.IsNullOrEmpty(text) ? text : value;
                    _dataContext.ExecuteAction(step.FunctionId, legacyParam);
                }
                break;
        }
    }

    /// <summary>无新 Events 时旧字段路径（理论上已被迁移层覆盖，仅作保底）。</summary>
    private void RunLegacyClick()
    {
        if (!CheckPermission()) return;

        var at = Model.ActionType ?? string.Empty;
        var writeTag = Model.ActionParam ?? string.Empty;

        switch (at)
        {
            case "set-on":
                if (!string.IsNullOrWhiteSpace(writeTag))
                    _dataContext.ExecuteAction("write-bool", $"{writeTag}|True");
                break;
            case "set-off":
                if (!string.IsNullOrWhiteSpace(writeTag))
                    _dataContext.ExecuteAction("write-bool", $"{writeTag}|False");
                break;
            case "toggle":
                if (!string.IsNullOrWhiteSpace(writeTag))
                    _dataContext.ExecuteAction("write-bool", $"{writeTag}|{!_currentBoolValue}");
                break;
            case "momentary":
                // 留给 PressDown / Release
                break;
            default:
                if (!string.IsNullOrEmpty(at))
                    _dataContext.ExecuteAction(at, writeTag);
                break;
        }
    }

    [RelayCommand]
    private void Click() => ExecuteEvent("click");

    /// <summary>复归型按下：写 True（新 Events 中的 momentary step 或旧字段）。</summary>
    [RelayCommand]
    private void PressDown()
    {
        // 新格式：press 事件 + click 事件里的 momentary step 都允许在 press 阶段触发
        if (Model.Events.TryGetValue("press", out var pressSteps) && pressSteps.Count > 0)
        {
            foreach (var s in pressSteps) RunStep("press", s);
        }
        if (Model.Events.TryGetValue("click", out var clickSteps))
        {
            foreach (var s in clickSteps)
                if (s.FunctionId == "momentary") RunStep("press", s);
        }
        // 兼容旧字段
        else if (Model.ActionType == "momentary")
        {
            if (!CheckPermission()) return;
            var w = Model.ActionParam ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(w))
                _dataContext.ExecuteAction("write-bool", $"{w}|True");
        }
    }

    /// <summary>复归型松开：写 False。</summary>
    [RelayCommand]
    private void Release()
    {
        if (Model.Events.TryGetValue("release", out var releaseSteps) && releaseSteps.Count > 0)
        {
            foreach (var s in releaseSteps) RunStep("release", s);
        }
        if (Model.Events.TryGetValue("click", out var clickSteps))
        {
            foreach (var s in clickSteps)
                if (s.FunctionId == "momentary") RunStep("release", s);
        }
        else if (Model.ActionType == "momentary")
        {
            var w = Model.ActionParam ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(w))
                _dataContext.ExecuteAction("write-bool", $"{w}|False");
        }
    }
}
