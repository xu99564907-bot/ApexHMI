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
using Serilog;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    // ========== 手动操作 / 气缸 / 轴 / 机械手 / 模式切换 ==========

    public async Task RefreshSelectedCylinderParmValuesAsync()
    {
        var selectedNodeIds = new[]
        {
            SelectedCylinderHomeCommandBinding,
            SelectedCylinderWorkCommandBinding,
            SelectedCylinderHomeSensorBinding,
            SelectedCylinderWorkSensorBinding,
            SelectedCylinderHomeInterlockBinding,
            SelectedCylinderWorkInterlockBinding,
            SelectedCylinderHomeDisplayBinding,
            SelectedCylinderWorkDisplayBinding,
            SelectedCylinderHomeDisplayFallbackBinding,
            SelectedCylinderWorkDisplayFallbackBinding,
            SelectedCylinderDisableHomeBinding,
            SelectedCylinderDisableWorkBinding,
            SelectedCylinderErrorDelayBinding,
            SelectedCylinderHomeDelayBinding,
            SelectedCylinderWorkDelayBinding
        }
        .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        if (_opcUaService.IsConnected && selectedNodeIds.Count > 0)
        {
            var values = await _opcUaService.ReadNodeValuesAsync(selectedNodeIds);
            await RunOnUiThreadAsync(() =>
            {
                foreach (var nodeId in selectedNodeIds)
                {
                    if (!values.TryGetValue(nodeId, out var value))
                    {
                        continue;
                    }

                    // 先写入绑定键缓存：即使变量表里尚未有 Tag，GetTagValue/诊断框也能显示与 OPC 读一致
                    RecordOpcBindingString(nodeId, value);
                    var tag = FindTagByNodeId(nodeId) ?? FindTagByNameOrNodeId(nodeId);
                    if (tag is not null)
                    {
                        OnPlcReadAppliedToTag(tag, value);
                    }
                }

                LoadSelectedCylinderParmSettings();
                RefreshManualCylinderBlockStates();
                RefreshCylinderBindingProperties();
            });

            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            LoadSelectedCylinderParmSettings();
            RefreshManualCylinderBlockStates();
            RefreshCylinderBindingProperties();
        });
    }

    public async Task RefreshSelectedAxisBindingValuesAsync()
    {
        var selectedNodeIds = new[]
        {
            SelectedAxisPowerCommandBinding,
            SelectedAxisStopCommandBinding,
            SelectedAxisHomeCommandBinding,
            SelectedAxisJogForwardBinding,
            SelectedAxisJogBackwardBinding,
            SelectedAxisStartPositionCommandBinding,
            SelectedAxisReferenceCommandBinding,
            SelectedAxisTeachEnableBinding,
            SelectedAxisTeachWriteBinding,
            SelectedAxisPointSelectBinding,
            SelectedAxisMoveToPointBinding,
            SelectedAxisSetPositionBinding,
            SelectedAxisSetVelocityBinding,
            SelectedAxisHomeSignalBinding,
            SelectedAxisPositiveLimitBinding,
            SelectedAxisNegativeLimitBinding,
            SelectedAxisServoFeedbackBinding,
            SelectedAxisPowerOnBinding,
            SelectedAxisBusyBinding,
            SelectedAxisPosOkBinding,
            SelectedAxisInitializedBinding,
            SelectedAxisHomeInterlockBinding,
            SelectedAxisJogInterlockBinding,
            SelectedAxisPositionInterlockBinding,
            SelectedAxisErrorBinding,
            SelectedAxisErrorIdBinding,
            SelectedAxisActualPositionBinding,
            SelectedAxisActualVelocityBinding,
            SelectedAxisActualTorqueBinding,
            SelectedAxisStateBinding
        }
        .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        if (_opcUaService.IsConnected && selectedNodeIds.Count > 0)
        {
            var values = await _opcUaService.ReadNodeValuesAsync(selectedNodeIds);
            await RunOnUiThreadAsync(() =>
            {
                foreach (var nodeId in selectedNodeIds)
                {
                    if (!values.TryGetValue(nodeId, out var value))
                    {
                        continue;
                    }

                    RecordOpcBindingString(nodeId, value);
                    var tag = FindTagByNodeId(nodeId) ?? FindTagByNameOrNodeId(nodeId);
                    if (tag is not null)
                    {
                        OnPlcReadAppliedToTag(tag, value);
                    }
                }

                RefreshManualAxisBlockStates();
                RefreshAxisBindingProperties();
            });

            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            RefreshManualAxisBlockStates();
            RefreshAxisBindingProperties();
        });
    }

    [RelayCommand]
    private async Task ToggleDeviceAsync(string? tagName)
    {
        if (!CanOperateDevices)
        {
            SystemMessage = "当前权限不足，无法操作设备";
            return;
        }

        if (string.IsNullOrWhiteSpace(tagName)) return;
        await ToggleBoundBooleanTagAsync(tagName, tagName);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task CylinderMoveToHomeAsync(ManualCylinderBlockItem? block) => await SetCylinderPositionAsync(block, false, "气缸原点操作");

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task CylinderMoveToWorkAsync(ManualCylinderBlockItem? block) => await SetCylinderPositionAsync(block, true, "气缸动点操作");

    [RelayCommand]
    private void ToggleCylinderHomeMask()
    {
        CylinderHomeMaskEnabled = !CylinderHomeMaskEnabled;
        _ = WriteSelectedCylinderBoolParmAsync(SelectedCylinderDisableHomeBinding, CylinderHomeMaskEnabled, CylinderHomeMaskEnabled ? "已开启气缸原点屏蔽" : "已关闭气缸原点屏蔽");
    }

    [RelayCommand]
    private void ToggleCylinderWorkMask()
    {
        CylinderWorkMaskEnabled = !CylinderWorkMaskEnabled;
        _ = WriteSelectedCylinderBoolParmAsync(SelectedCylinderDisableWorkBinding, CylinderWorkMaskEnabled, CylinderWorkMaskEnabled ? "已开启气缸动点屏蔽" : "已关闭气缸动点屏蔽");
    }

    [RelayCommand]
    private async Task SetDebugModeAsync() => await SetExclusiveModeAsync("Mode_Debug", "调试模式");

    [RelayCommand]
    private async Task SetDryRunModeAsync() => await SetExclusiveModeAsync("Mode_DryRun", "空跑模式");

    [RelayCommand]
    private async Task SetBypassStationModeAsync() => await SetExclusiveModeAsync("Mode_BypassStation", "过站模式");

    [RelayCommand]
    private async Task SetManualModeAsync() => await SetExclusiveModeAsync("Mode_Manual", "人工模式");

    [RelayCommand]
    private async Task SetAutoModeAsync() => await SetExclusiveModeAsync("Mode_Auto", "自动模式");

    [RelayCommand]
    private async Task StartDeviceAsync()
    {
        if (GetBoolTag("Device_Start"))
        {
            ShowPopup("提示", "设备已处于启动状态。", "Info");
            return;
        }
        if (ActiveAlarmCount > 0)
        {
            ShowPopup("操作条件不满足", "当前存在活动报警，不能启动设备。", "Interlock");
            return;
        }
        if (!IsManualMode && !IsAutoMode)
        {
            ShowPopup("操作条件不满足", "请先选择人工模式或自动模式，再启动设备。", "Warning");
            return;
        }

        await ToggleBoundBooleanTagAsync("Device_Start", "设备启动");
    }

    [RelayCommand]
    private async Task StopDeviceAsync()
    {
        if (!GetBoolTag("Device_Start"))
        {
            ShowPopup("提示", "设备当前已停止。", "Info");
            return;
        }

        await ToggleBoundBooleanTagAsync("Device_Start", "设备停止");
    }

    [RelayCommand]
    private async Task ResetAlarmFromHomeAsync()
    {
        if (!CanAdmin)
        {
            ShowPopup("权限不足", "仅管理员可在主页执行报警复位。", "Error");
            return;
        }

        await ResetAllAlarmsAsync();
    }

    [RelayCommand]
    private async Task ResetMotorFaultAsync()
    {
        if (!GetBoolTag("Motor1_Fault"))
        {
            ShowPopup("操作条件不满足", "当前电机无故障，无需执行故障复位。", "Warning");
            return;
        }
        if (!RequestConfirmation("确认复位", "确认执行电机故障复位吗？")) return;
        await PulseBooleanTagAsync("Motor1_Reset", "电机故障复位输出");
    }

    [RelayCommand]
    private async Task PauseRobotAsync()
    {
        if (!GetBoolTag("Robot_Run"))
        {
            ShowPopup("操作条件不满足", "机械手当前未运行，不能执行暂停操作。", "Warning");
            return;
        }
        await PulseBooleanTagAsync("Robot_Pause", "机械手暂停输出");
    }

    [RelayCommand]
    private async Task ResetRobotAsync()
    {
        if (GetBoolTag("Robot_Run") && !AllowRobotResetWhenRunning)
        {
            ShowPopup("操作条件不满足", "机械手运行中，请先停止后再复位。", "Warning");
            return;
        }
        if (!RequestConfirmation("确认复位", "确认执行机械手复位吗？")) return;
        await PulseBooleanTagAsync("Robot_Reset", "机械手复位输出");
    }

    [RelayCommand]
    private async Task ToggleAxisEnableAsync()
    {
        if (GetBoolTag("Axis1_Alarm"))
        {
            ShowPopup("操作条件不满足", "轴当前存在报警，请先复位报警后再进行使能。", "Warning");
            return;
        }
        await ToggleBoundBooleanTagAsync("Axis1_Enable", "轴使能");
    }

    [RelayCommand]
    private async Task AxisAlarmResetAsync()
    {
        if (!GetBoolTag("Axis1_Alarm"))
        {
            ShowPopup("操作条件不满足", "轴当前无报警，无需执行报警复位。", "Warning");
            return;
        }
        if (!RequestConfirmation("确认复位", "确认执行轴报警复位吗？")) return;
        await PulseBooleanTagAsync("Axis1_AlarmReset", "轴报警复位");
    }

    [RelayCommand]
    private async Task MoveAxisHomeAsync()
    {
        if (!GetBoolTag("Axis1_Enable"))
        {
            ShowPopup("操作条件不满足", "轴未使能，不能执行回零操作。", "Warning");
            return;
        }
        if (GetBoolTag("Axis1_Alarm") && !AllowAxisMoveWhenAlarm)
        {
            ShowPopup("操作条件不满足", "轴存在报警，不能执行回零操作。", "Warning");
            return;
        }
        await WriteAxisPositionAsync(0.0, "轴已回零");
    }

    [RelayCommand]
    private async Task JogAxisPositiveAsync()
    {
        if (!GetBoolTag("Axis1_Enable")) { ShowPopup("操作条件不满足", "轴未使能，不能执行 Jog+。", "Warning"); return; }
        if (GetBoolTag("Axis1_Alarm") && !AllowAxisMoveWhenAlarm) { ShowPopup("操作条件不满足", "轴存在报警，不能执行 Jog+。", "Warning"); return; }
        if (!double.TryParse(AxisJogDistance, out var jog)) { ShowPopup("参数错误", "Jog 距离格式错误，请输入有效数字。", "Warning"); return; }
        var current = GetAxisCurrentPosition();
        await WriteAxisPositionAsync(current + jog, $"轴 Jog+ {jog}");
    }

    [RelayCommand]
    private async Task JogAxisNegativeAsync()
    {
        if (!GetBoolTag("Axis1_Enable")) { ShowPopup("操作条件不满足", "轴未使能，不能执行 Jog-。", "Warning"); return; }
        if (GetBoolTag("Axis1_Alarm") && !AllowAxisMoveWhenAlarm) { ShowPopup("操作条件不满足", "轴存在报警，不能执行 Jog-。", "Warning"); return; }
        if (!double.TryParse(AxisJogDistance, out var jog)) { ShowPopup("参数错误", "Jog 距离格式错误，请输入有效数字。", "Warning"); return; }
        var current = GetAxisCurrentPosition();
        await WriteAxisPositionAsync(current - jog, $"轴 Jog- {jog}");
    }

    [RelayCommand]
    private async Task MoveAxisToTargetAsync()
    {
        if (!GetBoolTag("Axis1_Enable")) { ShowPopup("操作条件不满足", "轴未使能，不能执行定位移动。", "Warning"); return; }
        if (GetBoolTag("Axis1_Alarm") && !AllowAxisMoveWhenAlarm) { ShowPopup("操作条件不满足", "轴存在报警，不能执行定位移动。", "Warning"); return; }
        if (!double.TryParse(AxisTargetPosition, out var target)) { ShowPopup("参数错误", "目标位置格式错误，请输入有效数字。", "Warning"); return; }
        await WriteAxisPositionAsync(target, $"轴移动到 {target}");
    }

    // ========== 运行态元素操作 ==========

    private async Task ExecuteRuntimeElementActionAsync(DesignerElement element)
    {
        var action = string.IsNullOrWhiteSpace(element.CommandBinding)
            ? GetDefaultDesignerAction(element.ElementType)
            : element.CommandBinding;

        switch (action)
        {
            case "页面跳转":
                if (!string.IsNullOrWhiteSpace(element.NavigationTarget))
                {
                    NavigateToPage(element.NavigationTarget);
                }
                break;
            case "变量翻转":
                await ToggleBoundBooleanTagAsync(element.TagBinding, element.Text);
                break;
            case "置位":
                await PulseOrWriteBoundTagAsync(element.TagBinding, true, false, element.Text);
                break;
            case "复位":
                await PulseOrWriteBoundTagAsync(element.TagBinding, false, false, element.Text);
                break;
            case "脉冲":
                await PulseOrWriteBoundTagAsync(element.TagBinding, true, true, element.Text);
                break;
            case "气缸回原":
                await SetCylinderPositionAsync(null, false, element.Text);
                break;
            case "气缸动点":
                await SetCylinderPositionAsync(null, true, element.Text);
                break;
        }
    }

    private static string GetDefaultDesignerAction(string elementType) => elementType switch
    {
        "PageButton" => "页面跳转",
        "Button" or "Motor" or "Cylinder" or "Stopper" or "Robot" => "变量翻转",
        _ => string.Empty
    };

    private async Task ToggleBoundBooleanTagAsync(string tagName, string sourceText)
    {
        if (!CanOperateDevices) { ShowPopup("权限不足", "当前权限不足，无法操作设备。", "Error"); return; }
        if (tagName.Equals("Cylinder_Extend", StringComparison.OrdinalIgnoreCase) && GetBoolTag("Alarm_EStop"))
        {
            ShowPopup("联锁禁止", "急停报警未恢复，不能操作气缸。", "Interlock");
            return;
        }
        if (tagName.Equals("Cylinder_Extend", StringComparison.OrdinalIgnoreCase) && GetBoolTag("Y_RunLamp") && !AllowManualCylinderWhenAuto)
        {
            ShowPopup("联锁禁止", "设备自动运行中，不能手动切换气缸。", "Interlock");
            return;
        }
        if (tagName.Equals("Cylinder_Extend", StringComparison.OrdinalIgnoreCase) && !GetBoolTag("Cylinder_BwdLS") && !GetBoolTag("Cylinder_FwdLS"))
        {
            ShowPopup("联锁禁止", "气缸当前处于中间状态，禁止再次切换。", "Interlock");
            return;
        }
        if (tagName.Equals("Stopper_Up", StringComparison.OrdinalIgnoreCase) && GetBoolTag("Y_RunLamp") && !AllowManualStopperWhenAuto)
        {
            ShowPopup("联锁禁止", "设备自动运行中，不能手动切换挡停。", "Interlock");
            return;
        }
        if (tagName.Equals("Robot_Run", StringComparison.OrdinalIgnoreCase) && GetBoolTag("Axis1_Alarm"))
        {
            ShowPopup("联锁禁止", "轴存在报警，不能启动机械手。", "Interlock");
            return;
        }
        if (tagName.Equals("Robot_Run", StringComparison.OrdinalIgnoreCase) && !GetBoolTag("Axis1_Enable"))
        {
            ShowPopup("联锁禁止", "轴未使能，不能启动机械手。", "Interlock");
            return;
        }
        var tag = Tags.FirstOrDefault(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        if (tag is null) { ShowPopup("操作失败", $"未找到绑定变量：{tagName}", "Error"); return; }
        if (!tag.IsWritable) { ShowPopup("操作失败", $"变量不可写：{tag.Name}", "Error"); return; }
        try
        {
            var current = string.Equals(tag.CurrentValue, "True", StringComparison.OrdinalIgnoreCase);
            var next = !current;
            await _opcUaService.WriteTagAsync(tag, next);
            tag.CurrentValue = next.ToString();
            EvaluateTagState(tag);
            SystemMessage = $"运行态操作：{sourceText} -> {tag.Name} = {next}";
            AddLog("运行态", SystemMessage, "Info");
            AddAudit("设备操作", tag.Name, "成功", SystemMessage);
            UpdateRuntimeVisuals();
            await RefreshNamedBindingsAsync(GetPostWriteRefreshTagNames(tag.Name));
        }
        catch (Exception ex)
        {
            SystemMessage = $"运行态操作失败：{ex.Message}";
            AddLog("运行态", SystemMessage, "Error");
        }
    }

    private async Task PulseBooleanTagAsync(string tagName, string sourceText)
    {
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作设备"; return; }
        var tag = Tags.FirstOrDefault(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        if (tag is null) { SystemMessage = $"未找到绑定变量：{tagName}"; return; }
        if (!tag.IsWritable) { SystemMessage = $"变量不可写：{tag.Name}"; return; }
        try
        {
            await _opcUaService.WriteTagAsync(tag, true);
            tag.CurrentValue = "True";
            AddLog("运行态", $"{sourceText} -> {tag.Name} = True", "Info");
            AddAudit("脉冲操作", tag.Name, "成功", sourceText);
            await _opcUaService.WriteTagAsync(tag, false);
            tag.CurrentValue = "False";
            SystemMessage = $"已执行：{sourceText}";
            UpdateRuntimeVisuals();
            await RefreshNamedBindingsAsync(GetPostWriteRefreshTagNames(tag.Name));
        }
        catch (Exception ex)
        {
            SystemMessage = $"脉冲输出失败：{ex.Message}";
            AddLog("运行态", SystemMessage, "Error");
        }
    }

    private async Task PulseOrWriteBoundTagAsync(string tagName, bool value, bool pulse, string sourceText)
    {
        if (!CanOperateDevices)
        {
            ShowPopup("权限不足", "当前权限不足，无法操作设备。", "Error");
            return;
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            ShowPopup("操作失败", "当前控件未绑定变量。", "Warning");
            return;
        }

        var tag = Tags.FirstOrDefault(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        if (tag is null)
        {
            ShowPopup("操作失败", $"未找到绑定变量：{tagName}", "Error");
            return;
        }

        if (!tag.IsWritable)
        {
            ShowPopup("操作失败", $"变量不可写：{tag.Name}", "Warning");
            return;
        }

        try
        {
            await _opcUaService.WriteTagAsync(tag, value);
            tag.CurrentValue = value.ToString();
            AddLog("运行态", $"{sourceText} -> {tag.Name} = {value}", "Info");
            AddAudit("设备操作", tag.Name, "成功", sourceText);

            if (pulse)
            {
                await Task.Delay(120);
                await _opcUaService.WriteTagAsync(tag, false);
                tag.CurrentValue = "False";
            }

            UpdateRuntimeVisuals();
            await RefreshNamedBindingsAsync(GetPostWriteRefreshTagNames(tag.Name));
        }
        catch (Exception ex)
        {
            SystemMessage = $"运行态操作失败：{ex.Message}";
            AddLog("运行态", SystemMessage, "Error");
            AddAudit("设备操作", tagName, "失败", SystemMessage);
        }
    }

    // ========== 气缸位置控制 ==========

    private async Task SetCylinderPositionAsync(ManualCylinderBlockItem? block, bool extend, string sourceText)
    {
        if (!CanOperateDevices)
        {
            SystemMessage = "当前权限不足，无法操作气缸";
            return;
        }

        {
            var commandSuffix = extend ? ".Cmd.ManuToWork" : ".Cmd.ManuToHome";
            var configuredCommandTag = block is not null
                ? FindTagByNameOrNodeId(extend ? block.WorkCommandTagName : block.HomeCommandTagName)
                : !string.IsNullOrWhiteSpace(extend ? CylinderWorkCommandTagName : CylinderHomeCommandTagName)
                    ? FindTagByNameOrNodeId(extend ? CylinderWorkCommandTagName : CylinderHomeCommandTagName)
                    : null;
            var importedCommandTag = FindBestImportedCylinderCommandTag(block, commandSuffix);
            var shouldPreferImported = IsGeneratedCylinderPlaceholder(configuredCommandTag) && importedCommandTag is not null;
            var tag = shouldPreferImported
                ? importedCommandTag
                : configuredCommandTag ?? importedCommandTag ?? Tags.FirstOrDefault(t => t.Name.Equals("Cylinder_Extend", StringComparison.OrdinalIgnoreCase));
            if (tag is null)
            {
                ShowPopup("操作失败", "未找到气缸输出变量：Cylinder_Extend / Cmd.ManuToHome / Cmd.ManuToWork", "Error");
                return;
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var isPulseCommand = tag.NodeId.EndsWith(".Cmd.ManuToWork", StringComparison.OrdinalIgnoreCase)
                    || tag.NodeId.EndsWith(".Cmd.ManuToHome", StringComparison.OrdinalIgnoreCase);
                if (isPulseCommand)
                {
                    await _opcUaService.WriteTagAsync(tag, true);
                    tag.CurrentValue = "True";
                    await Task.Delay(120);
                    await _opcUaService.WriteTagAsync(tag, false);
                    tag.CurrentValue = "False";
                    await RefreshCylinderBlockStatusAsync(block);
                }
                else
                {
                    await _opcUaService.WriteTagAsync(tag, extend);
                    tag.CurrentValue = extend.ToString();
                    SetTagValue("Cylinder_FwdLS", extend ? "True" : "False");
                    SetTagValue("Cylinder_BwdLS", extend ? "False" : "True");
                }
                // provide immediate UI feedback: mark command active and status as "动作中"
                if (block is not null)
                {
                    await RunOnUiThreadAsync(() =>
                    {
                        if (extend)
                        {
                            block.WorkCommandActive = true;
                            block.StatusText = $"{block.WorkCommandLabel}动作中";
                        }
                        else
                        {
                            block.HomeCommandActive = true;
                            block.StatusText = $"{block.HomeCommandLabel}动作中";
                        }
                        block.CurrentStateText = ResolveCylinderBlockStateText(block);
                        block.InterlockHint = ResolveCylinderBlockHintText(block);
                    });
                }

                stopwatch.Stop();
                var configuredDelay = extend ? CylinderWorkDelaySetting : CylinderHomeDelaySetting;
                var durationText = double.TryParse(configuredDelay, NumberStyles.Float, CultureInfo.InvariantCulture, out var configuredDelayTicks)
                    ? $"{Math.Max(configuredDelayTicks * 0.1, stopwatch.Elapsed.TotalSeconds):0.000} s"
                    : $"{stopwatch.Elapsed.TotalSeconds:0.000} s";
                CylinderCurrentActionTimeDisplay = durationText;
                CylinderLastActionTimeDisplay = durationText;
                CylinderActionCount += 1;
                EvaluateTagState(tag);
                SystemMessage = $"{sourceText}完成";
                AddLog("气缸控制", $"{sourceText} -> {tag.Name} = {(isPulseCommand ? "Pulse" : extend.ToString())}", "Info");
                AddAudit("气缸控制", tag.Name, "成功", sourceText);
                UpdateRuntimeVisuals();
                await RefreshCylinderBlockStatusAsync(block);

                // schedule a refresh of cylinder states after configured delay to pick up sensor updates
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var refreshDelayMs = 500;
                        if (double.TryParse(configuredDelay, NumberStyles.Float, CultureInfo.InvariantCulture, out var configuredDelayTicks))
                        {
                            refreshDelayMs = (int)Math.Max(200, configuredDelayTicks * 100);
                        }

                        await Task.Delay(refreshDelayMs + 200);
                        await RefreshCylinderBlockStatusAsync(block);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "气缸状态二次刷新失败 block={Block}", block?.DisplayName);
                    }
                });
            }
            catch (Exception ex)
            {
                SystemMessage = "气缸操作失败：" + ex.Message;
                AddLog("气缸控制", SystemMessage, "Error");
            }

            return;
        }
    }

    private async Task RefreshCylinderBlockStatusAsync(ManualCylinderBlockItem? block)
    {
        if (!_opcUaService.IsConnected || block is null)
        {
            await RunOnUiThreadAsync(() =>
            {
                RefreshManualCylinderBlockStates();
                RefreshCylinderBindingProperties();
            });
            return;
        }

        var root = ResolveCylinderBlockRoot(block);
        if (string.IsNullOrWhiteSpace(root))
        {
            await RunOnUiThreadAsync(() => RefreshManualCylinderBlockStates());
            return;
        }

        var nodeIds = new[]
        {
            block.HomeCommandTagName,
            block.WorkCommandTagName,
            block.HomeSensorTagName,
            block.WorkSensorTagName,
            block.HomeInterlockTagName,
            block.WorkInterlockTagName,
            $"{root}.DevStatus.Valve_Home",
            $"{root}.DevStatus.Valve_Work",
            $"{root}.Status.InHome",
            $"{root}.Status.InWork",
            $"{root}.Status.Error",
            $"{root}.Status.ErrorID"
        }
        .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        if (nodeIds.Count == 0)
        {
            await RunOnUiThreadAsync(() => RefreshManualCylinderBlockStates());
            return;
        }

        try
        {
            var values = await _opcUaService.ReadNodeValuesAsync(nodeIds);
            await RunOnUiThreadAsync(() =>
            {
                foreach (var nodeId in nodeIds)
                {
                    if (!values.TryGetValue(nodeId, out var value))
                    {
                        continue;
                    }

                    RecordOpcBindingString(nodeId, value);
                    var tag = FindTagByNodeId(nodeId) ?? FindTagByNameOrNodeId(nodeId);
                    if (tag is not null)
                    {
                        OnPlcReadAppliedToTag(tag, value);
                    }
                }

                RefreshManualCylinderBlockStates();
                RefreshCylinderBindingProperties();
                UpdateRuntimeVisuals();
            });
        }
        catch
        {
            await RunOnUiThreadAsync(() => RefreshManualCylinderBlockStates());
        }
    }

    private static IEnumerable<string> GetPostWriteRefreshTagNames(string primaryTagName)
    {
        if (string.IsNullOrWhiteSpace(primaryTagName))
        {
            yield break;
        }

        yield return primaryTagName;

        if (primaryTagName.Equals("Robot_Run", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Robot_Pause";
            yield return "Axis1_Enable";
            yield return "Axis1_Alarm";
        }
    }

    private async Task RefreshNamedBindingsAsync(IEnumerable<string> tagNames)
    {
        var names = tagNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
        {
            await RunOnUiThreadAsync(UpdateRuntimeVisuals);
            return;
        }

        if (!_opcUaService.IsConnected)
        {
            await RunOnUiThreadAsync(UpdateRuntimeVisuals);
            return;
        }

        var nodeIds = names
            .Select(name => FindTagByNameOrNodeId(name))
            .Where(tag => tag is not null && !string.IsNullOrWhiteSpace(tag.NodeId))
            .Select(tag => tag!.NodeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (nodeIds.Count == 0)
        {
            await RunOnUiThreadAsync(UpdateRuntimeVisuals);
            return;
        }

        try
        {
            var values = await _opcUaService.ReadNodeValuesAsync(nodeIds);
            await RunOnUiThreadAsync(() =>
            {
                foreach (var nodeId in nodeIds)
                {
                    if (!values.TryGetValue(nodeId, out var value))
                    {
                        continue;
                    }

                    RecordOpcBindingString(nodeId, value);
                    var tag = FindTagByNodeId(nodeId) ?? FindTagByNameOrNodeId(nodeId);
                    if (tag is not null)
                    {
                        OnPlcReadAppliedToTag(tag, value);
                    }
                }

                UpdateRuntimeVisuals();
            });
        }
        catch
        {
            await RunOnUiThreadAsync(UpdateRuntimeVisuals);
        }
    }

    private async Task SetExclusiveModeAsync(string targetTagName, string modeName)
    {
        if (!CanOperateDevices)
        {
            ShowPopup("权限不足", "当前权限不足，无法切换运行模式。", "Error");
            return;
        }

        var modeTags = new[] { "Mode_Debug", "Mode_DryRun", "Mode_BypassStation", "Mode_Manual", "Mode_Auto" };
        foreach (var modeTagName in modeTags)
        {
            var modeTag = Tags.FirstOrDefault(t => t.Name == modeTagName);
            if (modeTag is null || !modeTag.IsWritable) continue;
            var value = modeTagName == targetTagName;
            try
            {
                await _opcUaService.WriteTagAsync(modeTag, value);
                modeTag.CurrentValue = value.ToString();
            }
            catch
            {
                modeTag.CurrentValue = value.ToString();
            }
        }

        AddLog("模式切换", $"已切换到 {modeName}", "Info");
        AddAudit("模式切换", targetTagName, "成功", modeName);
        UpdateRuntimeVisuals();
    }

    private async Task WriteAxisPositionAsync(double position, string successMessage)
    {
        var tag = Tags.FirstOrDefault(t => t.Name == "Axis1_Pos");
        if (tag is null || !tag.IsWritable) { SystemMessage = "轴位置变量不可写"; return; }
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        try
        {
            await _opcUaService.WriteTagAsync(tag, position);
            tag.CurrentValue = position.ToString("0.###");
            EvaluateTagState(tag);
            SystemMessage = successMessage;
            AddLog("轴控制", successMessage, "Info");
            AddAudit("轴控制", "Axis1_Pos", "成功", successMessage);
            UpdateRuntimeVisuals();
        }
        catch (Exception ex)
        {
            SystemMessage = $"轴操作失败：{ex.Message}";
            AddLog("轴控制", SystemMessage, "Error");
        }
    }

    private double GetAxisCurrentPosition()
    {
        var tag = Tags.FirstOrDefault(t => t.Name == "Axis1_Pos");
        if (tag is null) return 0;
        return double.TryParse(tag.CurrentValue, out var value) ? value : 0;
    }

    // ========== 气缸状态诊断 ==========

    private string ResolveCylinderCurrentStateText()
    {
        if (IsAutoMode && !CylinderBackwardActive)
        {
            return "设备手动后气缸未复原，请把气缸复原后再切换到自动模式";
        }

        if (CylinderForwardActive && CylinderBackwardActive)
        {
            return "动作位和初始位感应器都点亮，检查感应器状态";
        }

        if (!CylinderForwardActive && !CylinderBackwardActive)
        {
            return "动作位和初始位感应器都不亮，检查感应器状态";
        }

        if (!CylinderOutputActive && CylinderForwardActive && !CylinderBackwardActive)
        {
            return "气缸前往初始位超时，检查动作位传感器状态";
        }

        if (CylinderOutputActive && CylinderBackwardActive && !CylinderForwardActive)
        {
            return "气缸前往动作位超时，检查初始位传感器状态";
        }

        if (!CylinderHomeInterlockActive && !CylinderBackwardActive)
        {
            return "初始位互锁条件未满足，检查互锁机构状态";
        }

        if (!CylinderMoveInterlockActive && !CylinderForwardActive)
        {
            return "动作位互锁条件未满足，检查互锁机构状态";
        }

        return CylinderOutputActive ? "气缸正在前往动作位" : "气缸已在初始位待命";
    }

    // ========== 气缸块管理 ==========

    private void EnsureDefaultManualCylinderBlock()
    {
        ManualCylinderBlocks.Clear();
        if (ManualCylinderBlocks.Count > 0)
        {
            return;
        }

        ManualCylinderBlocks.Add(CreateManualCylinderBlock(1, "夹紧气缸 CY01"));
        RefreshManualCylinderBlockStates();
    }

    private void ManualCylinderBlocks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ManualCylinderBlocks_CollectionChanged(sender, e));
            return;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ManualCylinderBlockItem>())
            {
                item.PropertyChanged += ManualCylinderBlockItem_PropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ManualCylinderBlockItem>())
            {
                item.PropertyChanged -= ManualCylinderBlockItem_PropertyChanged;
            }
        }

        ManualCylinderBlocksView.Refresh();
        OnPropertyChanged(nameof(ManualCylinderBlockCards));
    }

    private void ManualCylinderBlockItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ManualCylinderBlockItem_PropertyChanged(sender, e));
            return;
        }

        if (sender is ManualCylinderBlockItem block && e.PropertyName == nameof(ManualCylinderBlockItem.DisplayName))
        {
            var isVertical = IsVerticalCylinderName(block.DisplayName);
            if (block.IsVerticalNaming != isVertical)
            {
                block.IsVerticalNaming = isVertical;
            }
        }

        if (e.PropertyName == nameof(ManualCylinderBlockItem.DisplayOrder))
        {
            ManualCylinderBlocksView.Refresh();
        }

        if (e.PropertyName == nameof(ManualCylinderBlockItem.DisplayOrder)
            || e.PropertyName == nameof(ManualCylinderBlockItem.DisplayName)
            || e.PropertyName == nameof(ManualCylinderBlockItem.CylinderIndex))
        {
            OnPropertyChanged(nameof(ManualCylinderBlockCards));
        }
    }

    private void RebuildManualCylinderBlocksFromIo()
    {
        var existingBlocks = ManualCylinderBlocks
            .GroupBy(item => item.CylinderIndex)
            .ToDictionary(group => group.Key, group => CloneManualCylinderBlockForConfig(group.OrderBy(item => item.DisplayOrder).First()));

        var cylinders = ExtractCylinderDefinitionsFromIo()
            .GroupBy(item => item.CylinderIndex)
            .Select(group => group.First())
            .ToList();
        ManualCylinderBlocks.Clear();

        if (cylinders.Count == 0)
        {
            EnsureDefaultManualCylinderBlock();
            return;
        }

        foreach (var cylinder in cylinders.OrderBy(item => item.CylinderIndex))
        {
            try
            {
                if (existingBlocks.TryGetValue(cylinder.CylinderIndex, out var existing))
                {
                    cylinder.DisplayOrder = existing.DisplayOrder;
                }

                ManualCylinderBlocks.Add(cylinder);
                EnsureCylinderTagsForBlock(cylinder);
            }
            catch (Exception ex)
            {
                AddLog("气缸块", $"重建 CY{cylinder.CylinderIndex:00} 失败：{ex.Message}", "Warning");
            }
        }

        RefreshManualCylinderBlockStates();
    }

    internal async Task PersistConfigAsync(bool updateStatus)
    {
        var path = Path.Combine(GetProjectRoot(), "config", "appsettings.json");
        await _configurationService.SaveAsync(path, BuildAppConfig());
        if (!updateStatus)
        {
            return;
        }

        SystemMessage = $"配置已保存：{path}";
        AddLog("配置", SystemMessage, "Info");
    }

    private AppConfig BuildAppConfig()
    {
        return new AppConfig
        {
            Connection = Connection,
            Tags = Tags.ToList(),
            EventBindings = EventBindings.ToList(),
            IoGeneration = new IoGenerationSettings
            {
                PlcType = SelectedIoPlcTemplate,
                OperationNumber = IoOperationNumber,
                ControlDbMultiplier = _controlDbMultiplier,
                ControlDbOffset = _controlDbOffset,
                DriveDbOffset = _driveDbOffset
            },
            IoTableRows = IoTableRows.ToList(),
            IoTableSource = new IoTableSourceInfo
            {
                FilePath = _currentIoSourceFilePath,
                EncodingCodePage = _currentIoSourceEncodingCodePage,
                Headers = _currentIoSourceHeaders?.ToList() ?? new List<string>()
            },
            ManualCylinderBlocks = ManualCylinderBlocks
                .Select(CloneManualCylinderBlockForConfig)
                .ToList(),
            AxisConfigEntries = _axisConfigEntries,
            GitPull = BuildGitPullSettingsForConfig(),
            SfcPrograms = BuildSfcProgramsForConfig(),
            SfcInitProgram = BuildSfcInitProgramForConfig()
        };
    }

    private List<SfcProgramConfig> BuildSfcProgramsForConfig()
    {
        // 先把当前工位刷入字典，再序列化全部工位
        FlushCurrentSfcToDict(SfcStationNo);
        return _sfcProgramsByStation.Values.ToList();
    }

    private SfcProgramConfig BuildSfcInitProgramForConfig()
    {
        return new SfcProgramConfig
        {
            ProgramName = SfcInitProgramName,
            StationNo   = SfcInitStationNo,
            Steps = SfcInitSteps.Select(s => new SfcStepDto
            {
                StepNo              = s.StepNo,
                CompletionCondition = s.CompletionCondition,
                NextStep            = s.NextStep,
                Actions = s.Actions.Select(a => new SfcStepActionDto
                {
                    DeviceType      = a.DeviceType,
                    DeviceIndex     = a.DeviceIndex,
                    DeviceName      = a.DeviceName,
                    ActionType      = a.ActionType,
                    PointIndex      = a.PointIndex,
                    CustomCommand   = a.CustomCommand,
                    CustomCondition = a.CustomCondition
                }).ToList(),
                Branches = s.Branches.Select(b => new SfcStepBranchDto
                {
                    Condition  = b.Condition,
                    TargetStep = b.TargetStep
                }).ToList(),
                Alarms = s.AlarmEntries.Select(al => new SfcStepAlarmDto
                {
                    AlarmMessage   = al.AlarmMessage,
                    AlarmCondition = al.AlarmCondition,
                    AlarmType      = al.AlarmType
                }).ToList()
            }).ToList()
        };
    }

    private void RestoreManualCylinderBlocks(IEnumerable<ManualCylinderBlockItem>? blocks)
    {
        ManualCylinderBlocks.Clear();

        var restoredBlocks = (blocks ?? Enumerable.Empty<ManualCylinderBlockItem>())
            .Where(item => item is not null)
            .Select(CloneManualCylinderBlockForConfig)
            .GroupBy(item => item.CylinderIndex)
            .Select(group => group.OrderBy(item => item.DisplayOrder).First())
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.CylinderIndex)
            .ToList();

        var extractedDefinitions = ExtractCylinderDefinitionsFromIo()
            .GroupBy(item => item.CylinderIndex)
            .ToDictionary(group => group.Key, group => group.First());

        if (extractedDefinitions.Count > 0)
        {
            var restoredMap = restoredBlocks.ToDictionary(item => item.CylinderIndex, item => item);
            foreach (var block in extractedDefinitions.Values
                .OrderBy(item => restoredMap.TryGetValue(item.CylinderIndex, out var restored) ? restored.DisplayOrder : item.DisplayOrder)
                .ThenBy(item => item.CylinderIndex))
            {
                if (restoredMap.TryGetValue(block.CylinderIndex, out var restored))
                {
                    block.DisplayOrder = restored.DisplayOrder;
                }

                block.IsVerticalNaming = IsVerticalCylinderName(block.DisplayName);
                ManualCylinderBlocks.Add(block);
                EnsureCylinderTagsForBlock(block);
            }

            RefreshManualCylinderBlockStates();
            return;
        }

        if (restoredBlocks.Count > 0)
        {
            foreach (var block in restoredBlocks)
            {
                block.IsVerticalNaming = IsVerticalCylinderName(block.DisplayName);
                ManualCylinderBlocks.Add(block);
                EnsureCylinderTagsForBlock(block);
            }

            RefreshManualCylinderBlockStates();
            return;
        }

        if (IoTableRows.Count > 0)
        {
            RebuildManualCylinderBlocksFromIo();
            return;
        }

        EnsureDefaultManualCylinderBlock();
    }

    private static ManualCylinderBlockItem CloneManualCylinderBlockForConfig(ManualCylinderBlockItem source)
    {
        return new ManualCylinderBlockItem
        {
            CylinderIndex = source.CylinderIndex,
            DisplayOrder = source.DisplayOrder,
            DisplayName = source.DisplayName,
            HomeCommandTagName = source.HomeCommandTagName,
            WorkCommandTagName = source.WorkCommandTagName,
            HomeSensorTagName = source.HomeSensorTagName,
            WorkSensorTagName = source.WorkSensorTagName,
            HomeInterlockTagName = source.HomeInterlockTagName,
            WorkInterlockTagName = source.WorkInterlockTagName,
            HomeValueTagName = source.HomeValueTagName,
            WorkValueTagName = source.WorkValueTagName,
            HomeCommandDisplayName = source.HomeCommandDisplayName,
            WorkCommandDisplayName = source.WorkCommandDisplayName,
            HomeSensorDisplayName = source.HomeSensorDisplayName,
            WorkSensorDisplayName = source.WorkSensorDisplayName,
            HomeCommandAddress = source.HomeCommandAddress,
            WorkCommandAddress = source.WorkCommandAddress,
            HomeSensorAddress = source.HomeSensorAddress,
            WorkSensorAddress = source.WorkSensorAddress,
            IsVerticalNaming = source.IsVerticalNaming
        };
    }

    private static bool IsLegacyCylinderPresentation(ManualCylinderBlockItem item) =>
        string.IsNullOrWhiteSpace(item.HomeCommandDisplayName)
        && string.IsNullOrWhiteSpace(item.WorkCommandDisplayName)
        && string.IsNullOrWhiteSpace(item.HomeSensorDisplayName)
        && string.IsNullOrWhiteSpace(item.WorkSensorDisplayName)
        && string.IsNullOrWhiteSpace(item.HomeCommandAddress)
        && string.IsNullOrWhiteSpace(item.WorkCommandAddress)
        && string.IsNullOrWhiteSpace(item.HomeSensorAddress)
        && string.IsNullOrWhiteSpace(item.WorkSensorAddress);

    private IEnumerable<ManualCylinderBlockItem> ExtractCylinderDefinitionsFromIo()
    {
        var groups = new Dictionary<int, ManualCylinderBlockItem>();
        var inputRoleMap = new Dictionary<int, Dictionary<string, int>>();
        var outputRoleMap = new Dictionary<int, Dictionary<string, int>>();

        foreach (var row in IoTableRows)
        {
            CollectCylinderDefinition(groups, row.InputComment, row.InputAddress, true, inputRoleMap);
            CollectCylinderDefinition(groups, row.OutputComment, row.OutputAddress, false, outputRoleMap);
        }

        return groups.Values;
    }

    private void CollectCylinderDefinition(IDictionary<int, ManualCylinderBlockItem> groups, string comment, string address, bool isInput, IDictionary<int, Dictionary<string, int>> roleMap)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        var match = Regex.Match(comment, "(CY\\d{1,3})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return;
        }

        var indexText = new string(match.Value.Where(char.IsDigit).ToArray());
        if (!int.TryParse(indexText, out var index) || index <= 0)
        {
            return;
        }

        if (!groups.TryGetValue(index, out var block))
        {
            block = CreateManualCylinderBlock(index, ExtractCylinderDisplayName(comment));
            groups[index] = block;
        }
        else if (string.IsNullOrWhiteSpace(block.DisplayName) || block.DisplayName.StartsWith("气缸 ", StringComparison.Ordinal))
        {
            block.DisplayName = ExtractCylinderDisplayName(comment);
            block.IsVerticalNaming = IsVerticalCylinderName(block.DisplayName);
        }

        var motionLabel = ExtractCylinderMotionLabel(comment);
        var occurrenceIndex = ResolveCylinderDefinitionOccurrence(index, motionLabel, roleMap);

        ApplyCylinderIoDefinition(block, motionLabel, address, isInput, occurrenceIndex);
    }

    private ManualCylinderBlockItem CreateManualCylinderBlock(int index, string displayName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(displayName) ? $"气缸 CY{index:00}" : displayName;
        var root = BuildCylinderRootForOperation(index);
        return new ManualCylinderBlockItem
        {
            CylinderIndex = index,
            DisplayOrder = index,
            DisplayName = normalizedName,
            IsVerticalNaming = IsVerticalCylinderName(normalizedName),
            HomeCommandTagName = $"{root}.Cmd.ManuToHome",
            WorkCommandTagName = $"{root}.Cmd.ManuToWork",
            HomeSensorTagName = $"{root}.Status.InHome",
            WorkSensorTagName = $"{root}.Status.InWork",
            HomeInterlockTagName = $"{root}.Parm.IC_Home",
            WorkInterlockTagName = $"{root}.Parm.IC_Work",
            HomeValueTagName = string.Empty,
            WorkValueTagName = string.Empty
        };
    }

    private void RebindCylinderDbByOperation()
    {
        if (ManualCylinderBlocks.Count == 0)
        {
            return;
        }

        foreach (var block in ManualCylinderBlocks)
        {
            var cylinderIndex = ResolveCylinderIndex(block);
            var root = BuildCylinderRootForOperation(cylinderIndex);
            block.CylinderIndex = cylinderIndex;
            block.HomeCommandTagName = $"{root}.Cmd.ManuToHome";
            block.WorkCommandTagName = $"{root}.Cmd.ManuToWork";
            block.HomeSensorTagName = $"{root}.Status.InHome";
            block.WorkSensorTagName = $"{root}.Status.InWork";
            block.HomeInterlockTagName = $"{root}.Parm.IC_Home";
            block.WorkInterlockTagName = $"{root}.Parm.IC_Work";
            block.HomeValueTagName = string.Empty;
            block.WorkValueTagName = string.Empty;
            EnsureCylinderTagsForBlock(block);
        }

        RemoveObsoleteCylinderValueTags();

        if (SelectedCylinderSettingsBlock is not null)
        {
            LoadSelectedCylinderParmSettings();
        }

        RefreshManualCylinderBlockStates();
        RefreshCylinderBindingProperties();
    }

    private int ResolveCylinderIndex(ManualCylinderBlockItem block)
    {
        if (block.CylinderIndex > 0)
        {
            return block.CylinderIndex;
        }

        var root = ResolveCylinderBlockRoot(block);
        var match = CylinderRootPattern.Match(root);
        return match.Success && int.TryParse(match.Groups[1].Value, out var index) && index > 0
            ? index
            : 1;
    }

    private string BuildCylinderRootForOperation(int cylinderIndex)
    {
        var index = Math.Max(1, cylinderIndex);
        var driveDb = ResolveOperationDriveNumber(IoOperationNumber);
        return $"Application.DB{driveDb}_DriveControl.CylCtrl[{index}]";
    }

    private int ResolveOperationDriveNumber(string? operationNumber)
    {
        var controlDb = ResolveOperationBaseNumber(operationNumber);
        return controlDb + _driveDbOffset;
    }

    private void RemoveObsoleteCylinderValueTags()
    {
        var obsolete = Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag.NodeId)
                && tag.NodeId.Contains(".CylCtrl[", StringComparison.OrdinalIgnoreCase)
                && (tag.NodeId.EndsWith(".Value_Home", StringComparison.OrdinalIgnoreCase)
                    || tag.NodeId.EndsWith(".Value_Work", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var tag in obsolete)
        {
            Tags.Remove(tag);
        }
    }

    private string ExtractCylinderDisplayName(string comment)
    {
        var text = (comment ?? string.Empty).Trim();
        // 以 CY 编号作为分割点，取编号前面的完整描述作为显示名
        var cyMatch = Regex.Match(text, @"CY\d{1,3}", RegexOptions.IgnoreCase);
        if (cyMatch.Success && cyMatch.Index > 0)
        {
            var beforeCy = text[..cyMatch.Index].TrimEnd('_', '-', ' ');
            if (!string.IsNullOrWhiteSpace(beforeCy))
            {
                return beforeCy;
            }
        }

        // 回退：按旧逻辑
        var separatorIndex = FindLastConfiguredSeparatorIndex(text);
        var displayName = separatorIndex > 0 ? text[..separatorIndex] : text;
        return displayName.Trim();
    }

    private string ExtractCylinderMotionLabel(string comment)
    {
        var text = (comment ?? string.Empty).Trim();
        var separatorIndex = FindLastConfiguredSeparatorIndex(text);
        if (separatorIndex >= 0 && separatorIndex < text.Length - 1)
        {
            var separator = GetMatchedSeparator(text, separatorIndex);
            return text[(separatorIndex + separator.Length)..].Trim();
        }

        var match = Regex.Match(text, "(CY\\d{1,3})(.+)$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[2].Value.Trim('_', ' ', '-') : text;
    }

    private void ApplyCylinderIoDefinition(ManualCylinderBlockItem block, string motionLabel, string address, bool isInput, int occurrenceIndex)
    {
        var role = ResolveCylinderMotionRole(motionLabel, occurrenceIndex);

        if (isInput)
        {
            AssignCylinderSensorDefinition(block, role, motionLabel, address);
            return;
        }

        AssignCylinderCommandDefinition(block, role, motionLabel, address);
    }

    private static void AssignCylinderSensorDefinition(ManualCylinderBlockItem block, CylinderMotionRole role, string label, string address)
    {
        if (role == CylinderMotionRole.Work || (role == CylinderMotionRole.Unknown && string.IsNullOrWhiteSpace(block.WorkSensorDisplayName)))
        {
            if (string.IsNullOrWhiteSpace(block.WorkSensorDisplayName))
            {
                block.WorkSensorDisplayName = label;
            }

            block.WorkSensorAddress = AppendAddress(block.WorkSensorAddress, address);
            return;
        }

        if (role == CylinderMotionRole.Home || (role == CylinderMotionRole.Unknown && string.IsNullOrWhiteSpace(block.HomeSensorDisplayName)))
        {
            if (string.IsNullOrWhiteSpace(block.HomeSensorDisplayName))
            {
                block.HomeSensorDisplayName = label;
            }

            block.HomeSensorAddress = AppendAddress(block.HomeSensorAddress, address);
        }
    }

    private static void AssignCylinderCommandDefinition(ManualCylinderBlockItem block, CylinderMotionRole role, string label, string address)
    {
        if (role == CylinderMotionRole.Work || (role == CylinderMotionRole.Unknown && string.IsNullOrWhiteSpace(block.WorkCommandDisplayName)))
        {
            if (string.IsNullOrWhiteSpace(block.WorkCommandDisplayName))
            {
                block.WorkCommandDisplayName = label;
            }

            block.WorkCommandAddress = AppendAddress(block.WorkCommandAddress, address);
            return;
        }

        if (role == CylinderMotionRole.Home || (role == CylinderMotionRole.Unknown && string.IsNullOrWhiteSpace(block.HomeCommandDisplayName)))
        {
            if (string.IsNullOrWhiteSpace(block.HomeCommandDisplayName))
            {
                block.HomeCommandDisplayName = label;
            }

            block.HomeCommandAddress = AppendAddress(block.HomeCommandAddress, address);
        }
    }

    private int ResolveCylinderDefinitionOccurrence(int cylinderIndex, string motionLabel, IDictionary<int, Dictionary<string, int>> roleMap)
    {
        if (!roleMap.TryGetValue(cylinderIndex, out var labelMap))
        {
            labelMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            roleMap[cylinderIndex] = labelMap;
        }

        var key = string.IsNullOrWhiteSpace(motionLabel) ? "__EMPTY__" : motionLabel.Trim();
        if (!labelMap.TryGetValue(key, out var occurrenceIndex))
        {
            occurrenceIndex = labelMap.Count + 1;
            labelMap[key] = occurrenceIndex;
        }

        return occurrenceIndex;
    }

    private string NormalizeGroupedCylinderSuffix(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        foreach (var suffix in _namingRules.Cylinder.GroupedSuffixes.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            displayName = Regex.Replace(
                displayName,
                $"(CY\\d{{1,3}}){Regex.Escape(suffix)}$",
                "$1",
                RegexOptions.IgnoreCase);
        }

        return displayName;
    }

    private string NormalizeCylinderDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        var normalized = NormalizeGroupedCylinderSuffix(displayName.Trim());
        normalized = Regex.Replace(normalized, @"(CY\d{1,3})_\d+$", "$1", RegexOptions.IgnoreCase);

        var segments = _namingRules.Cylinder.SegmentSeparators
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Aggregate(
                new List<string> { normalized },
                (current, separator) => current
                    .SelectMany(part => part.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries).Select(segment => segment.Trim()))
                    .ToList());

        var coreSegment = segments.FirstOrDefault(segment => Regex.IsMatch(segment, @"CY\d{1,3}", RegexOptions.IgnoreCase))
            ?? segments.LastOrDefault(segment => !string.IsNullOrWhiteSpace(segment))
            ?? normalized;

        coreSegment = Regex.Replace(coreSegment, @"(CY\d{1,3})_\d+$", "$1", RegexOptions.IgnoreCase);
        return coreSegment.Trim();
    }

    private static string AppendAddress(string current, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return current;
        }

        if (string.IsNullOrWhiteSpace(current))
        {
            return address;
        }

        var parts = current.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()).ToArray();
        return parts.Contains(address, StringComparer.OrdinalIgnoreCase)
            ? current
            : $"{current}/{address}";
    }

    private CylinderMotionRole ResolveCylinderMotionRole(string label, int occurrenceIndex)
    {
        var occurrenceRole = ResolveCylinderMotionRoleByOccurrence(occurrenceIndex);
        if (occurrenceRole != CylinderMotionRole.Unknown)
        {
            return occurrenceRole;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return CylinderMotionRole.Unknown;
        }

        var normalized = label.Trim();
        var homeKeywords = _namingRules.Cylinder.HomeKeywords;
        if (homeKeywords.Any(normalized.Contains))
        {
            return CylinderMotionRole.Home;
        }

        var workKeywords = _namingRules.Cylinder.WorkKeywords;

        if (workKeywords.Any(normalized.Contains))
        {
            return CylinderMotionRole.Work;
        }

        return CylinderMotionRole.Unknown;
    }

    private CylinderMotionRole ResolveCylinderMotionRoleByOccurrence(int occurrenceIndex)
    {
        if (!_namingRules.Cylinder.MotionAssignmentMode.Equals("ByRowOrder", StringComparison.OrdinalIgnoreCase))
        {
            return CylinderMotionRole.Unknown;
        }

        return occurrenceIndex switch
        {
            1 => ParseConfiguredCylinderRole(_namingRules.Cylinder.FirstOccurrenceRole),
            2 => ParseConfiguredCylinderRole(_namingRules.Cylinder.SecondOccurrenceRole),
            _ => CylinderMotionRole.Unknown
        };
    }

    private static CylinderMotionRole ParseConfiguredCylinderRole(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "work" => CylinderMotionRole.Work,
            "home" => CylinderMotionRole.Home,
            _ => CylinderMotionRole.Unknown
        };

    private enum CylinderMotionRole
    {
        Unknown,
        Home,
        Work
    }

    private string ResolveCurrentCylinderWorkPositionLabel() =>
        IsVerticalCylinderName(CylinderDisplayName) ? "上升到位" : "伸出到位";

    private string ResolveCurrentCylinderHomePositionLabel() =>
        IsVerticalCylinderName(CylinderDisplayName) ? "下降到位" : "缩回到位";

    private bool IsVerticalCylinderName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && _namingRules.Cylinder.VerticalKeywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private int FindLastConfiguredSeparatorIndex(string text)
    {
        var bestIndex = -1;
        foreach (var separator in _namingRules.Cylinder.SegmentSeparators.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var index = text.LastIndexOf(separator, StringComparison.Ordinal);
            if (index > bestIndex)
            {
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private string GetMatchedSeparator(string text, int separatorIndex)
    {
        foreach (var separator in _namingRules.Cylinder.SegmentSeparators.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (separatorIndex >= 0
                && separatorIndex + separator.Length <= text.Length
                && string.Equals(text.Substring(separatorIndex, separator.Length), separator, StringComparison.Ordinal))
            {
                return separator;
            }
        }

        return "_";
    }

    private void EnsureCylinderTagsForBlock(ManualCylinderBlockItem block)
    {
        // Command tags (writable) — 确保每个气缸块的命令 tag 存在于 Tags 集合中
        EnsurePlaceholderTag(block.HomeCommandTagName, true);
        EnsurePlaceholderTag(block.WorkCommandTagName, true);
        // Sensor tags (read-only) — 确保每个气缸块的传感器反馈 tag 存在
        EnsurePlaceholderTag(block.HomeSensorTagName, false);
        EnsurePlaceholderTag(block.WorkSensorTagName, false);
        // Interlock tags
        EnsurePlaceholderTag(block.HomeInterlockTagName, false);
        EnsurePlaceholderTag(block.WorkInterlockTagName, false);
        // DevStatus / Status / Parm tags
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.DevStatus.Valve_Home", false);
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.DevStatus.Valve_Work", false);
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.Status.Error", false);
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.Status.ErrorID", false, "UInt16", "0");
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.Parm.DisableHome", true);
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.Parm.DisableWork", true);
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.Parm.Error_Delay", true);
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.Parm.Home_Delay", true);
        EnsurePlaceholderTag($"{ResolveCylinderBlockRoot(block)}.Parm.Work_Delay", true);
    }

    private void EnsurePlaceholderTag(string nodeId, bool writable, string dataType = "Boolean", string currentValue = "False")
    {
        if (FindTagByNameOrNodeId(nodeId) is not null)
        {
            return;
        }

        Tags.Add(new TagItem
        {
            Name = nodeId.Replace("Application.", string.Empty, StringComparison.OrdinalIgnoreCase),
            NodeId = nodeId,
            DataType = dataType,
            Category = "Cylinder",
            Group = "Imported",
            Direction = writable ? "Output" : "Input",
            CurrentValue = currentValue,
            Description = "由 IO 气缸块自动补齐的固定变量路径",
            IsWritable = writable
        });
    }

    private void RefreshManualCylinderBlockStates()
    {
        foreach (var block in ManualCylinderBlocks)
        {
            block.HomeActive = GetBoolTag(block.HomeSensorTagName);
            block.WorkActive = GetBoolTag(block.WorkSensorTagName);
            block.HomeInterlockActive = GetBoolTag(block.HomeInterlockTagName) || !FindTagExists(block.HomeInterlockTagName);
            block.WorkInterlockActive = GetBoolTag(block.WorkInterlockTagName) || !FindTagExists(block.WorkInterlockTagName);
            block.HomeCommandActive = GetCylinderBlockAnyBool(block, ".DevStatus.Valve_Home", ".Cmd.ManuToHome", block.HomeCommandTagName);
            block.WorkCommandActive = GetCylinderBlockAnyBool(block, ".DevStatus.Valve_Work", ".Cmd.ManuToWork", block.WorkCommandTagName);
            block.ErrorActive = GetCylinderBlockBool(block, ".Status.Error");
            block.ErrorIdText = GetCylinderBlockTagValue(block, ".Status.ErrorID", fallbackValue: "0");
            block.OutputActive = block.WorkCommandActive;
            block.StatusText = block.WorkActive ? block.WorkPositionLabel : block.HomeActive ? block.HomePositionLabel : "切换中";
            block.CurrentStateText = ResolveCylinderBlockStateText(block);
            block.InterlockHint = ResolveCylinderBlockHintText(block);
        }
    }

    private bool FindTagExists(string tagNameOrNodeId) => FindTagByNameOrNodeId(tagNameOrNodeId) is not null;

    private bool GetCylinderBlockBool(ManualCylinderBlockItem block, string primarySuffix, string? secondarySuffix = null, string? fallbackTagName = null)
    {
        foreach (var candidate in EnumerateCylinderBlockCandidateTags(block, primarySuffix, secondarySuffix, fallbackTagName))
        {
            var tag = FindTagByNodeId(candidate) ?? FindTagByNameOrNodeId(candidate);
            if (HasUsableBooleanValue(tag))
            {
                return TryParseTagBool(tag!.CurrentValue, out var boolValue) && boolValue;
            }
        }

        return false;
    }

    private bool GetCylinderBlockAnyBool(ManualCylinderBlockItem block, string primarySuffix, string? secondarySuffix = null, string? fallbackTagName = null)
    {
        foreach (var candidate in EnumerateCylinderBlockCandidateTags(block, primarySuffix, secondarySuffix, fallbackTagName))
        {
            var tag = FindTagByNodeId(candidate) ?? FindTagByNameOrNodeId(candidate);
            if (!HasUsableBooleanValue(tag))
            {
                continue;
            }

            if (TryParseTagBool(tag!.CurrentValue, out var boolValue) && boolValue)
            {
                return true;
            }
        }

        return false;
    }

    private string GetCylinderBlockTagValue(ManualCylinderBlockItem block, string primarySuffix, string? secondarySuffix = null, string? fallbackValue = "--")
    {
        foreach (var candidate in EnumerateCylinderBlockCandidateTags(block, primarySuffix, secondarySuffix))
        {
            var tag = FindTagByNodeId(candidate) ?? FindTagByNameOrNodeId(candidate);
            if (tag is null || string.IsNullOrWhiteSpace(tag.CurrentValue) || string.Equals(tag.CurrentValue, "--", StringComparison.Ordinal))
            {
                continue;
            }

            return tag.CurrentValue;
        }

        return fallbackValue ?? "--";
    }

    private string ResolveCylinderBlockStateText(ManualCylinderBlockItem block)
    {
        if (block.ErrorActive)
        {
            return ResolveCylinderBlockErrorText(block);
        }

        if (block.WorkCommandActive && !block.WorkActive)
        {
            return $"{block.WorkCommandLabel}动作中";
        }

        if (block.HomeCommandActive && !block.HomeActive)
        {
            return $"{block.HomeCommandLabel}动作中";
        }

        return "状态正常";
    }

    private string ResolveCylinderBlockHintText(ManualCylinderBlockItem block)
    {
        if (block.ErrorActive)
        {
            var errorId = string.IsNullOrWhiteSpace(block.ErrorIdText) ? "0" : block.ErrorIdText;
            return $"故障代码：{errorId}";
        }

        if (block.WorkCommandActive && !block.WorkActive)
        {
            return $"目标：{block.WorkPositionLabel}";
        }

        if (block.HomeCommandActive && !block.HomeActive)
        {
            return $"目标：{block.HomePositionLabel}";
        }

        return block.WorkActive
            ? $"{block.WorkPositionLabel}有效"
            : block.HomeActive
                ? $"{block.HomePositionLabel}有效"
                : "等待反馈";
    }

    private string ResolveCylinderBlockErrorText(ManualCylinderBlockItem block)
    {
        return TryParseCylinderErrorId(block.ErrorIdText) switch
        {
            1 => $"{block.HomeCommandLabel}超时，检查{block.WorkPositionLabel}",
            2 => $"{block.WorkCommandLabel}超时，检查{block.HomePositionLabel}",
            3 => $"{block.WorkPositionLabel}与{block.HomePositionLabel}同时有效，检查感应器状态",
            4 => $"{block.WorkPositionLabel}与{block.HomePositionLabel}均无信号，检查感应器状态",
            5 => $"{block.HomeInterlockLabel}未满足，检查互锁机构状态",
            6 => $"{block.WorkInterlockLabel}未满足，检查互锁机构状态",
            7 => "设备手动后气缸未复原，请复原后再切到自动模式",
            > 0 => $"气缸故障，代码 {block.ErrorIdText}",
            _ => "气缸故障"
        };
    }

    private static int TryParseCylinderErrorId(string? value) =>
        int.TryParse(value, out var result) ? result : 0;

    private static bool HasUsableBooleanValue(TagItem? tag) =>
        tag is not null
        && !string.IsNullOrWhiteSpace(tag.CurrentValue)
        && !string.Equals(tag.CurrentValue, "--", StringComparison.Ordinal)
        && !tag.CurrentValue.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase);

    private IEnumerable<string> EnumerateCylinderBlockCandidateTags(ManualCylinderBlockItem block, string primarySuffix, string? secondarySuffix = null, string? fallbackTagName = null)
    {
        var candidates = new List<string>();
        var root = ResolveCylinderBlockRoot(block);

        if (!string.IsNullOrWhiteSpace(root))
        {
            candidates.Add(root + primarySuffix);
            if (!string.IsNullOrWhiteSpace(secondarySuffix))
            {
                candidates.Add(root + secondarySuffix);
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackTagName))
        {
            candidates.Add(fallbackTagName);
        }

        return candidates
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveCylinderBlockRoot(ManualCylinderBlockItem block)
    {
        var sources = new[]
        {
            block.HomeCommandTagName,
            block.WorkCommandTagName,
            block.HomeSensorTagName,
            block.WorkSensorTagName,
            block.HomeInterlockTagName,
            block.WorkInterlockTagName
        };

        var suffixes = new[]
        {
            ".Cmd.ManuToHome",
            ".Cmd.ManuToWork",
            ".Status.InHome",
            ".Status.InWork",
            ".Parm.IC_Home",
            ".Parm.IC_Work",
            ".DevStatus.Valve_Home",
            ".DevStatus.Valve_Work",
            ".Value_Home",
            ".Value_Work"
        };

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            foreach (var suffix in suffixes)
            {
                if (source.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return source[..^suffix.Length];
                }
            }
        }

        return string.Empty;
    }

    // ========== 轴块管理 ==========

    private List<ManualAxisBlockItem> ExtractAxisDefinitionsFromTags()
    {
        var axisList = new List<ManualAxisBlockItem>();
        var axisPattern = new Regex(@"AxisCtrl\[(\d+)\]", RegexOptions.IgnoreCase);

        var axisTagGroups = Tags
            .Select(t =>
            {
                var m = axisPattern.Match(t.Name);
                if (!m.Success) m = axisPattern.Match(t.NodeId ?? string.Empty);
                return m.Success ? int.Parse(m.Groups[1].Value) : -1;
            })
            .Where(idx => idx >= 0)
            .Distinct()
            .OrderBy(idx => idx);

        foreach (var axisIndex in axisTagGroups)
        {
            var block = CreateAxisBlock(axisIndex, $"轴 AX{axisIndex:D2}");
            axisList.Add(block);
        }

        return axisList;
    }

    private ManualAxisBlockItem CreateAxisBlock(int axisIndex, string displayName, List<AxisPointLabel>? points = null)
    {
        var root = BuildAxisRootForOperation(axisIndex);

        var block = new ManualAxisBlockItem
        {
            AxisIndex = axisIndex,
            DisplayOrder = axisIndex,
            DisplayName = NormalizeAxisDisplayName(axisIndex, displayName),
            // 命令标签
            PowerCommandTagName = $"{root}.Cmd.Power",
            StopCommandTagName = $"{root}.Cmd.Stop",
            ManuToHomeTagName = $"{root}.Cmd.ManuToHome",
            ManuJogForwardTagName = $"{root}.Cmd.ManuJogFoward",
            ManuJogBackwardTagName = $"{root}.Cmd.ManuJogBackward",
            TeachOnTagName = $"{root}.Cmd.TeachOn",
            TeachTagName = $"{root}.Cmd.Teach",
            ManuPointTagName = $"{root}.Cmd.ManuPoint",
            PointSelectTagName = $"{root}.Cmd.PointSelect",
            AutoAbsTagName = $"{root}.Cmd.AutoABS",
            ManuPositionTagName = $"{root}.Cmd.ManuPosition",
            VelocityControlTagName = $"{root}.Cmd.VelocityControl",
            // 设备状态标签
            HomeSignalTagName = $"{root}.DevStatus.iDOG",
            PositiveLimitTagName = $"{root}.DevStatus.iLimitFor",
            NegativeLimitTagName = $"{root}.DevStatus.iLimitBack",
            AlarmSignalTagName = $"{root}.DevStatus.iAlam",
            ServoEnableFbTagName = $"{root}.DevStatus.oPowerOn",
            ResetFbTagName = $"{root}.DevStatus.oReset",
            BrakeStatusTagName = $"{root}.DevStatus.iBrake",
            // 运行状态标签
            PowerOnTagName = $"{root}.Status.PowerON",
            BusyTagName = $"{root}.Status.Busy",
            PosOkTagName = $"{root}.Status.PosOK",
            InitializedTagName = $"{root}.Status.Intialed",
            ErrorTagName = $"{root}.Status.Error",
            ErrorIdTagName = $"{root}.Status.ErrorID",
            ActualPositionTagName = $"{root}.Status.ActPosition",
            ActualVelocityTagName = $"{root}.Status.ActVelocity",
            ActualTorqueTagName = $"{root}.Status.ActTorque",
            StopPositionTagName2 = $"{root}.Status.StopPositon",
            HmiPositionTagName = $"{root}.Status.HmiPosition",
            StateTagName = $"{root}.Status.State",
            PausedTagName = $"{root}.Status.Paused",
            HomeInterlockTagName = $"{root}.Parm.IC_Home",
            JogInterlockTagName = $"{root}.Parm.IC_JOG",
            PositioningInterlockTagName = $"{root}.Parm.IC_ABS",
            SetPositionTagName = $"{root}.Parm.ManuPosition",
            SetVelocityTagName = $"{root}.Parm.ManuVelocity",
            StopPositionTagName = $"{root}.Status.StopPositon",
        };

        // 填充点位标签
        PopulatePointOptions(block, points);

        return block;
    }

    /// <summary>填充轴块的点位选项列表</summary>
    private static void PopulatePointOptions(ManualAxisBlockItem block, List<AxisPointLabel>? points)
    {
        block.PointOptions.Clear();

        if (points is { Count: > 0 })
        {
            // 使用配置表中定义的点位标签
            foreach (var p in points.OrderBy(x => x.Index))
            {
                block.PointOptions.Add(new AxisPointLabel(p.Index, $"{p.Index}: {p.Label}"));
            }
        }
        else
        {
            // 无配置时使用默认数字序号
            foreach (var i in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 25, 30, 40, 50 })
            {
                block.PointOptions.Add(new AxisPointLabel(i, i.ToString()));
            }
        }

        // 默认选中第一个
        if (block.PointOptions.Count > 0)
        {
            block.SelectedPointIndex = block.PointOptions[0].Index;
        }
    }

    private static string NormalizeAxisDisplayName(int axisIndex, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)
            || displayName.Contains("杞", StringComparison.Ordinal)
            || displayName.Contains("?AX", StringComparison.OrdinalIgnoreCase))
        {
            return $"轴 AX{axisIndex:D2}";
        }

        return displayName;
    }

    private string BuildAxisRootForOperation(int axisIndex)
    {
        var driveDb = ResolveOperationDriveNumber(IoOperationNumber);
        return $"Application.DB{driveDb}_DriveControl.AxisCtrl[{Math.Max(0, axisIndex)}]";
    }

    private void RebuildManualAxisBlocksFromIo()
    {
        List<ManualAxisBlockItem> axes;

        // 优先使用 IO 配置表"轴名称"Sheet 数据
        if (_axisConfigEntries.Count > 0)
        {
            axes = _axisConfigEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .OrderBy(e => e.Index)
                .Select(e => CreateAxisBlock(e.Index, e.Name, e.Points))
                .ToList();
        }
        else
        {
            axes = ExtractAxisDefinitionsFromTags();
        }

        ManualAxisBlocks.Clear();

        if (axes.Count == 0)
        {
            EnsureDefaultManualAxisBlock();
            return;
        }

        foreach (var axis in axes.OrderBy(a => a.AxisIndex))
        {
            try
            {
                ManualAxisBlocks.Add(axis);
                EnsureAxisTagsForBlock(axis);
            }
            catch (Exception ex)
            {
                AddLog("轴块", $"重建 AX{axis.AxisIndex:D2} 失败：{ex.Message}", "Warning");
            }
        }

        OnPropertyChanged(nameof(ManualAxisBlockCards));
        RefreshManualAxisBlockStates();
    }

    private void EnsureDefaultManualAxisBlock()
    {
        if (ManualAxisBlocks.Count > 0) return;

        var defaultAxis = CreateAxisBlock(0, "轴 AX00 (默认)");
        ManualAxisBlocks.Add(defaultAxis);
        EnsureAxisTagsForBlock(defaultAxis);
        OnPropertyChanged(nameof(ManualAxisBlockCards));
    }

    private void EnsureAxisTagsForBlock(ManualAxisBlockItem block)
    {
        // Command tags (writable)
        EnsureAxisPlaceholderTag(block.PowerCommandTagName, true);
        EnsureAxisPlaceholderTag(block.StopCommandTagName, true);
        EnsureAxisPlaceholderTag(block.ManuToHomeTagName, true);
        EnsureAxisPlaceholderTag(block.ManuJogForwardTagName, true);
        EnsureAxisPlaceholderTag(block.ManuJogBackwardTagName, true);
        EnsureAxisPlaceholderTag(block.TeachOnTagName, true);
        EnsureAxisPlaceholderTag(block.TeachTagName, true);
        EnsureAxisPlaceholderTag(block.ManuPointTagName, true);
        EnsureAxisPlaceholderTag(block.PointSelectTagName, true, "Int16", "0");
        EnsureAxisPlaceholderTag(block.AutoAbsTagName, true, "Int16", "0");
        EnsureAxisPlaceholderTag(block.ManuPositionTagName, true);
        EnsureAxisPlaceholderTag(block.VelocityControlTagName, true);
        // DevStatus tags (read-only)
        EnsureAxisPlaceholderTag(block.HomeSignalTagName, false);
        EnsureAxisPlaceholderTag(block.PositiveLimitTagName, false);
        EnsureAxisPlaceholderTag(block.NegativeLimitTagName, false);
        EnsureAxisPlaceholderTag(block.AlarmSignalTagName, false);
        EnsureAxisPlaceholderTag(block.ServoEnableFbTagName, false);
        EnsureAxisPlaceholderTag(block.ResetFbTagName, false);
        EnsureAxisPlaceholderTag(block.BrakeStatusTagName, false);
        // Status tags (read-only)
        EnsureAxisPlaceholderTag(block.PowerOnTagName, false);
        EnsureAxisPlaceholderTag(block.BusyTagName, false);
        EnsureAxisPlaceholderTag(block.PosOkTagName, false);
        EnsureAxisPlaceholderTag(block.InitializedTagName, false);
        EnsureAxisPlaceholderTag(block.ErrorTagName, false);
        EnsureAxisPlaceholderTag(block.ErrorIdTagName, false, "UInt16", "0");
        EnsureAxisPlaceholderTag(block.ActualPositionTagName, false, "Float", "0");
        EnsureAxisPlaceholderTag(block.ActualVelocityTagName, false, "Float", "0");
        EnsureAxisPlaceholderTag(block.ActualTorqueTagName, false, "Float", "0");
        EnsureAxisPlaceholderTag(block.StopPositionTagName2, false, "Float", "0");
        EnsureAxisPlaceholderTag(block.HmiPositionTagName, false, "Float", "0");
        EnsureAxisPlaceholderTag(block.StateTagName, false, "Int16", "0");
        EnsureAxisPlaceholderTag(block.PausedTagName, false);
        EnsureAxisPlaceholderTag(block.SetPositionTagName, true, "Float", "0");
        EnsureAxisPlaceholderTag(block.SetVelocityTagName, true, "Float", "0");
        EnsureAxisPlaceholderTag(block.StopPositionTagName, false, "Float", "0");
        EnsureAxisPlaceholderTag(block.HomeInterlockTagName, false);
        EnsureAxisPlaceholderTag(block.JogInterlockTagName, false);
        EnsureAxisPlaceholderTag(block.PositioningInterlockTagName, false);
    }

    private void EnsureAxisPlaceholderTag(string nodeId, bool writable, string dataType = "Boolean", string currentValue = "False")
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        if (FindTagByNameOrNodeId(nodeId) is not null) return;

        Tags.Add(new TagItem
        {
            Name = nodeId.Replace("Application.", string.Empty, StringComparison.OrdinalIgnoreCase),
            NodeId = nodeId,
            DataType = dataType,
            Category = "Axis",
            Group = "Imported",
            Direction = writable ? "Output" : "Input",
            CurrentValue = currentValue,
            Description = "由轴块自动补齐的固定变量路径",
            IsWritable = writable
        });
    }

    private void RefreshManualAxisBlockStates()
    {
        foreach (var block in ManualAxisBlocks)
        {
            // 设备状态
            block.HomeSignalActive = GetBoolTag(block.HomeSignalTagName);
            block.PositiveLimitActive = GetBoolTag(block.PositiveLimitTagName);
            block.NegativeLimitActive = GetBoolTag(block.NegativeLimitTagName);
            block.AlarmActive = GetBoolTag(block.AlarmSignalTagName);
            block.ServoEnabledFeedback = GetBoolTag(block.ServoEnableFbTagName);
            block.BrakeActive = GetBoolTag(block.BrakeStatusTagName);

            // 运行状态
            block.MotorRunning = GetBoolTag(block.BusyTagName);
            block.MotorActionDone = GetBoolTag(block.PosOkTagName);
            block.HomingComplete = GetBoolTag(block.InitializedTagName);
            block.MotorError = GetBoolTag(block.ErrorTagName);
            block.ErrorIdText = GetAxisTagValue(block.ErrorIdTagName, "0");
            block.ActualPositionDisplay = GetAxisTagValue(block.ActualPositionTagName, "0.000");
            block.ActualVelocityDisplay = GetAxisTagValue(block.ActualVelocityTagName, "0.0");
            block.ActualTorqueDisplay = GetAxisTagValue(block.ActualTorqueTagName, "0.0");

            // 互锁
            var servoOn = GetBoolTag(block.PowerOnTagName);
            block.HomeInterlock = FindTagByNameOrNodeId(block.HomeInterlockTagName) is not null
                ? GetBoolTag(block.HomeInterlockTagName)
                : servoOn && !block.MotorError;
            block.JogInterlock = FindTagByNameOrNodeId(block.JogInterlockTagName) is not null
                ? GetBoolTag(block.JogInterlockTagName)
                : servoOn && !block.MotorError;
            block.PositioningInterlock = FindTagByNameOrNodeId(block.PositioningInterlockTagName) is not null
                ? GetBoolTag(block.PositioningInterlockTagName)
                : servoOn && !block.MotorError && block.HomingComplete;

            // 停止位置 / HMI位置 / 状态码
            block.StopPositionDisplay = GetAxisTagValue(block.StopPositionTagName2, "0.000");
            if (string.IsNullOrWhiteSpace(block.StopPositionDisplay) || block.StopPositionDisplay == "0")
                block.StopPositionDisplay = GetAxisTagValue(block.StopPositionTagName, "0.000");
            block.HmiPositionDisplay = GetAxisTagValue(block.HmiPositionTagName, "0.000");
            var stateVal = 0;
            var stateStr = GetAxisTagValue(block.StateTagName, "0");
            int.TryParse(stateStr, out stateVal);
            block.StateCode = stateVal;
            block.StateCodeText = ResolveAxisStateCodeText(stateVal);

            // 状态文本
            block.StatusText = ResolveAxisStatusDisplayText(block, servoOn);
            block.CurrentStateText = ResolveAxisCurrentStateDisplayText(block, servoOn);
            block.InterlockHint = ResolveAxisInterlockHintDisplayText(block, servoOn);
        }

        if (SelectedAxisSettingsBlock is not null)
        {
            RefreshAxisBindingProperties();
        }
    }

    private static string ResolveAxisStatusDisplayText(ManualAxisBlockItem block, bool servoOn)
    {
        if (block.MotorError) return "故障";
        if (block.MotorRunning) return "运动中";
        if (block.MotorActionDone) return "到位";
        if (servoOn) return "使能";
        return "待机";
    }

    private static string ResolveAxisCurrentStateDisplayText(ManualAxisBlockItem block, bool servoOn)
    {
        if (block.MotorError) return $"轴故障，代码：{block.ErrorIdText}";
        if (block.AlarmActive) return "设备报警信号有效";
        if (block.PositiveLimitActive) return "正极限触发";
        if (block.NegativeLimitActive) return "负极限触发";
        if (block.MotorRunning) return "轴运动中，请勿重复操作";
        if (!servoOn) return "伺服未使能";
        return "状态正常";
    }

    private static string ResolveAxisInterlockHintDisplayText(ManualAxisBlockItem block, bool servoOn)
    {
        if (block.MotorError) return $"故障代码：{block.ErrorIdText}";
        if (!servoOn) return "请先执行使能操作";
        if (!block.HomingComplete) return "未回原，定位功能受限";
        return $"位置：{block.ActualPositionDisplay}  速度：{block.ActualVelocityDisplay}";
    }

    private string GetAxisTagValue(string tagName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return fallback;
        var tag = FindTagByNameOrNodeId(tagName);
        if (tag is null || string.IsNullOrWhiteSpace(tag.CurrentValue) || string.Equals(tag.CurrentValue, "--", StringComparison.Ordinal))
            return fallback;
        return tag.CurrentValue;
    }

    private static string ResolveAxisStatusText(ManualAxisBlockItem block, bool servoOn)
    {
        if (block.MotorError) return "故障";
        if (block.MotorRunning) return "运动中";
        if (block.MotorActionDone) return "到位";
        if (servoOn) return "使能";
        return "待机";
    }

    private static string ResolveAxisStateText(ManualAxisBlockItem block, bool servoOn)
    {
        if (block.MotorError) return $"轴故障 代码:{block.ErrorIdText}";
        if (block.AlarmActive) return "设备报警信号有效";
        if (block.PositiveLimitActive) return "正极限触发";
        if (block.NegativeLimitActive) return "负极限触发";
        if (block.MotorRunning) return "轴运动中，请勿操作";
        if (!servoOn) return "伺服未使能";
        return "状态正常";
    }

    private static string ResolveAxisHintText(ManualAxisBlockItem block, bool servoOn)
    {
        if (block.MotorError) return $"故障代码：{block.ErrorIdText}";
        if (!servoOn) return "请先执行使能操作";
        if (!block.HomingComplete) return "未回原，定位功能受限";
        return $"位置:{block.ActualPositionDisplay} 速度:{block.ActualVelocityDisplay}";
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisToggleEnableAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        var tag = FindTagByNameOrNodeId(block.PowerCommandTagName);
        if (tag is null || !tag.IsWritable) { SystemMessage = $"未找到轴使能变量：{block.PowerCommandTagName}"; return; }
        try
        {
            var current = TryParseTagBool(tag.CurrentValue, out var bv) && bv;
            await _opcUaService.WriteTagAsync(tag, !current);
            tag.CurrentValue = (!current).ToString();
            SystemMessage = current ? $"{block.DisplayName} 已去使能" : $"{block.DisplayName} 已使能";
            AddLog("轴控制", SystemMessage, "Info");
            AddAudit("轴控制", tag.Name, "成功", SystemMessage);
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴使能失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisStopAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        var tag = FindTagByNameOrNodeId(block.StopCommandTagName);
        if (tag is null || !tag.IsWritable) { SystemMessage = $"未找到轴停止变量：{block.StopCommandTagName}"; return; }
        try
        {
            await _opcUaService.WriteTagAsync(tag, true);
            tag.CurrentValue = "True";
            await Task.Delay(120);
            await _opcUaService.WriteTagAsync(tag, false);
            tag.CurrentValue = "False";
            SystemMessage = $"{block.DisplayName} 已停止";
            AddLog("轴控制", SystemMessage, "Info");
            AddAudit("轴控制", tag.Name, "成功", SystemMessage);
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴停止失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisMoveToHomeAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        if (!block.HomeInterlock) { ShowPopup("操作条件不满足", "回原互锁未满足，请先使能伺服并排除故障。", "Warning"); return; }
        var tag = FindTagByNameOrNodeId(block.ManuToHomeTagName);
        if (tag is null || !tag.IsWritable) { SystemMessage = $"未找到轴回原变量：{block.ManuToHomeTagName}"; return; }
        try
        {
            await _opcUaService.WriteTagAsync(tag, true);
            tag.CurrentValue = "True";
            await Task.Delay(120);
            await _opcUaService.WriteTagAsync(tag, false);
            tag.CurrentValue = "False";
            block.StatusText = "回原中";
            SystemMessage = $"{block.DisplayName} 回原点";
            AddLog("轴控制", SystemMessage, "Info");
            AddAudit("轴控制", tag.Name, "成功", SystemMessage);
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴回原失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisJogForwardAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        if (!block.JogInterlock) { ShowPopup("操作条件不满足", "点动互锁未满足，请先使能伺服并排除故障。", "Warning"); return; }
        var tag = FindTagByNameOrNodeId(block.ManuJogForwardTagName);
        if (tag is null || !tag.IsWritable) { SystemMessage = $"未找到轴正向点动变量：{block.ManuJogForwardTagName}"; return; }
        try
        {
            await _opcUaService.WriteTagAsync(tag, true);
            tag.CurrentValue = "True";
            await Task.Delay(120);
            await _opcUaService.WriteTagAsync(tag, false);
            tag.CurrentValue = "False";
            SystemMessage = $"{block.DisplayName} 正向点动";
            AddLog("轴控制", SystemMessage, "Info");
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴正向点动失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisJogBackwardAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        if (!block.JogInterlock) { ShowPopup("操作条件不满足", "点动互锁未满足，请先使能伺服并排除故障。", "Warning"); return; }
        var tag = FindTagByNameOrNodeId(block.ManuJogBackwardTagName);
        if (tag is null || !tag.IsWritable) { SystemMessage = $"未找到轴反向点动变量：{block.ManuJogBackwardTagName}"; return; }
        try
        {
            await _opcUaService.WriteTagAsync(tag, true);
            tag.CurrentValue = "True";
            await Task.Delay(120);
            await _opcUaService.WriteTagAsync(tag, false);
            tag.CurrentValue = "False";
            SystemMessage = $"{block.DisplayName} 反向点动";
            AddLog("轴控制", SystemMessage, "Info");
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴反向点动失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    // ==================== 新增轴命令 ====================

    /// <summary>启动定位 — 写入设定位置和速度，然后触发 AutoABS</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisStartPositioningAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        if (!block.PositioningInterlock) { ShowPopup("操作条件不满足", "定位互锁未满足，请先使能伺服、完成回原并排除故障。", "Warning"); return; }
        try
        {
            // 1. 写入设定位置到 HmiPosition
            if (!string.IsNullOrWhiteSpace(block.SetPositionTagName))
            {
                var posTag = FindTagByNameOrNodeId(block.SetPositionTagName);
                if (posTag is not null && posTag.IsWritable)
                {
                    if (float.TryParse(block.SetPositionInput, out var posVal))
                    {
                        await _opcUaService.WriteTagAsync(posTag, posVal);
                        posTag.CurrentValue = posVal.ToString("F3");
                    }
                }
            }

            // 2. 写入设定速度
            if (!string.IsNullOrWhiteSpace(block.SetVelocityTagName))
            {
                var velTag = FindTagByNameOrNodeId(block.SetVelocityTagName);
                if (velTag is not null && velTag.IsWritable)
                {
                    if (float.TryParse(block.SetVelocityInput, out var velVal))
                    {
                        await _opcUaService.WriteTagAsync(velTag, velVal);
                        velTag.CurrentValue = velVal.ToString();
                    }
                }
            }

            // 3. 触发 AutoABS（脉冲）
            var absTag = FindTagByNameOrNodeId(block.ManuPositionTagName);
            if (absTag is null || !absTag.IsWritable) { SystemMessage = $"未找到轴启动定位变量：{block.ManuPositionTagName}"; return; }
            await _opcUaService.WriteTagAsync(absTag, true);
            absTag.CurrentValue = "True";
            await Task.Delay(200);
            await _opcUaService.WriteTagAsync(absTag, false);
            absTag.CurrentValue = "False";

            SystemMessage = $"{block.DisplayName} 启动定位 位置={block.SetPositionInput} 速度={block.SetVelocityInput}";
            AddLog("轴控制", SystemMessage, "Info");
            AddAudit("轴控制", block.ManuPositionTagName, "成功", SystemMessage);
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴定位失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    /// <summary>设定参考点 — 写 TeachOn 脉冲</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisSetReferenceAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        var tag = FindTagByNameOrNodeId(block.ManuToHomeTagName);
        if (tag is null || !tag.IsWritable) { SystemMessage = $"未找到轴参考点变量：{block.ManuToHomeTagName}"; return; }
        try
        {
            await _opcUaService.WriteTagAsync(tag, true);
            tag.CurrentValue = "True";
            await Task.Delay(200);
            await _opcUaService.WriteTagAsync(tag, false);
            tag.CurrentValue = "False";
            SystemMessage = $"{block.DisplayName} 设定参考点";
            AddLog("轴控制", SystemMessage, "Info");
            AddAudit("轴控制", tag.Name, "成功", SystemMessage);
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴设定参考点失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    /// <summary>示教使能 — 切换 TeachOn</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisTeachEnableAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        var tag = FindTagByNameOrNodeId(block.TeachOnTagName);
        if (tag is null || !tag.IsWritable) { SystemMessage = $"未找到轴示教使能变量：{block.TeachOnTagName}"; return; }
        try
        {
            var current = TryParseTagBool(tag.CurrentValue, out var bv) && bv;
            await _opcUaService.WriteTagAsync(tag, !current);
            tag.CurrentValue = (!current).ToString();
            SystemMessage = current ? $"{block.DisplayName} 示教已关闭" : $"{block.DisplayName} 示教已开启";
            AddLog("轴控制", SystemMessage, "Info");
            AddAudit("轴控制", tag.Name, "成功", SystemMessage);
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴示教使能失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    /// <summary>写入位置（示教写入） — 写 PointSelect + Teach 脉冲</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisWritePositionAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        try
        {
            // 写 PointSelect
            var selTag = FindTagByNameOrNodeId(block.PointSelectTagName);
            if (selTag is not null && selTag.IsWritable)
            {
                await _opcUaService.WriteTagAsync(selTag, (short)block.SelectedPointIndex);
                selTag.CurrentValue = block.SelectedPointIndex.ToString();
            }

            // 写 Teach 脉冲
            var teachTag = FindTagByNameOrNodeId(block.TeachTagName);
            if (teachTag is null || !teachTag.IsWritable) { SystemMessage = $"未找到轴示教写入变量：{block.TeachTagName}"; return; }
            await _opcUaService.WriteTagAsync(teachTag, true);
            teachTag.CurrentValue = "True";
            await Task.Delay(200);
            await _opcUaService.WriteTagAsync(teachTag, false);
            teachTag.CurrentValue = "False";

            SystemMessage = $"{block.DisplayName} 写入位置 点位={block.SelectedPointIndex}";
            AddLog("轴控制", SystemMessage, "Info");
            AddAudit("轴控制", block.TeachTagName, "成功", SystemMessage);
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴写入位置失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    /// <summary>位置定位（移动到选定位置） — 写 PointSelect + ManuPoint 脉冲</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AxisMoveToPointAsync(ManualAxisBlockItem? block)
    {
        if (block is null) return;
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法操作轴"; return; }
        if (!block.PositioningInterlock) { ShowPopup("操作条件不满足", "定位互锁未满足，请先使能伺服、完成回原并排除故障。", "Warning"); return; }
        try
        {
            // 写 PointSelect
            var selTag = FindTagByNameOrNodeId(block.PointSelectTagName);
            if (selTag is not null && selTag.IsWritable)
            {
                await _opcUaService.WriteTagAsync(selTag, (short)block.SelectedPointIndex);
                selTag.CurrentValue = block.SelectedPointIndex.ToString();
            }

            // 写 ManuPoint 脉冲
            var mpTag = FindTagByNameOrNodeId(block.ManuPointTagName);
            if (mpTag is null || !mpTag.IsWritable) { SystemMessage = $"未找到轴位置定位变量：{block.ManuPointTagName}"; return; }
            await _opcUaService.WriteTagAsync(mpTag, true);
            mpTag.CurrentValue = "True";
            await Task.Delay(200);
            await _opcUaService.WriteTagAsync(mpTag, false);
            mpTag.CurrentValue = "False";

            SystemMessage = $"{block.DisplayName} 位置定位 点位={block.SelectedPointIndex}";
            AddLog("轴控制", SystemMessage, "Info");
            AddAudit("轴控制", block.ManuPointTagName, "成功", SystemMessage);
            await RefreshAxisBlockStatusAsync(block);
        }
        catch (Exception ex) { SystemMessage = $"轴位置定位失败：{ex.Message}"; AddLog("轴控制", SystemMessage, "Error"); }
    }

    private async Task RefreshAxisBlockStatusAsync(ManualAxisBlockItem? block)
    {
        if (block is null)
        {
            await RunOnUiThreadAsync(UpdateRuntimeVisuals);
            return;
        }

        var bindings = new[]
        {
            block.PowerCommandTagName,
            block.StopCommandTagName,
            block.ManuToHomeTagName,
            block.ManuJogForwardTagName,
            block.ManuJogBackwardTagName,
            block.TeachOnTagName,
            block.TeachTagName,
            block.ManuPointTagName,
            block.PointSelectTagName,
            block.ManuPositionTagName,
            block.HomeSignalTagName,
            block.PositiveLimitTagName,
            block.NegativeLimitTagName,
            block.AlarmSignalTagName,
            block.ServoEnableFbTagName,
            block.PowerOnTagName,
            block.BusyTagName,
            block.PosOkTagName,
            block.InitializedTagName,
            block.ErrorTagName,
            block.ErrorIdTagName,
            block.ActualPositionTagName,
            block.ActualVelocityTagName,
            block.ActualTorqueTagName,
            block.StateTagName,
            block.HomeInterlockTagName,
            block.JogInterlockTagName,
            block.PositioningInterlockTagName,
            block.HmiPositionTagName
        };

        await RefreshNamedBindingsAsync(bindings);
        await RunOnUiThreadAsync(RefreshAxisBindingProperties);
    }

    /// <summary>根据状态码返回描述文本</summary>
    private static string ResolveAxisStateCodeText(int stateCode)
    {
        return stateCode switch
        {
            0 => "0: Power_off",
            1 => "1: Power_on",
            2 => "2: Homing",
            3 => "3: Standstill",
            4 => "4: DiscreteMotion",
            5 => "5: ContinuousMotion",
            6 => "6: SynchronizedMotion",
            7 => "7: Stopping",
            8 => "8: ErrorStop",
            _ => $"{stateCode}: Unknown",
        };
    }
}
