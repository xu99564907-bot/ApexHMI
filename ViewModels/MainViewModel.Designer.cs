#nullable enable

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
    // ========== 设计器 / IO 生成 / 自动程序 ==========

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        await PersistConfigAsync(updateStatus: true);
    }

    private async Task LoadNamingRulesAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "naming-rules.json");
        _namingRules = await _namingRulesService.LoadOrCreateAsync(path);
    }

    [RelayCommand]
    private async Task LoadConfigAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "appsettings.json");
        var config = await _configurationService.LoadAsync(path);
        if (config is null) { SystemMessage = "未找到配置文件"; return; }
        Connection = config.Connection;
        if (string.IsNullOrWhiteSpace(Connection.Protocol))
        {
            Connection.Protocol = "OPC UA";
        }
        Tags.Clear(); foreach (var tag in config.Tags) Tags.Add(tag);
        EventBindings.Clear(); foreach (var binding in config.EventBindings) EventBindings.Add(binding);
        SelectedIoPlcTemplate = string.IsNullOrWhiteSpace(config.IoGeneration?.PlcType) ? "汇川中型PLC" : config.IoGeneration.PlcType;
        IoOperationNumber = string.IsNullOrWhiteSpace(config.IoGeneration?.OperationNumber) ? "OP10" : config.IoGeneration.OperationNumber;
        _controlDbMultiplier = config.IoGeneration?.ControlDbMultiplier > 0 ? config.IoGeneration.ControlDbMultiplier : 100;
        _controlDbOffset = config.IoGeneration?.ControlDbOffset ?? 0;
        _driveDbOffset = config.IoGeneration?.DriveDbOffset ?? 50;
        _axisConfigEntries = config.AxisConfigEntries ?? new List<AxisConfigEntry>();
        IoTableRows.Clear();
        foreach (var row in config.IoTableRows ?? new List<IoTableRow>()) IoTableRows.Add(row);

        // 恢复最近一次导入的 IO 表来源信息，使重启后仍可直接“保存 IO 表”
        var ioSource = config.IoTableSource ?? new IoTableSourceInfo();
        _currentIoSourceFilePath = ioSource.FilePath ?? string.Empty;
        _currentIoSourceEncodingCodePage = ioSource.EncodingCodePage > 0 ? ioSource.EncodingCodePage : 65001;
        _currentIoSourceHeaders = ioSource.Headers ?? new List<string>();
        _importedIoRowsSnapshot.Clear();
        _importedIoRowsSnapshot.AddRange(CloneIoRows(IoTableRows));
        _lastIoSavedSnapshot.Clear();
        _lastIoSavedFilePath = string.Empty;
        _lastIoHistoryFilePath = string.Empty;
        _lastIoSaveAt = DateTime.MinValue;
        OnPropertyChanged(nameof(CanSaveIoTable));

        RestoreManualCylinderBlocks(config.ManualCylinderBlocks);
        RebuildManualAxisBlocksFromIo();
        RebindCylinderDbByOperation();
        RestoreGitPullSettings(config.GitPull);
        RefreshIoGenerationSummary();
        OnPropertyChanged(nameof(TagCount));
        RefreshMonitorView();
        RefreshAlarmStatistics();
        SystemMessage = "配置加载完成";
        AddLog("配置", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task ImportIoTableAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入 IO 表",
            Filter = "IO 表 (*.xlsx;*.csv;*.txt)|*.xlsx;*.csv;*.txt|Excel 文件 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var importResult = await _ioTableImportService.ImportAsync(dialog.FileName);
            IoTableRows.Clear();
            foreach (var row in importResult.Rows) IoTableRows.Add(row);

            // 存储轴配置表数据
            if (importResult.AxisConfigEntries.Count > 0)
            {
                _axisConfigEntries = importResult.AxisConfigEntries;
                AddLog("IO 生成", $"已从[轴名称]Sheet 加载 {_axisConfigEntries.Count} 个轴定义。", "Info");
            }

            var inferredOperationNumber = InferOperationNumberFromIoRows(importResult.Rows);
            if (!string.IsNullOrWhiteSpace(inferredOperationNumber))
            {
                IoOperationNumber = inferredOperationNumber;
            }

            _currentIoSourceFilePath = importResult.SourceFilePath;
            _currentIoSourceEncodingCodePage = importResult.EncodingCodePage;
            _currentIoSourceHeaders = importResult.Headers;
            _importedIoRowsSnapshot.Clear();
            _importedIoRowsSnapshot.AddRange(CloneIoRows(importResult.Rows));
            _lastIoSavedSnapshot.Clear();
            _lastIoSavedFilePath = string.Empty;
            _lastIoHistoryFilePath = string.Empty;
            _lastIoSaveAt = DateTime.MinValue;
            GeneratedIoPrograms.Clear();
            SelectedGeneratedIoProgram = null;
            GeneratedIoOutputDirectory = string.Empty;
            IsRuntimeMode = false;
            SelectedTabIndex = ResolveTabIndex("设计器");
            CurrentDesignerSubSection = "手动程序生成";
            CurrentSection = "手动程序生成";
            RefreshIoGenerationSummary();
            OnPropertyChanged(nameof(CanSaveIoTable));
            SystemMessage = $"IO 表导入完成：{Path.GetFileName(dialog.FileName)}";
            AddLog("IO 生成", $"{SystemMessage}，共 {importResult.Rows.Count} 行。", "Info");
            if (!string.IsNullOrWhiteSpace(inferredOperationNumber))
            {
                AddLog("IO 生成", $"已根据导入内容自动绑定工位号：{inferredOperationNumber}（对应 DB{ResolveOperationBaseNumber(inferredOperationNumber)}）", "Info");
            }

            // 导入后立即重建气缸/轴块，使名称和点位即时生效
            try
            {
                RebuildManualCylinderBlocksFromIo();
                RebuildManualAxisBlocksFromIo();
                RebindCylinderDbByOperation();
            }
            catch (Exception rebuildEx)
            {
                AddLog("IO 生成", $"导入后重建功能块失败：{rebuildEx.Message}", "Warning");
            }

            // 立即持久化，避免重启后丢失轴配置
            try
            {
                await PersistConfigAsync(updateStatus: false);
            }
            catch (Exception persistEx)
            {
                AddLog("IO 生成", $"导入后持久化失败：{persistEx.Message}", "Warning");
            }

            await WarmupOpcNodeResolutionCacheAsync("导入IO后");
        }
        catch (Exception ex)
        {
            SystemMessage = $"IO 表导入失败：{ex.Message}";
            AddLog("IO 生成", SystemMessage, "Error");
            ShowPopup("导入失败", ex.Message, "Error");
        }
    }

    [RelayCommand]
    private void ClearIoTable()
    {
        IoTableRows.Clear();
        GeneratedIoPrograms.Clear();
        SelectedGeneratedIoProgram = null;
        GeneratedIoOutputDirectory = string.Empty;
        _currentIoSourceFilePath = string.Empty;
        _currentIoSourceEncodingCodePage = 65001;
        _currentIoSourceHeaders = new();
        _importedIoRowsSnapshot.Clear();
        _lastIoSavedSnapshot.Clear();
        _lastIoSavedFilePath = string.Empty;
        _lastIoHistoryFilePath = string.Empty;
        _lastIoSaveAt = DateTime.MinValue;
        RefreshIoGenerationSummary();
        OnPropertyChanged(nameof(CanSaveIoTable));
        SystemMessage = "IO 表已清空";
        AddLog("IO 生成", SystemMessage, "Warning");
    }

    [RelayCommand]
    private async Task SaveIoTableToSourceAsync()
    {
        if (IoTableRows.Count == 0)
        {
            ShowPopup("保存失败", "当前 IO 表为空，无需保存。", "Warning");
            return;
        }

        try
        {
            // 优先使用最近一次导入/保存的目录；若不存在则回退到应用根目录下的 config/IoTable。
            string directory;
            string extension;
            if (!string.IsNullOrWhiteSpace(_currentIoSourceFilePath))
            {
                directory = Path.GetDirectoryName(_currentIoSourceFilePath) ?? string.Empty;
                extension = Path.GetExtension(_currentIoSourceFilePath);
            }
            else
            {
                directory = string.Empty;
                extension = ".csv";
            }

            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                directory = Path.Combine(GetApplicationRoot(), "config", "IoTable");
                Directory.CreateDirectory(directory);
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".csv";
            }

            var intervalMinutes = Math.Max(1, IoSaveIntervalMinutes);
            var shouldReuseSameFile = intervalMinutes < 5
                && !string.IsNullOrWhiteSpace(_lastIoSavedFilePath)
                && File.Exists(_lastIoSavedFilePath)
                && string.Equals(Path.GetDirectoryName(_lastIoSavedFilePath), directory, StringComparison.OrdinalIgnoreCase)
                && DateTime.Now - _lastIoSaveAt <= TimeSpan.FromMinutes(intervalMinutes);

            var filePath = shouldReuseSameFile
                ? _lastIoSavedFilePath
                : Path.Combine(directory, $"IO表{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
            var historyPath = Path.Combine(directory, "IO表_修改履历.txt");
            var baselineRows = shouldReuseSameFile && _lastIoSavedSnapshot.Count > 0
                ? _lastIoSavedSnapshot
                : _importedIoRowsSnapshot;
            var currentRows = CloneIoRows(IoTableRows);
            var changeLines = BuildIoChangeLog(baselineRows, currentRows);

            await _ioTableImportService.SaveAsync(filePath, IoTableRows, _currentIoSourceHeaders, _currentIoSourceEncodingCodePage);
            if (changeLines.Count > 0)
            {
                await Compat.AppendAllTextAsync(historyPath, BuildIoHistoryEntry(filePath, changeLines), Encoding.UTF8);
            }

            _lastIoSavedSnapshot.Clear();
            _lastIoSavedSnapshot.AddRange(currentRows);
            _lastIoSavedFilePath = filePath;
            _lastIoHistoryFilePath = historyPath;
            _lastIoSaveAt = DateTime.Now;

            // 若之前没有来源文件（例如首次从配置恢复、未主动导入），用本次保存路径作为新的来源，
            // 以便下次启动后仍能直接保存到该位置。
            var sourcePathChanged = string.IsNullOrWhiteSpace(_currentIoSourceFilePath);
            if (sourcePathChanged)
            {
                _currentIoSourceFilePath = filePath;
                OnPropertyChanged(nameof(CanSaveIoTable));
            }

            SystemMessage = shouldReuseSameFile
                ? $"IO 表已更新：{Path.GetFileName(filePath)}"
                : $"IO 表已另存为：{Path.GetFileName(filePath)}";
            AddLog("IO 生成", SystemMessage, "Info");

            if (sourcePathChanged)
            {
                try
                {
                    await PersistConfigAsync(updateStatus: false);
                }
                catch (Exception persistEx)
                {
                    AddLog("IO 生成", $"保存后持久化来源路径失败：{persistEx.Message}", "Warning");
                }
            }
        }
        catch (Exception ex)
        {
            SystemMessage = $"IO 表保存失败：{ex.Message}";
            AddLog("IO 生成", SystemMessage, "Error");
            ShowPopup("保存失败", ex.Message, "Error");
        }
    }

    private static List<IoTableRow> CloneIoRows(IEnumerable<IoTableRow> rows)
    {
        return rows.Select(row => new IoTableRow
        {
            InputModule = row.InputModule,
            InputAddress = row.InputAddress,
            InputStation = row.InputStation,
            InputComment = row.InputComment,
            InputRemark = row.InputRemark,
            OutputModule = row.OutputModule,
            OutputAddress = row.OutputAddress,
            OutputStation = row.OutputStation,
            OutputComment = row.OutputComment,
            OutputRemark = row.OutputRemark
        }).ToList();
    }

    private static List<string> BuildIoChangeLog(IReadOnlyList<IoTableRow> before, IReadOnlyList<IoTableRow> after)
    {
        var changes = new List<string>();
        var max = Math.Max(before.Count, after.Count);

        for (var index = 0; index < max; index++)
        {
            var oldRow = index < before.Count ? before[index] : null;
            var newRow = index < after.Count ? after[index] : null;
            var rowNo = index + 1;

            if (oldRow is null && newRow is not null)
            {
                if (HasIoRowContent(newRow))
                {
                    changes.Add($"第 {rowNo} 行新增：输入地址 {FormatIoValue(newRow.InputAddress)}，输入注释 {FormatIoValue(newRow.InputComment)}，输出地址 {FormatIoValue(newRow.OutputAddress)}，输出注释 {FormatIoValue(newRow.OutputComment)}");
                }

                continue;
            }

            if (oldRow is not null && newRow is null)
            {
                if (HasIoRowContent(oldRow))
                {
                    changes.Add($"第 {rowNo} 行删除：输入地址 {FormatIoValue(oldRow.InputAddress)}，输入注释 {FormatIoValue(oldRow.InputComment)}，输出地址 {FormatIoValue(oldRow.OutputAddress)}，输出注释 {FormatIoValue(oldRow.OutputComment)}");
                }

                continue;
            }

            if (oldRow is null || newRow is null)
            {
                continue;
            }

            AppendIoFieldChange(changes, rowNo, "输入地址", oldRow.InputAddress, newRow.InputAddress);
            AppendIoFieldChange(changes, rowNo, "输入注释", oldRow.InputComment, newRow.InputComment, newRow.InputAddress, oldRow.InputAddress);
            AppendIoFieldChange(changes, rowNo, "输出地址", oldRow.OutputAddress, newRow.OutputAddress);
            AppendIoFieldChange(changes, rowNo, "输出注释", oldRow.OutputComment, newRow.OutputComment, newRow.OutputAddress, oldRow.OutputAddress);
        }

        return changes;
    }

    private static void AppendIoFieldChange(ICollection<string> changes, int rowNo, string fieldName, string beforeValue, string afterValue, string? currentAddress = null, string? previousAddress = null)
    {
        if (string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
        {
            return;
        }

        var addressNote = BuildIoAddressNote(fieldName, currentAddress, previousAddress);
        changes.Add($"第 {rowNo} 行 {fieldName}{addressNote}：{FormatIoValue(beforeValue)} -> {FormatIoValue(afterValue)}");
    }

    private static string BuildIoAddressNote(string fieldName, string? currentAddress, string? previousAddress)
    {
        if (!fieldName.Contains("注释", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var address = !string.IsNullOrWhiteSpace(currentAddress) ? currentAddress : previousAddress;
        return string.IsNullOrWhiteSpace(address) ? string.Empty : $"（地址 {address.Trim()}）";
    }

    private static bool HasIoRowContent(IoTableRow row)
    {
        return !string.IsNullOrWhiteSpace(row.InputAddress)
            || !string.IsNullOrWhiteSpace(row.InputComment)
            || !string.IsNullOrWhiteSpace(row.OutputAddress)
            || !string.IsNullOrWhiteSpace(row.OutputComment);
    }

    private static string FormatIoValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "（空）" : value.Trim();
    }

    private static string BuildIoHistoryEntry(string filePath, IReadOnlyCollection<string> changeLines)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 保存文件：{Path.GetFileName(filePath)}");

        if (changeLines.Count > 0)
        {
            foreach (var line in changeLines)
            {
                builder.AppendLine($"- {line}");
            }
        }

        builder.AppendLine();
        return builder.ToString();
    }

    [RelayCommand]
    private async Task GenerateIoProgramsAsync()
    {
        try
        {
            if (CanSaveIoTable)
            {
                await SaveIoTableToSourceAsync();
            }

            var ioRowsSnapshot = CloneIoRows(IoTableRows);
            var result = await _ioProgramGenerationService.GenerateAsync(
                ioRowsSnapshot,
                new IoGenerationSettings
                {
                    PlcType = SelectedIoPlcTemplate,
                    OperationNumber = IoOperationNumber,
                    ControlDbMultiplier = _controlDbMultiplier,
                    ControlDbOffset = _controlDbOffset,
                    DriveDbOffset = _driveDbOffset
                },
                GetApplicationRoot());

            await RunOnUiThreadAsync(() =>
            {
                GeneratedIoPrograms.Clear();
                foreach (var artifact in result.Artifacts)
                {
                    GeneratedIoPrograms.Add(artifact);
                }

                SelectedGeneratedIoProgram = GeneratedIoPrograms.FirstOrDefault();
                GeneratedIoOutputDirectory = result.OutputDirectory;
                RefreshIoGenerationSummary(result);
                SystemMessage = $"IO 程序已生成：{result.OutputDirectory}";
                AddLog("IO 生成", $"{SystemMessage}（输入 {result.InputCount} / 输出 {result.OutputCount}）", "Info");
            });

            try
            {
                await RunOnUiThreadAsync(RebuildManualCylinderBlocksFromIo);
            }
            catch (Exception ex)
            {
                AddLog("IO 生成", $"气缸块刷新失败：{ex.Message}", "Warning");
            }

            try
            {
                await RunOnUiThreadAsync(RebuildManualAxisBlocksFromIo);
            }
            catch (Exception ex)
            {
                AddLog("IO 生成", $"轴块刷新失败：{ex.Message}", "Warning");
            }

            try
            {
                await SyncGeneratedArtifactsToGitAsync(result);
            }
            catch (Exception ex)
            {
                AddLog("IO 生成", $"同步到 Git 目录失败：{ex.Message}", "Warning");
            }

            try
            {
                await PrepareInProShopProjectImportAsync(result);
            }
            catch (Exception ex)
            {
                AddLog("IO 生成", $"准备导入 InProShop 工程失败：{ex.Message}", "Warning");
            }

            await PersistConfigAsync(updateStatus: false);
            await WarmupOpcNodeResolutionCacheAsync("生成程序后");
        }
        catch (Exception ex)
        {
            SystemMessage = $"手动程序生成失败：{ex.Message}";
            AddLog("IO 生成", SystemMessage, "Error");
            ShowPopup("生成失败", ex.Message, "Error");
        }
    }

    [RelayCommand]
    private void ResetAutoProgramFlow()
    {
        SeedAutoProgramFlow();
        GeneratedAutoPrograms.Clear();
        SelectedGeneratedAutoProgram = null;
        GeneratedAutoOutputDirectory = string.Empty;
        SystemMessage = "自动流程已重置为标准骨架";
    }

    [RelayCommand]
    private void AddAutoProgramStep()
    {
        var nextStepNo = AutoProgramFlowNodes.Count == 0 ? 10 : AutoProgramFlowNodes.Max(x => x.StepNo) + 10;
        AutoProgramFlowNodes.Add(new AutoProgramFlowNode
        {
            StepNo = nextStepNo,
            Title = $"新步骤 {nextStepNo:000}",
            Action = "补充动作说明",
            NextStep = "END",
            Left = 70,
            Top = 80 + AutoProgramFlowNodes.Count * 130,
            Fill = "#E0F2FE"
        });
        RebuildAutoFlowLayout();
        SystemMessage = $"已新增自动流程节点 STEP {nextStepNo:000}";
    }

    [RelayCommand]
    private async Task GenerateAutoProgramsAsync()
    {
        try
        {
            var projectRoot = GetApplicationRoot();
            var outputDirectory = Path.Combine(projectRoot, "Generated", "AutoProgram");
            Directory.CreateDirectory(outputDirectory);

            var orderedNodes = AutoProgramFlowNodes.OrderBy(x => x.StepNo).ToList();
            var stationNo = ResolveStationNo(AutoProgramStation);
            var controlDb = $"DB{ResolveOperationBaseNumber(IoOperationNumber)}_Control";
            var autoTemplate = ReadGenerationTemplate("Auto.txt");
            var initTemplate = ReadGenerationTemplate("Init.txt");
            var autoProgram = BuildAutoTemplateProgram(autoTemplate, controlDb, stationNo, orderedNodes);
            var initProgram = BuildInitTemplateProgram(initTemplate, controlDb, stationNo, orderedNodes);
            var chartBuilder = new StringBuilder();
            chartBuilder.AppendLine($"流程名称：{AutoProgramName}");
            chartBuilder.AppendLine($"工位：{AutoProgramStation}");
            chartBuilder.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            chartBuilder.AppendLine();
            foreach (var node in orderedNodes)
            {
                chartBuilder.AppendLine($"{node.StepCode}  {node.Title}");
                chartBuilder.AppendLine($"动作：{node.Action}");
                chartBuilder.AppendLine($"流向：{node.NextStep}");
                chartBuilder.AppendLine();
            }

            var artifacts = new[]
            {
                CreateGeneratedArtifact(outputDirectory, $"{IoOperationNumber}_AutoRun_{AutoProgramStation}.txt", autoProgram),
                CreateGeneratedArtifact(outputDirectory, $"{IoOperationNumber}_Init_{AutoProgramStation}.txt", initProgram),
                CreateGeneratedArtifact(outputDirectory, $"{AutoProgramStation}_FlowChart.txt", chartBuilder.ToString())
            };

            GeneratedAutoPrograms.Clear();
            foreach (var artifact in artifacts)
            {
                GeneratedAutoPrograms.Add(artifact);
            }

            SelectedGeneratedAutoProgram = GeneratedAutoPrograms.FirstOrDefault();
            GeneratedAutoOutputDirectory = outputDirectory;
            RefreshAutoProgramSummary();
            SystemMessage = $"自动程序已生成：{outputDirectory}";
            AddLog("自动程序", $"{AutoProgramName} 已生成 {artifacts.Length} 个文件", "Info");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            SystemMessage = $"自动程序生成失败：{ex.Message}";
            AddLog("自动程序", SystemMessage, "Error");
            ShowPopup("生成失败", ex.Message, "Error");
        }
    }

    [RelayCommand]
    private void OpenGeneratedIoFolder()
    {
        try
        {
            _ioProgramGenerationService.OpenOutputDirectory(GeneratedIoOutputDirectory);
        }
        catch (Exception ex)
        {
            SystemMessage = $"打开生成目录失败：{ex.Message}";
            AddLog("IO 生成", SystemMessage, "Error");
            ShowPopup("打开失败", ex.Message, "Error");
        }
    }

    [RelayCommand]
    private void OpenGeneratedAutoFolder()
    {
        try
        {
            _ioProgramGenerationService.OpenOutputDirectory(GeneratedAutoOutputDirectory);
        }
        catch (Exception ex)
        {
            SystemMessage = $"打开自动程序目录失败：{ex.Message}";
            AddLog("自动程序", SystemMessage, "Error");
            ShowPopup("打开失败", ex.Message, "Error");
        }
    }

    [RelayCommand]
    private void OpenGeneratedIoFile(GeneratedProgramArtifact? artifact)
    {
        if (artifact is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = artifact.OutputPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SystemMessage = $"打开程序文件失败：{ex.Message}";
            AddLog("IO 生成", SystemMessage, "Error");
        }
    }

    // ========== 设计器元素操作 ==========

    [RelayCommand]
    private void ApplyRuntimeTemplate(string? templateName)
    {
        var template = string.IsNullOrWhiteSpace(templateName) ? SelectedRuntimeTemplate : templateName;
        if (SelectedDesignerPage is null) return;
        DesignerElements.Clear();
        switch (template)
        {
            case "主界面":
                DesignerElements.Add(CreateDesignerElement("PageButton", 40, 20, "去报警页", navigationTarget: "报警画面"));
                DesignerElements.Add(CreateDesignerElement("Motor", 40, 100, "电机1", "Y_RunLamp"));
                DesignerElements.Add(CreateDesignerElement("Cylinder", 280, 100, "气缸1", "Cylinder_Extend"));
                DesignerElements.Add(CreateDesignerElement("Robot", 520, 100, "机械手1", "Robot_Run"));
                DesignerElements.Add(CreateDesignerElement("Stopper", 760, 100, "挡停1", "Stopper_Up"));
                DesignerElements.Add(CreateDesignerElement("Axis", 1000, 100, "轴模块1", "Axis1_Pos"));
                break;
            case "监控画面":
                DesignerElements.Add(CreateDesignerElement("Indicator", 40, 80, "运行灯", "Y_RunLamp"));
                DesignerElements.Add(CreateDesignerElement("ValueDisplay", 240, 80, "轴位置", "Axis1_Pos"));
                DesignerElements.Add(CreateDesignerElement("AlarmBanner", 40, 180, "当前报警", "Alarm_EStop"));
                break;
            case "手动画面":
                DesignerElements.Add(CreateDesignerElement("Button", 40, 80, "启停电机", "Y_RunLamp"));
                DesignerElements.Add(CreateDesignerElement("Cylinder", 240, 80, "气缸动作", "Cylinder_Extend"));
                DesignerElements.Add(CreateDesignerElement("Stopper", 480, 80, "挡停动作", "Stopper_Up"));
                DesignerElements.Add(CreateDesignerElement("Robot", 720, 80, "机械手动作", "Robot_Run"));
                break;
            case "参数设定":
                DesignerElements.Add(CreateDesignerElement("Label", 40, 60, "系统参数"));
                DesignerElements.Add(CreateDesignerElement("ValueDisplay", 40, 120, "轴位置", "Axis1_Pos"));
                DesignerElements.Add(CreateDesignerElement("Label", 320, 60, "工艺参数"));
                DesignerElements.Add(CreateDesignerElement("ValueDisplay", 320, 120, "运行灯状态", "Y_RunLamp"));
                break;
            case "报警画面":
                DesignerElements.Add(CreateDesignerElement("AlarmBanner", 40, 80, "当前报警", "Alarm_EStop"));
                DesignerElements.Add(CreateDesignerElement("PageButton", 40, 20, "返回主界面", navigationTarget: "主界面"));
                break;
        }
        SyncCanvasToPage();
        SelectedDesignerElement = DesignerElements.FirstOrDefault();
        SystemMessage = $"已应用运行页模板：{template}";
    }

    [RelayCommand]
    private void AddDesignerElement(string? elementType)
    {
        if (IsRuntimeMode) { SystemMessage = "运行态下禁止编辑设计器"; return; }
        var type = string.IsNullOrWhiteSpace(elementType) ? SelectedToolboxItem : elementType;
        var count = DesignerElements.Count + 1;
        var element = CreateDesignerElement(type, 40 + (count % 5) * 30, 40 + (count % 5) * 30);
        DesignerElements.Add(element);
        SelectedDesignerElement = element;
        SyncCanvasToPage();
        CurrentDesignerSubSection = "画布设计";
        CurrentSection = "画布设计";
        SystemMessage = $"已添加模块：{type}";
        AddLog("设计器", SystemMessage, "Info");
    }

    [RelayCommand]
    private void AddDesignerElementAtDrop(string? payload)
    {
        if (IsRuntimeMode) { SystemMessage = "运行态下禁止拖拽新增控件"; return; }
        if (string.IsNullOrWhiteSpace(payload)) return;
        var parts = payload.Split('|'); if (parts.Length < 3) return;
        var type = parts[0];
        if (!double.TryParse(parts[1], out var x)) x = 40;
        if (!double.TryParse(parts[2], out var y)) y = 40;
        x = Snap(x); y = Snap(y);
        var element = CreateDesignerElement(type, x, y);
        DesignerElements.Add(element);
        SelectedDesignerElement = element;
        SyncCanvasToPage();
        SystemMessage = $"已拖拽添加模块：{type}";
        AddLog("设计器", SystemMessage, "Info");
    }

    [RelayCommand] private void StartToolboxDrag(string? tool) { if (IsRuntimeMode) return; DragToolboxItem = tool ?? string.Empty; }
    [RelayCommand] private void RemoveSelectedDesignerElement() { if (IsRuntimeMode) { SystemMessage = "运行态下禁止删除控件"; return; } if (SelectedDesignerElement is null) { SystemMessage = "请先选择要删除的控件"; return; } var name = SelectedDesignerElement.Name; DesignerElements.Remove(SelectedDesignerElement); SelectedDesignerElement = null; SyncCanvasToPage(); SystemMessage = $"已删除控件：{name}"; AddLog("设计器", SystemMessage, "Warning"); }
    [RelayCommand] private void CopySelectedDesignerElement() { if (SelectedDesignerElement is null) return; _clipboardElement = CloneElement(SelectedDesignerElement); OnPropertyChanged(nameof(HasClipboard)); SystemMessage = $"已复制控件：{SelectedDesignerElement.Name}"; }
    [RelayCommand] private void PasteDesignerElement() { if (IsRuntimeMode) { SystemMessage = "运行态下禁止粘贴控件"; return; } if (_clipboardElement is null) return; var clone = CloneElement(_clipboardElement); clone.Id = Guid.NewGuid().ToString("N"); clone.Name = _clipboardElement.Name + "_Paste"; clone.Left = Snap(clone.Left + 20); clone.Top = Snap(clone.Top + 20); DesignerElements.Add(clone); SelectedDesignerElement = clone; SyncCanvasToPage(); SystemMessage = $"已粘贴控件：{clone.Name}"; }

    [RelayCommand]
    private void SelectDesignerElement(DesignerElement? element)
    {
        if (element is null) return;
        SelectedDesignerElement = element;
        SystemMessage = $"已选中控件：{element.Name}";
        if (IsRuntimeMode) _ = ExecuteRuntimeElementActionAsync(element);
    }

    [RelayCommand]
    private async Task ExecuteRuntimeElementAsync(DesignerElement? element)
    {
        if (element is null)
        {
            return;
        }

        SelectedDesignerElement = element;
        await ExecuteRuntimeElementActionAsync(element);
    }

    [RelayCommand]
    private async Task SaveDesignerLayoutAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "designer-layout.json");
        var page = new DesignerPage { Name = DesignerPageName, CanvasWidth = DesignerCanvasWidth, CanvasHeight = DesignerCanvasHeight, Elements = DesignerElements.ToList() };
        await _designerLayoutService.SavePageAsync(path, page);
        SystemMessage = $"设计器布局已保存：{path}";
        AddLog("设计器", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task LoadDesignerLayoutAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "designer-layout.json");
        var page = await _designerLayoutService.LoadPageAsync(path);
        if (page is null) { SystemMessage = "未找到设计器布局文件"; return; }
        DesignerPageName = page.Name;
        DesignerCanvasWidth = page.CanvasWidth;
        DesignerCanvasHeight = page.CanvasHeight;
        DesignerElements.Clear();
        foreach (var element in page.Elements) DesignerElements.Add(element);
        SelectedDesignerElement = DesignerElements.FirstOrDefault();
        SystemMessage = "设计器布局加载完成";
        AddLog("设计器", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task SaveDesignerProjectAsync()
    {
        SyncCanvasToPage();
        var path = Path.Combine(GetProjectRoot(), "config", "designer-project.json");
        var project = new DesignerProject
        {
            ProjectName = DesignerProjectName,
            Pages = DesignerPages.Select(p => new DesignerPage
            {
                Name = p.Name,
                CanvasWidth = p.CanvasWidth,
                CanvasHeight = p.CanvasHeight,
                Elements = p.Elements.Select(CloneElement).ToList()
            }).ToList()
        };
        await _designerProjectService.SaveProjectAsync(path, project);
        SystemMessage = $"设计器工程已保存：{path}";
        AddLog("设计器", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task LoadDesignerProjectAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "designer-project.json");
        var project = await _designerProjectService.LoadProjectAsync(path);
        if (project is null) { SystemMessage = "未找到设计器工程文件"; return; }
        DesignerProjectName = project.ProjectName;
        DesignerPages.Clear();
        foreach (var page in project.Pages)
        {
            DesignerPages.Add(new DesignerPage
            {
                Name = page.Name,
                CanvasWidth = page.CanvasWidth,
                CanvasHeight = page.CanvasHeight,
                Elements = page.Elements.Select(CloneElement).ToList()
            });
        }
        SelectedDesignerPage = DesignerPages.FirstOrDefault();
        SystemMessage = "设计器工程加载完成";
        AddLog("设计器", SystemMessage, "Info");
    }

    [RelayCommand] private void AddNewPage() { if (IsRuntimeMode) { SystemMessage = "运行态下禁止新建页面"; return; } SyncCanvasToPage(); var page = new DesignerPage { Name = $"页面{DesignerPages.Count + 1}", CanvasWidth = 1280, CanvasHeight = 720, Elements = new() }; DesignerPages.Add(page); SelectedDesignerPage = page; SystemMessage = $"已新建页面：{page.Name}"; AddLog("设计器", SystemMessage, "Info"); }
    [RelayCommand] private void RemoveCurrentPage() { if (IsRuntimeMode) { SystemMessage = "运行态下禁止删除页面"; return; } if (SelectedDesignerPage is null) return; var page = SelectedDesignerPage; DesignerPages.Remove(page); SelectedDesignerPage = DesignerPages.FirstOrDefault(); if (SelectedDesignerPage is null) { DesignerElements.Clear(); DesignerPageName = "主界面"; DesignerCanvasWidth = 1280; DesignerCanvasHeight = 720; } SystemMessage = $"已删除页面：{page.Name}"; AddLog("设计器", SystemMessage, "Warning"); }
    [RelayCommand] private void MoveSelectedElement(string? direction) { if (SelectedDesignerElement is null || IsRuntimeMode) return; const double step = 5; switch (direction) { case "Left": SelectedDesignerElement.Left = Snap(Math.Max(0, SelectedDesignerElement.Left - step)); break; case "Right": SelectedDesignerElement.Left = Snap(SelectedDesignerElement.Left + step); break; case "Up": SelectedDesignerElement.Top = Snap(Math.Max(0, SelectedDesignerElement.Top - step)); break; case "Down": SelectedDesignerElement.Top = Snap(SelectedDesignerElement.Top + step); break; } SyncCanvasToPage(); }
    [RelayCommand] private void AlignSelectedToGrid() { if (SelectedDesignerElement is null || IsRuntimeMode) return; SelectedDesignerElement.Left = Snap(SelectedDesignerElement.Left); SelectedDesignerElement.Top = Snap(SelectedDesignerElement.Top); SelectedDesignerElement.Width = Snap(SelectedDesignerElement.Width); SelectedDesignerElement.Height = Snap(SelectedDesignerElement.Height); SyncCanvasToPage(); }

    [RelayCommand]
    private async Task ToggleRuntimeModeAsync()
    {
        IsRuntimeMode = !IsRuntimeMode;
        if (IsRuntimeMode) await RefreshTagsAsync();
    }

    [RelayCommand]
    private void NavigateToPage(string? pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            return;
        }

        var page = DesignerPages.FirstOrDefault(p => p.Name.Equals(pageName, StringComparison.OrdinalIgnoreCase));
        if (page is null)
        {
            SystemMessage = $"未找到页面：{pageName}";
            return;
        }

        SelectedDesignerPage = page;
        CurrentSection = ResolveTabIndex(CurrentSection) == 9 || string.Equals(CurrentSection, "运行页面", StringComparison.Ordinal)
            ? "运行页面"
            : $"页面切换 -> {page.Name}";
        SystemMessage = $"已切换运行页面：{page.Name}";
    }

    // ========== 设计器辅助方法 ==========

    private void SeedDesignerData()
    {
        var mainPage = new DesignerPage { Name = "主界面", CanvasWidth = 1280, CanvasHeight = 720, Elements = new() { CreateDesignerElement("PageButton", 40, 20, "去报警页", navigationTarget: "报警画面"), CreateDesignerElement("Button", 40, 80, "启动按钮", "Y_RunLamp"), CreateDesignerElement("Indicator", 220, 80, "运行灯", "Y_RunLamp"), CreateDesignerElement("ValueDisplay", 400, 80, "轴位置", "Axis1_Pos"), CreateDesignerElement("Motor", 40, 180, "电机1", "Y_RunLamp"), CreateDesignerElement("Cylinder", 260, 180, "气缸1", "Cylinder_Extend"), CreateDesignerElement("Axis", 480, 180, "轴模块1", "Axis1_Pos"), CreateDesignerElement("Robot", 720, 180, "机械手1", "Robot_Run"), CreateDesignerElement("Stopper", 960, 180, "挡停1", "Stopper_Up") } };
        var alarmPage = new DesignerPage { Name = "报警画面", CanvasWidth = 1280, CanvasHeight = 720, Elements = new() { CreateDesignerElement("PageButton", 40, 20, "返回主界面", navigationTarget: "主界面"), CreateDesignerElement("AlarmBanner", 40, 80, "当前报警", "Alarm_EStop") } };
        DesignerPages.Add(mainPage); DesignerPages.Add(alarmPage); SelectedDesignerPage = mainPage;
    }

    private void LoadPageToCanvas(DesignerPage page)
    {
        DesignerPageName = page.Name;
        DesignerCanvasWidth = page.CanvasWidth;
        DesignerCanvasHeight = page.CanvasHeight;
        DesignerElements.Clear();
        foreach (var element in page.Elements.Select(CloneElement)) DesignerElements.Add(element);
        UpdateRuntimeVisuals();
        SelectedDesignerElement = DesignerElements.FirstOrDefault();
        if (!IsRuntimeMode && CurrentDesignerSubSection == "画布设计")
        {
            CurrentSection = "画布设计";
        }
    }

    private void SyncCanvasToPage()
    {
        if (SelectedDesignerPage is null) return;
        SelectedDesignerPage.Name = DesignerPageName;
        SelectedDesignerPage.CanvasWidth = DesignerCanvasWidth;
        SelectedDesignerPage.CanvasHeight = DesignerCanvasHeight;
        SelectedDesignerPage.Elements = DesignerElements.Select(CloneElement).ToList();
    }

    private DesignerElement CreateDesignerElement(string type, double left, double top, string? text = null, string? tagBinding = null, string? navigationTarget = null) => new()
    {
        Name = $"{type}_{DesignerElements.Count + 1}",
        ElementType = type,
        Left = Snap(left),
        Top = Snap(top),
        Width = GetDefaultWidth(type),
        Height = GetDefaultHeight(type),
        Text = text ?? GetDefaultText(type),
        TagBinding = tagBinding ?? Tags.FirstOrDefault()?.Name ?? string.Empty,
        CommandBinding = type is "Button" or "Motor" or "Cylinder" or "Stopper" or "Robot" ? "ToggleBool" : string.Empty,
        NavigationTarget = navigationTarget ?? string.Empty,
        Background = GetDefaultBackground(type),
        BorderBrush = "#64748B",
        Foreground = "#FFFFFF",
        FontSize = type is "AlarmBanner" ? 18 : 14,
        SnapToGrid = true
    };

    private DesignerElement CloneElement(DesignerElement e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        ElementType = e.ElementType,
        Left = e.Left,
        Top = e.Top,
        Width = e.Width,
        Height = e.Height,
        Text = e.Text,
        Background = e.Background,
        Foreground = e.Foreground,
        BorderBrush = e.BorderBrush,
        FontSize = e.FontSize,
        TagBinding = e.TagBinding,
        CommandBinding = e.CommandBinding,
        NavigationTarget = e.NavigationTarget,
        SnapToGrid = e.SnapToGrid
    };

    private static double GetDefaultWidth(string type) => type switch { "Label" => 160, "ValueDisplay" => 200, "AlarmBanner" => 280, "Motor" => 180, "Cylinder" => 180, "Axis" => 210, "Robot" => 190, "Stopper" => 170, "PageButton" => 140, _ => 120 };
    private static double GetDefaultHeight(string type) => type switch { "AlarmBanner" => 60, "Motor" => 100, "Cylinder" => 90, "Axis" => 100, "Robot" => 100, "Stopper" => 80, _ => 40 };
    private static string GetDefaultBackground(string type) => type switch { "Button" => "#2563EB", "Indicator" => "#475569", "Label" => "#1E293B", "ValueDisplay" => "#0F766E", "AlarmBanner" => "#F59E0B", "Motor" => "#3B82F6", "Cylinder" => "#10B981", "Axis" => "#8B5CF6", "Robot" => "#EC4899", "Stopper" => "#F59E0B", "PageButton" => "#6366F1", _ => "#64748B" };
    private static string GetDefaultText(string type) => type switch
    {
        "Button" => "按钮",
        "Indicator" => "指示灯",
        "Label" => "文本标签",
        "ValueDisplay" => "数值显示",
        "AlarmBanner" => "报警条",
        "Motor" => "电机模块",
        "Cylinder" => "气缸模块",
        "Axis" => "轴模块",
        "Robot" => "机械手模块",
        "Stopper" => "挡停模块",
        "PageButton" => "页面跳转",
        _ => "控件"
    };

    // ========== 自动程序流程 ==========

    private void SeedAutoProgramFlow()
    {
        AutoProgramFlowNodes.Clear();
        AutoProgramFlowNodes.Add(new AutoProgramFlowNode { StepNo = 10, Title = "上料检测", Action = "检测载具到位与产品存在信号", NextStep = "20", Left = 70, Top = 40, Fill = "#DBEAFE" });
        AutoProgramFlowNodes.Add(new AutoProgramFlowNode { StepNo = 20, Title = "夹紧定位", Action = "下压夹爪并确认夹紧完成", NextStep = "30", Left = 70, Top = 170, Fill = "#DCFCE7" });
        AutoProgramFlowNodes.Add(new AutoProgramFlowNode { StepNo = 30, Title = "机械手取放", Action = "机械手取料并放入加工位", NextStep = "40", Left = 70, Top = 300, Fill = "#FEF3C7" });
        AutoProgramFlowNodes.Add(new AutoProgramFlowNode { StepNo = 40, Title = "装配执行", Action = "执行装配动作并监视超时", NextStep = "50", Left = 70, Top = 430, Fill = "#FCE7F3" });
        AutoProgramFlowNodes.Add(new AutoProgramFlowNode { StepNo = 50, Title = "结果判定", Action = "判定 OK/NG 并写入结果", NextStep = "60", Left = 70, Top = 560, Fill = "#E0E7FF", IsDecision = true });
        AutoProgramFlowNodes.Add(new AutoProgramFlowNode { StepNo = 60, Title = "下料复位", Action = "下料并将流程复位到待机", NextStep = "END", Left = 70, Top = 690, Fill = "#F1F5F9" });
        RefreshAutoProgramSummary();
    }

    private void RebuildAutoFlowLayout()
    {
        var ordered = AutoProgramFlowNodes.OrderBy(x => x.StepNo).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Left = ordered[index].IsDecision ? 110 : 70;
            ordered[index].Top = 40 + index * 130;
        }

        RefreshAutoProgramSummary();
    }

    private GeneratedProgramArtifact CreateGeneratedArtifact(string outputDirectory, string fileName, string content)
    {
        var fullPath = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(fullPath, content, new UTF8Encoding(false));
        return new GeneratedProgramArtifact
        {
            DisplayName = Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            OutputPath = fullPath,
            Content = content
        };
    }

    private string ReadGenerationTemplate(string templateFileName)
    {
        var templateDirectory = Path.Combine(GetApplicationRoot(), "Templates", SelectedIoPlcTemplate);
        var templatePath = Path.Combine(templateDirectory, templateFileName);
        return File.Exists(templatePath) ? File.ReadAllText(templatePath, Encoding.UTF8) : string.Empty;
    }

    private string BuildAutoTemplateProgram(string template, string controlDb, int stationNo, IReadOnlyList<AutoProgramFlowNode> nodes)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var startStep = nodes.FirstOrDefault()?.StepNo ?? 10;
        var stepCases = new StringBuilder();
        foreach (var node in nodes)
        {
            stepCases.AppendLine($"\t{node.StepNo}:");
            stepCases.AppendLine($"\t\tAuto[{stationNo}].Comment:=\"{node.Title} - {node.Action}\";");
            stepCases.AppendLine($"\t\tAuto[{stationNo}].Step:={ResolveNextStepValue(node.NextStep, node.StepNo)};");
        }

        var result = template
            .Replace("{StationNo}", stationNo.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{GeneratedAt}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), StringComparison.Ordinal)
            .Replace("{ControlDb}", controlDb, StringComparison.Ordinal)
            .Replace("Auto[{StationNo}].Step:=10;", $"Auto[{stationNo}].Step:={startStep};", StringComparison.Ordinal)
            .Replace("\t10:\r\n\t\tAuto[{StationNo}].Comment:=\"缁涘绶熷銉ㄥ濮濄儵顎冮柊宥囩枂\";\r\n\t\tAuto[{StationNo}].Step:=1000;", stepCases.ToString().TrimEnd(), StringComparison.Ordinal)
            .Replace("Auto[{StationNo}].Comment:=\"自动流程启动\";", $"Auto[{stationNo}].Comment:=\"{AutoProgramName} 启动\";", StringComparison.Ordinal);

        return result;
    }

    private string BuildInitTemplateProgram(string template, string controlDb, int stationNo, IReadOnlyList<AutoProgramFlowNode> nodes)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var summaryComment = nodes.Count == 0 ? "等待初始化条件" : $"初始化 {AutoProgramName}，共 {nodes.Count} 步";
        var result = template
            .Replace("{StationNo}", stationNo.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{GeneratedAt}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), StringComparison.Ordinal)
            .Replace("{ControlDb}", controlDb, StringComparison.Ordinal)
            .Replace("Init[{StationNo}].Comment:=\"初始化开始\";", $"Init[{stationNo}].Comment:=\"{AutoProgramName} 初始化开始\";", StringComparison.Ordinal)
            .Replace("Init[{StationNo}].Comment:=\"初始化摘要\";", $"Init[{stationNo}].Comment:=\"{summaryComment}\";", StringComparison.Ordinal)
            .Replace("Init[{StationNo}].Comment:=\"初始化完成\";", $"Init[{stationNo}].Comment:=\"{AutoProgramName} 初始化完成\";", StringComparison.Ordinal);

        return result;
    }

    private static string ResolveNextStepValue(string? nextStep, int currentStep)
    {
        if (string.IsNullOrWhiteSpace(nextStep))
        {
            return "10";
        }

        var trimmed = nextStep.Trim();
        if (trimmed.Equals("END", StringComparison.OrdinalIgnoreCase))
        {
            return "10";
        }

        return int.TryParse(trimmed, out var stepNo) ? stepNo.ToString(CultureInfo.InvariantCulture) : (currentStep + 10).ToString(CultureInfo.InvariantCulture);
    }

    private int ResolveOperationBaseNumber(string? operationNumber)
    {
        if (string.IsNullOrWhiteSpace(operationNumber))
        {
            return 10 * Math.Max(1, _controlDbMultiplier) + _controlDbOffset;
        }

        var digits = new string(operationNumber.Where(char.IsDigit).ToArray());
        var opNo = int.TryParse(digits, out var number) && number > 0 ? number : 10;
        return opNo * Math.Max(1, _controlDbMultiplier) + _controlDbOffset;
    }

    private string? InferOperationNumberFromIoRows(IEnumerable<IoTableRow> rows)
    {
        var candidates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var multiplier = Math.Max(1, _controlDbMultiplier);
        var controlOffset = _controlDbOffset;
        var driveOffset = _driveDbOffset;

        foreach (var row in rows)
        {
            var fragments = new[]
            {
                row.InputModule,
                row.InputStation,
                row.InputComment,
                row.InputRemark,
                row.OutputModule,
                row.OutputStation,
                row.OutputComment,
                row.OutputRemark
            };

            foreach (var fragment in fragments.Where(text => !string.IsNullOrWhiteSpace(text)))
            {
                foreach (Match match in OperationNumberPattern.Matches(fragment))
                {
                    if (!match.Success || match.Groups.Count < 2)
                    {
                        continue;
                    }

                    var digits = match.Groups[1].Value;
                    if (!int.TryParse(digits, out var opNo) || opNo <= 0)
                    {
                        continue;
                    }

                    var key = $"OP{opNo:00}";
                    candidates[key] = candidates.TryGetValue(key, out var count) ? count + 1 : 1;
                }

                foreach (Match match in DbNumberPattern.Matches(fragment))
                {
                    if (!match.Success || match.Groups.Count < 2)
                    {
                        continue;
                    }

                    var digits = match.Groups[1].Value;
                    if (!int.TryParse(digits, out var dbNo) || dbNo <= 0)
                    {
                        continue;
                    }

                    // DB 与 OP 的映射规则（可配置）：
                    // Control DB: DB = OP * multiplier + controlOffset
                    // Drive DB:   DB = ControlDB + driveOffset
                    int opNo = -1;
                    var candidateByControl = dbNo - controlOffset;
                    if (candidateByControl >= 0 && candidateByControl % multiplier == 0)
                    {
                        opNo = candidateByControl / multiplier;
                    }

                    var candidateByDrive = dbNo - controlOffset - driveOffset;
                    if (opNo <= 0 && candidateByDrive >= 0 && candidateByDrive % multiplier == 0)
                    {
                        opNo = candidateByDrive / multiplier;
                    }

                    if (opNo <= 0)
                    {
                        continue;
                    }

                    var key = $"OP{opNo:00}";
                    candidates[key] = candidates.TryGetValue(key, out var count) ? count + 1 : 1;
                }
            }
        }

        return candidates
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key)
            .FirstOrDefault();
    }

    private static int ResolveStationNo(string? stationText)
    {
        if (string.IsNullOrWhiteSpace(stationText))
        {
            return 1;
        }

        var digits = new string(stationText.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var stationNo) && stationNo > 0 ? stationNo : 1;
    }

    private void RefreshAutoProgramSummary()
    {
        OnPropertyChanged(nameof(AutoProgramHeadline));
        OnPropertyChanged(nameof(AutoProgramSummary));
        OnPropertyChanged(nameof(HasGeneratedAutoPrograms));
        OnPropertyChanged(nameof(SelectedGeneratedAutoProgramContent));
    }

    private void RefreshIoGenerationSummary(IoGenerationResult? result = null)
    {
        var inputCount = result?.InputCount ?? IoTableRows.Count(r => !string.IsNullOrWhiteSpace(r.InputAddress));
        var outputCount = result?.OutputCount ?? IoTableRows.Count(r => !string.IsNullOrWhiteSpace(r.OutputAddress));
        IoImportSummary = IoTableRows.Count == 0
            ? "尚未导入 IO 表"
            : $"已导入 {IoTableRows.Count} 行，输入 {inputCount} 点，输出 {outputCount} 点";
        OnPropertyChanged(nameof(IoGenerationHeadline));
        OnPropertyChanged(nameof(IoGenerationCountSummary));
        OnPropertyChanged(nameof(HasGeneratedIoPrograms));
        OnPropertyChanged(nameof(SelectedGeneratedIoProgramContent));
    }

    private async Task WarmupOpcNodeResolutionCacheAsync(string source)
    {
        if (!_opcUaService.IsConnected)
        {
            return;
        }

        try
        {
            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in Tags)
            {
                if (string.IsNullOrWhiteSpace(tag.NodeId))
                {
                    continue;
                }

                if (tag.NodeId.StartsWith("Application.", StringComparison.OrdinalIgnoreCase)
                    || tag.NodeId.Contains(".CylCtrl[", StringComparison.OrdinalIgnoreCase)
                    || tag.NodeId.Contains(".AxisCtrl[", StringComparison.OrdinalIgnoreCase)
                    || tag.NodeId.Contains(".VacCtrl[", StringComparison.OrdinalIgnoreCase))
                {
                    nodeIds.Add(tag.NodeId);
                }
            }

            foreach (var block in ManualCylinderBlocks)
            {
                var root = ResolveCylinderBlockRoot(block);
                nodeIds.Add(block.HomeCommandTagName);
                nodeIds.Add(block.WorkCommandTagName);
                nodeIds.Add(block.HomeSensorTagName);
                nodeIds.Add(block.WorkSensorTagName);
                nodeIds.Add(block.HomeInterlockTagName);
                nodeIds.Add(block.WorkInterlockTagName);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    nodeIds.Add($"{root}.DevStatus.Valve_Home");
                    nodeIds.Add($"{root}.DevStatus.Valve_Work");
                    nodeIds.Add($"{root}.Status.InHome");
                    nodeIds.Add($"{root}.Status.InWork");
                    nodeIds.Add($"{root}.Status.Error");
                    nodeIds.Add($"{root}.Status.ErrorID");
                }
            }

            foreach (var block in ManualAxisBlocks)
            {
                nodeIds.Add(block.PowerCommandTagName);
                nodeIds.Add(block.StopCommandTagName);
                nodeIds.Add(block.ManuToHomeTagName);
                nodeIds.Add(block.ManuJogForwardTagName);
                nodeIds.Add(block.ManuJogBackwardTagName);
                nodeIds.Add(block.ManuPositionTagName);
                nodeIds.Add(block.PointSelectTagName);
                nodeIds.Add(block.ManuPointTagName);
                nodeIds.Add(block.HomeSignalTagName);
                nodeIds.Add(block.PositiveLimitTagName);
                nodeIds.Add(block.NegativeLimitTagName);
                nodeIds.Add(block.ServoEnableFbTagName);
                nodeIds.Add(block.PowerOnTagName);
                nodeIds.Add(block.BusyTagName);
                nodeIds.Add(block.PosOkTagName);
                nodeIds.Add(block.InitializedTagName);
                nodeIds.Add(block.ErrorTagName);
                nodeIds.Add(block.ErrorIdTagName);
                nodeIds.Add(block.ActualPositionTagName);
                nodeIds.Add(block.ActualVelocityTagName);
                nodeIds.Add(block.ActualTorqueTagName);
                nodeIds.Add(block.StateTagName);
            }

            var warmupTargets = nodeIds.Where(nodeId => !string.IsNullOrWhiteSpace(nodeId)).ToList();
            if (warmupTargets.Count == 0)
            {
                return;
            }

            await _opcUaService.WarmupNodeIdsAsync(warmupTargets);
            AddLog("OPC UA", $"{source}已预解析 {warmupTargets.Count} 个节点并写入缓存。", "Info");
        }
        catch (Exception ex)
        {
            AddLog("OPC UA", $"{source}预解析节点失败：{ex.Message}", "Warning");
        }
    }

    private void DesignerElements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncCanvasToPage();
    private void DesignerPages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (DesignerPages.Count > 0 && SelectedDesignerPage is null) SelectedDesignerPage = DesignerPages[0]; }
}
