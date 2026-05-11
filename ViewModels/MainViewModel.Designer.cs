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
using ApexHMI.Models.Sfc;
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
        RestoreAllSfcStations(config);
        RestoreAllSfcInitStations(config);
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

            // D1: 导入后立即校验地址重复 / 类型不匹配
            var errors = ValidateIoTable();
            if (errors > 0)
            {
                ShowPopup("IO 表存在异常",
                    $"已标记 {errors} 处异常行（红底）。请修复后再点【生成程序】，否则将被阻止。",
                    "Warning");
            }
        }
        catch (Exception ex)
        {
            SystemMessage = $"IO 表导入失败：{ex.Message}";
            AddLog("IO 生成", SystemMessage, "Error");
            ShowPopup("导入失败", ex.Message, "Error");
        }
    }

    /// <summary>
    /// D1 校验：地址重复 / 类型不匹配（DI 地址出现在 DO 列等）。
    /// 返回标记的异常行数；同时把每行的 HasError + ValidationError 填好。
    /// </summary>
    internal int ValidateIoTable()
    {
        // 先清空旧标记
        foreach (var row in IoTableRows)
        {
            row.HasError = false;
            row.ValidationError = string.Empty;
        }

        // 收集所有非空地址 → 按地址索引到（行,角色 = "I"/"O"）
        var inputMap = new Dictionary<string, List<IoTableRow>>(StringComparer.OrdinalIgnoreCase);
        var outputMap = new Dictionary<string, List<IoTableRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in IoTableRows)
        {
            var ia = (row.InputAddress ?? string.Empty).Trim();
            var oa = (row.OutputAddress ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(ia))
            {
                if (!inputMap.TryGetValue(ia, out var list)) { list = new List<IoTableRow>(); inputMap[ia] = list; }
                list.Add(row);
            }
            if (!string.IsNullOrEmpty(oa))
            {
                if (!outputMap.TryGetValue(oa, out var list)) { list = new List<IoTableRow>(); outputMap[oa] = list; }
                list.Add(row);
            }
        }

        var errorCount = 0;
        // 输入地址重复
        foreach (var kv in inputMap.Where(p => p.Value.Count > 1))
        {
            foreach (var r in kv.Value)
            {
                r.HasError = true;
                r.ValidationError = AppendError(r.ValidationError, $"输入地址重复：{kv.Key}");
                errorCount++;
            }
        }
        // 输出地址重复
        foreach (var kv in outputMap.Where(p => p.Value.Count > 1))
        {
            foreach (var r in kv.Value)
            {
                r.HasError = true;
                r.ValidationError = AppendError(r.ValidationError, $"输出地址重复：{kv.Key}");
                errorCount++;
            }
        }
        // 类型不匹配：同一地址既出现在输入又出现在输出
        foreach (var key in inputMap.Keys.Intersect(outputMap.Keys, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var r in inputMap[key].Concat(outputMap[key]).Distinct())
            {
                r.HasError = true;
                r.ValidationError = AppendError(r.ValidationError, $"地址 {key} 同时被用作输入和输出");
                errorCount++;
            }
        }
        return errorCount;
    }

    private static string AppendError(string existing, string add)
        => string.IsNullOrEmpty(existing) ? add : existing + "; " + add;

    /// <summary>
    /// D2: 选中生成的 ST 文件，弹层 side-by-side 显示 Git 当前版本 vs 本次生成。
    /// 在 Git 目录中按文件名递归查找匹配文件；找不到 Git 版本时左侧为空提示。
    /// </summary>
    [RelayCommand]
    private void ShowGeneratedDiff(GeneratedProgramArtifact? artifact)
    {
        artifact ??= SelectedGeneratedIoProgram;
        if (artifact is null)
        {
            ShowPopup("差异预览", "请先在程序预览下拉中选择一个 ST 文件", "Warning");
            return;
        }

        var vm = new ApexHMI.Views.Dialogs.ProgramDiffDialog.DiffViewModel
        {
            RightPath = artifact.OutputPath,
            RightContent = artifact.Content
        };

        var gitFolder = ResolveEffectiveGitFolder();
        if (!string.IsNullOrWhiteSpace(gitFolder) && System.IO.Directory.Exists(gitFolder))
        {
            try
            {
                var match = System.IO.Directory
                    .EnumerateFiles(gitFolder, artifact.FileName, System.IO.SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (match is not null)
                {
                    vm.LeftPath = match;
                    vm.LeftContent = System.IO.File.ReadAllText(match);
                }
                else
                {
                    vm.LeftPath = $"未在 Git 目录中找到 {artifact.FileName}";
                    vm.LeftContent = "(Git 目录中没有同名文件，可能是首次生成或路径不匹配)";
                }
            }
            catch (System.Exception ex)
            {
                vm.LeftContent = $"读取 Git 文件失败：{ex.Message}";
            }
        }
        else
        {
            vm.LeftPath = "(Git 目录未配置)";
            vm.LeftContent = "请先在 Git 拉取面板配置仓库目录，再使用差异预览。";
        }

        vm.Summary = string.Equals(vm.LeftContent, vm.RightContent, System.StringComparison.Ordinal)
            ? "✅ 内容一致"
            : $"⚠ 内容存在差异（左 {vm.LeftContent.Length} 字符 / 右 {vm.RightContent.Length} 字符）";

        var dlg = new ApexHMI.Views.Dialogs.ProgramDiffDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow,
            DataContext = vm
        };
        dlg.ShowDialog();
    }

    // D3 多工位批量生成：CSV 输入（如 "OP30,OP40,OP50"），逐个设 IoOperationNumber 后跑生成
    [ObservableProperty] private string ioBatchOperationNumbers = string.Empty;

    [RelayCommand]
    private async Task BatchGenerateIoProgramsAsync()
    {
        var raw = (IoBatchOperationNumbers ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            ShowPopup("批量生成", "请先填写工位号 CSV，例如：OP30,OP40,OP50", "Warning");
            return;
        }

        var ops = raw.Split(new[] { ',', '，', ';', '；', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();
        if (ops.Count == 0) return;

        var originalOp = IoOperationNumber;
        var ok = 0;
        var fail = 0;
        foreach (var op in ops)
        {
            try
            {
                IoOperationNumber = op;
                AddLog("IO 生成", $"批量生成 → 切换到 {op}", "Info");
                await GenerateIoProgramsAsync();
                ok++;
            }
            catch (System.Exception ex)
            {
                AddLog("IO 生成", $"工位 {op} 生成失败：{ex.Message}", "Error");
                fail++;
            }
        }

        IoOperationNumber = originalOp;
        SystemMessage = $"批量生成完成：成功 {ok} 个，失败 {fail} 个";
        AddLog("IO 生成", SystemMessage, fail > 0 ? "Warning" : "Info");
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
            // D1: 生成前重新校验，存在异常则阻止
            var errs = ValidateIoTable();
            if (errs > 0)
            {
                ShowPopup("生成被阻止", $"IO 表存在 {errs} 处异常（地址重复 / 类型不匹配），红底标记的行需要先修复。", "Error");
                return;
            }

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
                    DriveDbOffset = _driveDbOffset,
                    AxisEntries = _axisConfigEntries.ToList()
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

            // 路径B: 同步生成/更新开放平台 manual.* 页面（DynamicPageHost 可加载）
            try
            {
                await RunOnUiThreadAsync(() =>
                {
                    if (this is ApexHMI.ViewModels.Shell.MainWindowViewModel mvm)
                        mvm.RegenerateManualPages();
                });
            }
            catch (Exception ex)
            {
                AddLog("IO 生成", $"开放平台手动页面生成失败：{ex.Message}", "Warning");
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

    // ========== SFC 自动程序生成 ==========

    [RelayCommand]
    private void AddSfcStep()
    {
        var nextNo = SfcSteps.Count == 0 ? 10 : SfcSteps.Max(s => s.StepNo) + 10;
        var step = new SfcStep { StepNo = nextNo, NextStep = (nextNo + 10).ToString() };
        var defaultAction = new SfcStepAction();
        WireSfcAction(defaultAction);
        step.Actions.Add(defaultAction);
        SfcSteps.Add(step);
        SelectedSfcStep = step;
        SystemMessage = $"已添加步骤 STEP {nextNo:000}";
        _ = PersistConfigAsync(updateStatus: false);
    }

    [RelayCommand]
    private void AddSfcAction()
    {
        if (SelectedSfcStep is null) return;
        var action = new SfcStepAction();
        WireSfcAction(action);
        SelectedSfcStep.Actions.Add(action);
        SystemMessage = "已添加动作";
    }

    private void WireSfcAction(SfcStepAction action)
    {
        action.PropertyChanged += (s, e) =>
        {
            if (s is SfcStepAction a &&
                e.PropertyName is nameof(SfcStepAction.DeviceType) or nameof(SfcStepAction.DeviceIndex))
                RefreshSfcActionOptions(a);
        };
        RefreshSfcActionOptions(action);
    }

    private void RefreshSfcActionOptions(SfcStepAction action)
    {
        // 注入轴点位
        action.AxisPointOptions.Clear();
        if (action.DeviceType == "Axis")
        {
            var entry = _axisConfigEntries.FirstOrDefault(e => e.Index == action.DeviceIndex);
            if (entry is not null)
            {
                foreach (var p in entry.Points)
                    action.AxisPointOptions.Add(p);
                action.SelectedAxisPoint = action.AxisPointOptions.FirstOrDefault(p => p.Index == action.PointIndex);
            }
        }

        // 同步设备下拉选中（保证 ComboBox 初始高亮正确）
        if (action.DeviceType == "Cylinder")
        {
            var opt = SfcCylinderOptions.FirstOrDefault(o => o.Index == action.DeviceIndex);
            if (opt is not null && action.SelectedDeviceOption != opt)
            {
                action.SelectedDeviceOption = opt;
                if (!string.IsNullOrWhiteSpace(opt.DisplayName)) action.DeviceName = opt.DisplayName;
            }
        }
        else if (action.DeviceType == "Axis")
        {
            var opt = SfcAxisOptions.FirstOrDefault(o => o.Index == action.DeviceIndex);
            if (opt is not null && action.SelectedDeviceOption != opt)
            {
                action.SelectedDeviceOption = opt;
                if (!string.IsNullOrWhiteSpace(opt.DisplayName)) action.DeviceName = opt.DisplayName;
            }
        }
        else if (action.DeviceType == "Vacuum")
        {
            var opt = SfcVacuumOptions.FirstOrDefault(o => o.Index == action.DeviceIndex);
            if (opt is not null && action.SelectedDeviceOption != opt)
            {
                action.SelectedDeviceOption = opt;
                if (!string.IsNullOrWhiteSpace(opt.DisplayName)) action.DeviceName = opt.DisplayName;
            }
        }
    }

    [RelayCommand]
    private void DeleteSfcAction(SfcStepAction? action)
    {
        if (SelectedSfcStep is null || action is null) return;
        SelectedSfcStep.Actions.Remove(action);
        SystemMessage = "已删除动作";
    }

    [RelayCommand]
    private void AddSfcBranch()
    {
        if (SelectedSfcStep is null) return;
        SelectedSfcStep.Branches.Add(new SfcStepBranch());
        SystemMessage = "已添加跳转分支";
    }

    [RelayCommand]
    private void DeleteSfcBranch(SfcStepBranch? branch)
    {
        if (SelectedSfcStep is null || branch is null) return;
        SelectedSfcStep.Branches.Remove(branch);
        SystemMessage = "已删除跳转分支";
    }

    [RelayCommand]
    private void AddSfcAlarm()
    {
        if (SelectedSfcStep is null) return;
        SelectedSfcStep.AlarmEntries.Add(new SfcStepAlarm());
    }

    [RelayCommand]
    private void DeleteSfcAlarm(SfcStepAlarm? alarm)
    {
        if (SelectedSfcStep is null || alarm is null) return;
        SelectedSfcStep.AlarmEntries.Remove(alarm);
    }

    [RelayCommand]
    private void DeleteSfcStep()
    {
        if (SelectedSfcStep is null) return;
        var idx = SfcSteps.IndexOf(SelectedSfcStep);
        SfcSteps.Remove(SelectedSfcStep);
        SelectedSfcStep = SfcSteps.Count > 0 ? SfcSteps[Math.Max(0, idx - 1)] : null;
        SystemMessage = "已删除步骤";
        _ = PersistConfigAsync(updateStatus: false);
    }

    [RelayCommand]
    private void MoveSfcStepUp()
    {
        if (SelectedSfcStep is null) return;
        var idx = SfcSteps.IndexOf(SelectedSfcStep);
        if (idx <= 0) return;
        SfcSteps.Move(idx, idx - 1);
        RenumberSfcSteps();
        _ = PersistConfigAsync(updateStatus: false);
    }

    [RelayCommand]
    private void MoveSfcStepDown()
    {
        if (SelectedSfcStep is null) return;
        var idx = SfcSteps.IndexOf(SelectedSfcStep);
        if (idx < 0 || idx >= SfcSteps.Count - 1) return;
        SfcSteps.Move(idx, idx + 1);
        RenumberSfcSteps();
        _ = PersistConfigAsync(updateStatus: false);
    }

    private void RenumberSfcSteps()
    {
        // 先记录旧编号 → 新编号映射（用于更新分支 TargetStep）
        var oldToNew = new Dictionary<int, int>();
        for (int i = 0; i < SfcSteps.Count; i++)
            oldToNew[SfcSteps[i].StepNo] = (i + 1) * 10;

        // 更新步序编号
        for (int i = 0; i < SfcSteps.Count; i++)
            SfcSteps[i].StepNo = (i + 1) * 10;

        // NextStep：始终改为物理下一步（顺序跳转），保证移动后步序正确衔接
        // 最后一步 NextStep 保持不变（通常是 END/1000/循环回第一步），用旧→新映射更新
        for (int i = 0; i < SfcSteps.Count; i++)
        {
            if (i < SfcSteps.Count - 1)
            {
                SfcSteps[i].NextStep = SfcSteps[i + 1].StepNo.ToString();
            }
            else
            {
                // 最后一步：尝试把旧跳转目标更新到新编号
                if (TryRemapStepRef(SfcSteps[i].NextStep, oldToNew, out var remapped))
                    SfcSteps[i].NextStep = remapped;
            }
        }

        // 分支 TargetStep：用旧→新映射保留跳转意图
        foreach (var step in SfcSteps)
        {
            foreach (var branch in step.Branches)
            {
                if (TryRemapStepRef(branch.TargetStep, oldToNew, out var newTarget))
                    branch.TargetStep = newTarget;
            }
        }
    }

    private static bool TryRemapStepRef(string? stepRef, Dictionary<int, int> map, out string newRef)
    {
        newRef = stepRef ?? string.Empty;
        if (string.IsNullOrWhiteSpace(stepRef) || stepRef == "END") return false;
        if (!int.TryParse(stepRef.Trim(), out var oldNo)) return false;
        if (!map.TryGetValue(oldNo, out var newNo)) return false;
        newRef = newNo.ToString();
        return true;
    }

    // ========== 初始化程序步骤管理（独立步骤集合）==========

    [RelayCommand]
    private void AddSfcInitStep()
    {
        var nextNo = SfcInitSteps.Count == 0 ? 10 : SfcInitSteps.Max(s => s.StepNo) + 10;
        var step = new SfcStep { StepNo = nextNo, NextStep = (nextNo + 10).ToString() };
        var defaultAction = new SfcStepAction();
        WireSfcAction(defaultAction);
        step.Actions.Add(defaultAction);
        SfcInitSteps.Add(step);
        SelectedSfcInitStep = step;
        SystemMessage = $"已添加初始化步骤 STEP {nextNo:000}";
        _ = PersistConfigAsync(updateStatus: false);
    }

    [RelayCommand]
    private void DeleteSfcInitStep()
    {
        if (SelectedSfcInitStep is null) return;
        var idx = SfcInitSteps.IndexOf(SelectedSfcInitStep);
        SfcInitSteps.Remove(SelectedSfcInitStep);
        SelectedSfcInitStep = SfcInitSteps.Count > 0 ? SfcInitSteps[Math.Max(0, idx - 1)] : null;
        SystemMessage = "已删除初始化步骤";
        _ = PersistConfigAsync(updateStatus: false);
    }

    [RelayCommand]
    private void MoveSfcInitStepUp()
    {
        if (SelectedSfcInitStep is null) return;
        var idx = SfcInitSteps.IndexOf(SelectedSfcInitStep);
        if (idx <= 0) return;
        SfcInitSteps.Move(idx, idx - 1);
        RenumberSfcInitSteps();
        _ = PersistConfigAsync(updateStatus: false);
    }

    [RelayCommand]
    private void MoveSfcInitStepDown()
    {
        if (SelectedSfcInitStep is null) return;
        var idx = SfcInitSteps.IndexOf(SelectedSfcInitStep);
        if (idx < 0 || idx >= SfcInitSteps.Count - 1) return;
        SfcInitSteps.Move(idx, idx + 1);
        RenumberSfcInitSteps();
        _ = PersistConfigAsync(updateStatus: false);
    }

    private void RenumberSfcInitSteps()
    {
        var oldToNew = new Dictionary<int, int>();
        for (int i = 0; i < SfcInitSteps.Count; i++)
            oldToNew[SfcInitSteps[i].StepNo] = (i + 1) * 10;
        for (int i = 0; i < SfcInitSteps.Count; i++)
            SfcInitSteps[i].StepNo = (i + 1) * 10;
        for (int i = 0; i < SfcInitSteps.Count; i++)
        {
            if (i < SfcInitSteps.Count - 1)
                SfcInitSteps[i].NextStep = SfcInitSteps[i + 1].StepNo.ToString();
            else
                if (TryRemapStepRef(SfcInitSteps[i].NextStep, oldToNew, out var remapped))
                    SfcInitSteps[i].NextStep = remapped;
        }
        foreach (var step in SfcInitSteps)
            foreach (var branch in step.Branches)
                if (TryRemapStepRef(branch.TargetStep, oldToNew, out var newTarget))
                    branch.TargetStep = newTarget;
    }

    [RelayCommand]
    private void AddSfcInitAction()
    {
        if (SelectedSfcInitStep is null) return;
        var action = new SfcStepAction();
        WireSfcAction(action);
        SelectedSfcInitStep.Actions.Add(action);
        SystemMessage = "已添加初始化动作";
    }

    [RelayCommand]
    private void DeleteSfcInitAction(SfcStepAction? action)
    {
        if (SelectedSfcInitStep is null || action is null) return;
        SelectedSfcInitStep.Actions.Remove(action);
        SystemMessage = "已删除初始化动作";
    }

    [RelayCommand]
    private void AddSfcInitBranch()
    {
        if (SelectedSfcInitStep is null) return;
        SelectedSfcInitStep.Branches.Add(new SfcStepBranch());
        SystemMessage = "已添加初始化跳转分支";
    }

    [RelayCommand]
    private void DeleteSfcInitBranch(SfcStepBranch? branch)
    {
        if (SelectedSfcInitStep is null || branch is null) return;
        SelectedSfcInitStep.Branches.Remove(branch);
        SystemMessage = "已删除初始化跳转分支";
    }

    [RelayCommand]
    private void AddSfcInitAlarm()
    {
        if (SelectedSfcInitStep is null) return;
        SelectedSfcInitStep.AlarmEntries.Add(new SfcStepAlarm());
    }

    [RelayCommand]
    private void DeleteSfcInitAlarm(SfcStepAlarm? alarm)
    {
        if (SelectedSfcInitStep is null || alarm is null) return;
        SelectedSfcInitStep.AlarmEntries.Remove(alarm);
    }

    [RelayCommand]
    private void AutoFillSelectedSfcInitStep()
    {
        if (SelectedSfcInitStep is null) return;
        var driveDb = BuildSfcDriveDb();
        SfcCodeGeneratorService.AutoFill(SelectedSfcInitStep, driveDb);
        SystemMessage = "已自动填充初始化步骤完成条件";
    }

    [RelayCommand]
    private void CopySfcInitCode()
    {
        if (string.IsNullOrWhiteSpace(SfcInitGeneratedCode)) return;
        System.Windows.Clipboard.SetText(SfcInitGeneratedCode);
        SystemMessage = "初始化程序代码已复制到剪贴板";
    }

    [RelayCommand]
    private void SaveSfcInitCodeToFile()
    {
        if (string.IsNullOrWhiteSpace(SfcInitGeneratedCode)) { SystemMessage = "请先生成初始化程序代码"; return; }
        try
        {
            var outputDir = Path.Combine(AppContext.BaseDirectory, "Generated", "SfcProgram");
            Directory.CreateDirectory(outputDir);
            var fileName = $"{IoOperationNumber}_{SfcInitProgramName}_Init_S{SfcInitStationNo}.txt";
            var filePath = Path.Combine(outputDir, fileName);
            File.WriteAllText(filePath, SfcInitGeneratedCode, Encoding.UTF8);
            SystemMessage = $"已保存：{filePath}";
        }
        catch (Exception ex)
        {
            SystemMessage = $"保存失败：{ex.Message}";
        }
    }

    private void RestoreSfcInitSteps(SfcProgramConfig? config)
    {
        SfcInitSteps.Clear();
        SelectedSfcInitStep = null;
        if (config is null) return;
        if (!string.IsNullOrWhiteSpace(config.ProgramName)) SfcInitProgramName = config.ProgramName;
        if (!string.IsNullOrWhiteSpace(config.StationNo))   SfcInitStationNo   = config.StationNo;
        foreach (var dto in config.Steps ?? Enumerable.Empty<SfcStepDto>())
        {
            var step = new SfcStep
            {
                StepNo              = dto.StepNo,
                CompletionCondition = dto.CompletionCondition ?? string.Empty,
                NextStep            = dto.NextStep ?? "END"
            };
            foreach (var adto in dto.Actions ?? Enumerable.Empty<SfcStepActionDto>())
            {
                var action = new SfcStepAction
                {
                    DeviceType      = adto.DeviceType      ?? "Cylinder",
                    DeviceIndex     = adto.DeviceIndex,
                    DeviceName      = adto.DeviceName      ?? string.Empty,
                    ActionType      = adto.ActionType      ?? "ToWork",
                    PointIndex      = adto.PointIndex,
                    CustomCommand   = adto.CustomCommand   ?? string.Empty,
                    CustomCondition = adto.CustomCondition ?? string.Empty
                };
                WireSfcAction(action);
                step.Actions.Add(action);
            }
            foreach (var bdto in dto.Branches ?? Enumerable.Empty<SfcStepBranchDto>())
            {
                step.Branches.Add(new SfcStepBranch
                {
                    Condition  = bdto.Condition  ?? string.Empty,
                    TargetStep = bdto.TargetStep ?? "END"
                });
            }
            foreach (var aldto in dto.Alarms ?? Enumerable.Empty<SfcStepAlarmDto>())
            {
                step.AlarmEntries.Add(new SfcStepAlarm
                {
                    AlarmMessage   = aldto.AlarmMessage   ?? string.Empty,
                    AlarmCondition = aldto.AlarmCondition ?? string.Empty,
                    AlarmType      = aldto.AlarmType      ?? "Stop"
                });
            }
            SfcInitSteps.Add(step);
        }
        SelectedSfcInitStep = SfcInitSteps.FirstOrDefault();
    }

    /// <summary>从 AppConfig 启动加载所有工位的初始化程序到字典 + 显示首个工位。</summary>
    private void RestoreAllSfcInitStations(AppConfig config)
    {
        _suppressInitStationSwitch = true;
        try
        {
            _sfcInitProgramsByStation.Clear();

            // 兼容旧版单数 SfcInitProgram 字段
            if (config.SfcInitProgram?.Steps?.Count > 0)
            {
                var key = string.IsNullOrWhiteSpace(config.SfcInitProgram.StationNo) ? "1" : config.SfcInitProgram.StationNo;
                _sfcInitProgramsByStation[key] = config.SfcInitProgram;
            }

            // 加载多工位列表
            foreach (var sfcCfg in config.SfcInitPrograms ?? Enumerable.Empty<SfcProgramConfig>())
            {
                var key = string.IsNullOrWhiteSpace(sfcCfg.StationNo) ? "1" : sfcCfg.StationNo;
                _sfcInitProgramsByStation[key] = sfcCfg;
            }

            var targetStation = _sfcInitProgramsByStation.Keys.OrderBy(k => k).FirstOrDefault() ?? "1";
            if (_sfcInitProgramsByStation.TryGetValue(targetStation, out var stationConfig))
                RestoreSfcInitSteps(stationConfig);
            else
                SfcInitStationNo = targetStation;

            _prevSfcInitStationNo = SfcInitStationNo;
        }
        finally
        {
            _suppressInitStationSwitch = false;
        }
    }

    /// <summary>工位号变更钩子：保存旧工位 Init → 加载新工位 → 持久化</summary>
    partial void OnSfcInitStationNoChanged(string value)
    {
        if (_suppressInitStationSwitch) return;
        FlushCurrentSfcInitToDict(_prevSfcInitStationNo);
        _prevSfcInitStationNo = value;
        LoadSfcInitFromDictInternal(value);
        _ = PersistConfigAsync(updateStatus: false);
    }

    /// <summary>把当前 UI 中的 SfcInitSteps 快照保存到工位字典。</summary>
    internal void FlushCurrentSfcInitToDict(string stationNo)
    {
        if (string.IsNullOrWhiteSpace(stationNo)) return;
        _sfcInitProgramsByStation[stationNo] = new SfcProgramConfig
        {
            ProgramName = SfcInitProgramName,
            StationNo = stationNo,
            Steps = SfcInitSteps.Select(s => new SfcStepDto
            {
                StepNo = s.StepNo,
                CompletionCondition = s.CompletionCondition,
                NextStep = s.NextStep,
                Actions = s.Actions.Select(a => new SfcStepActionDto
                {
                    DeviceType = a.DeviceType, DeviceIndex = a.DeviceIndex, DeviceName = a.DeviceName,
                    ActionType = a.ActionType, PointIndex = a.PointIndex,
                    CustomCommand = a.CustomCommand, CustomCondition = a.CustomCondition
                }).ToList(),
                Branches = s.Branches.Select(b => new SfcStepBranchDto
                {
                    Condition = b.Condition, TargetStep = b.TargetStep
                }).ToList(),
                Alarms = s.AlarmEntries.Select(al => new SfcStepAlarmDto
                {
                    AlarmMessage = al.AlarmMessage, AlarmCondition = al.AlarmCondition, AlarmType = al.AlarmType
                }).ToList()
            }).ToList()
        };
    }

    /// <summary>从字典加载指定工位的 Init steps（不改 SfcInitStationNo，避免循环触发）</summary>
    private void LoadSfcInitFromDictInternal(string stationNo)
    {
        _suppressInitStationSwitch = true;
        try
        {
            if (_sfcInitProgramsByStation.TryGetValue(stationNo, out var cfg))
            {
                RestoreSfcInitSteps(cfg);
            }
            else
            {
                SfcInitSteps.Clear();
                SelectedSfcInitStep = null;
            }
        }
        finally
        {
            _suppressInitStationSwitch = false;
        }
    }

    /// <summary>构建持久化用的 Init Programs 列表。</summary>
    internal List<SfcProgramConfig> BuildSfcInitProgramsForPersist()
    {
        FlushCurrentSfcInitToDict(SfcInitStationNo);
        return _sfcInitProgramsByStation.Values.ToList();
    }

    private void RestoreSfcSteps(SfcProgramConfig? config)
    {
        if (config is null) return;
        SfcSteps.Clear();
        if (!string.IsNullOrWhiteSpace(config.ProgramName)) SfcProgramName = config.ProgramName;
        if (!string.IsNullOrWhiteSpace(config.StationNo)) SfcStationNo = config.StationNo;
        foreach (var dto in config.Steps ?? Enumerable.Empty<SfcStepDto>())
        {
            var step = new SfcStep
            {
                StepNo = dto.StepNo,
                CompletionCondition = dto.CompletionCondition ?? string.Empty,
                NextStep = dto.NextStep ?? "END"
            };
            foreach (var adto in dto.Actions ?? Enumerable.Empty<SfcStepActionDto>())
            {
                var action = new SfcStepAction
                {
                    DeviceType = adto.DeviceType ?? "Cylinder",
                    DeviceIndex = adto.DeviceIndex,
                    DeviceName = adto.DeviceName ?? string.Empty,
                    ActionType = adto.ActionType ?? "ToWork",
                    PointIndex = adto.PointIndex,
                    CustomCommand = adto.CustomCommand ?? string.Empty,
                    CustomCondition = adto.CustomCondition ?? string.Empty
                };
                WireSfcAction(action);
                step.Actions.Add(action);
            }
            foreach (var bdto in dto.Branches ?? Enumerable.Empty<SfcStepBranchDto>())
            {
                step.Branches.Add(new SfcStepBranch
                {
                    Condition = bdto.Condition ?? string.Empty,
                    TargetStep = bdto.TargetStep ?? "END"
                });
            }
            foreach (var aldto in dto.Alarms ?? Enumerable.Empty<SfcStepAlarmDto>())
            {
                step.AlarmEntries.Add(new SfcStepAlarm
                {
                    AlarmMessage   = aldto.AlarmMessage   ?? string.Empty,
                    AlarmCondition = aldto.AlarmCondition ?? string.Empty,
                    AlarmType      = aldto.AlarmType      ?? "Stop"
                });
            }
            SfcSteps.Add(step);
        }
        SelectedSfcStep = SfcSteps.FirstOrDefault();
    }

    // ===== 多工位 SFC 配置管理 =====

    /// <summary>从完整 AppConfig 初始化所有工位缓存（用于启动加载）</summary>
    private void RestoreAllSfcStations(AppConfig config)
    {
        _suppressStationSwitch = true;
        try
        {
            _sfcProgramsByStation.Clear();

            // 兼容旧版单工位字段迁移
            if (config.SfcProgram?.Steps?.Count > 0)
            {
                var key = string.IsNullOrWhiteSpace(config.SfcProgram.StationNo) ? "1" : config.SfcProgram.StationNo;
                _sfcProgramsByStation[key] = config.SfcProgram;
            }

            // 加载多工位列表
            foreach (var sfcCfg in config.SfcPrograms ?? Enumerable.Empty<SfcProgramConfig>())
            {
                var key = string.IsNullOrWhiteSpace(sfcCfg.StationNo) ? "1" : sfcCfg.StationNo;
                _sfcProgramsByStation[key] = sfcCfg;
            }

            // 选取第一个工位（按字符串升序）作为初始显示工位
            var targetStation = _sfcProgramsByStation.Keys.OrderBy(k => k).FirstOrDefault() ?? "1";
            if (_sfcProgramsByStation.TryGetValue(targetStation, out var stationConfig))
                RestoreSfcSteps(stationConfig); // 内含 SfcStationNo 赋值，被 _suppressStationSwitch 屏蔽
            else
                SfcStationNo = targetStation;

            _prevSfcStationNo = SfcStationNo;
        }
        finally
        {
            _suppressStationSwitch = false;
        }
    }

    /// <summary>工位号变更钩子：保存旧工位 → 加载新工位 → 持久化</summary>
    partial void OnSfcStationNoChanged(string value)
    {
        if (_suppressStationSwitch) return;
        FlushCurrentSfcToDict(_prevSfcStationNo);
        _prevSfcStationNo = value;
        LoadSfcFromDictInternal(value);
        _ = PersistConfigAsync(updateStatus: false);
    }

    /// <summary>将当前 UI 状态快照保存到工位字典</summary>
    internal void FlushCurrentSfcToDict(string stationNo)
    {
        if (string.IsNullOrWhiteSpace(stationNo)) return;
        _sfcProgramsByStation[stationNo] = new SfcProgramConfig
        {
            ProgramName = SfcProgramName,
            StationNo = stationNo,
            Steps = SfcSteps.Select(s => new SfcStepDto
            {
                StepNo = s.StepNo,
                CompletionCondition = s.CompletionCondition,
                NextStep = s.NextStep,
                Actions = s.Actions.Select(a => new SfcStepActionDto
                {
                    DeviceType = a.DeviceType,
                    DeviceIndex = a.DeviceIndex,
                    DeviceName = a.DeviceName,
                    ActionType = a.ActionType,
                    PointIndex = a.PointIndex,
                    CustomCommand = a.CustomCommand,
                    CustomCondition = a.CustomCondition
                }).ToList(),
                Branches = s.Branches.Select(b => new SfcStepBranchDto
                {
                    Condition = b.Condition,
                    TargetStep = b.TargetStep
                }).ToList(),
                Alarms = s.AlarmEntries.Select(al => new SfcStepAlarmDto
                {
                    AlarmMessage = al.AlarmMessage,
                    AlarmCondition = al.AlarmCondition,
                    AlarmType = al.AlarmType
                }).ToList()
            }).ToList()
        };
    }

    /// <summary>从字典中加载指定工位的步骤（不修改 SfcStationNo，避免循环触发）</summary>
    private void LoadSfcFromDictInternal(string stationNo)
    {
        SfcSteps.Clear();
        SelectedSfcStep = null;
        SfcGeneratedCode = "配置步骤后点击「生成代码」。";
        if (!_sfcProgramsByStation.TryGetValue(stationNo, out var config)) return;
        if (!string.IsNullOrWhiteSpace(config.ProgramName)) SfcProgramName = config.ProgramName;
        foreach (var dto in config.Steps ?? Enumerable.Empty<SfcStepDto>())
        {
            var step = new SfcStep
            {
                StepNo = dto.StepNo,
                CompletionCondition = dto.CompletionCondition ?? string.Empty,
                NextStep = dto.NextStep ?? "END"
            };
            foreach (var adto in dto.Actions ?? Enumerable.Empty<SfcStepActionDto>())
            {
                var action = new SfcStepAction
                {
                    DeviceType = adto.DeviceType ?? "Cylinder",
                    DeviceIndex = adto.DeviceIndex,
                    DeviceName = adto.DeviceName ?? string.Empty,
                    ActionType = adto.ActionType ?? "ToWork",
                    PointIndex = adto.PointIndex,
                    CustomCommand = adto.CustomCommand ?? string.Empty,
                    CustomCondition = adto.CustomCondition ?? string.Empty
                };
                WireSfcAction(action);
                step.Actions.Add(action);
            }
            foreach (var bdto in dto.Branches ?? Enumerable.Empty<SfcStepBranchDto>())
            {
                step.Branches.Add(new SfcStepBranch
                {
                    Condition = bdto.Condition ?? string.Empty,
                    TargetStep = bdto.TargetStep ?? "END"
                });
            }
            foreach (var aldto in dto.Alarms ?? Enumerable.Empty<SfcStepAlarmDto>())
            {
                step.AlarmEntries.Add(new SfcStepAlarm
                {
                    AlarmMessage   = aldto.AlarmMessage   ?? string.Empty,
                    AlarmCondition = aldto.AlarmCondition ?? string.Empty,
                    AlarmType      = aldto.AlarmType      ?? "Stop"
                });
            }
            SfcSteps.Add(step);
        }
        SelectedSfcStep = SfcSteps.FirstOrDefault();
    }

    [RelayCommand]
    private void AutoFillSelectedSfcStep()
    {
        if (SelectedSfcStep is null) return;
        var driveDb = BuildSfcDriveDb();
        SfcCodeGeneratorService.AutoFill(SelectedSfcStep, driveDb);
        SystemMessage = "已自动填充完成条件";
    }

    [RelayCommand]
    private async Task GenerateSfcCode()
    {
        if (SfcSteps.Count == 0) { SystemMessage = "请先添加步骤"; return; }
        var driveDb = BuildSfcDriveDb();
        var opBase = ResolveOperationBaseNumber(IoOperationNumber);
        var controlDb = $"DB{opBase}_Control";
        var faultDbBase = $"DB{opBase + 70}";
        if (!int.TryParse(SfcStationNo, out var stationNo)) stationNo = 1;
        SfcGeneratedCode = SfcCodeGeneratorService.Generate(SfcSteps, driveDb, controlDb, stationNo, SfcProgramName, IoOperationNumber, GetProjectRoot(), faultDbBase);
        SystemMessage = "SFC 代码已生成";
        _ = PersistConfigAsync(updateStatus: false);
        AddLog("SFC 生成", $"{SfcProgramName} 已生成 {SfcSteps.Count} 步", "Info");

        try
        {
            // 保存自动流程 .txt 到 exe 路径
            var outputDir = Path.Combine(AppContext.BaseDirectory, "Generated", "SfcProgram");
            Directory.CreateDirectory(outputDir);
            var fileName = $"{IoOperationNumber}_{SfcProgramName}_S{SfcStationNo}.txt";
            var filePath = Path.Combine(outputDir, fileName);
            File.WriteAllText(filePath, SfcGeneratedCode, Encoding.UTF8);
            AddLog("SFC 生成", $"已保存：{filePath}", "Info");

            // 生成并保存报警文件
            var usedAlarmTypes = SfcCodeGeneratorService.GetUsedAlarmTypes(SfcSteps);
            if (usedAlarmTypes.Count > 0)
            {
                var normalizedOp = IoOperationNumber.Trim().ToUpperInvariant().StartsWith("OP")
                    ? IoOperationNumber.Trim().ToUpperInvariant()
                    : $"OP{IoOperationNumber.Trim().ToUpperInvariant()}";
                foreach (var alarmType in usedAlarmTypes)
                {
                    // 本地备份使用 MergeAlarmDut（传 null 表示从零生成），保持与 _exported 逻辑一致
                    var dutContent = SfcCodeGeneratorService.MergeAlarmDut(null, SfcSteps, IoOperationNumber, alarmType);
                    File.WriteAllText(Path.Combine(outputDir, $"Str_{normalizedOp}_Fault{alarmType}.st"), dutContent, Encoding.UTF8);
                }
                AddLog("SFC 生成", $"已保存报警文件（{string.Join(", ", usedAlarmTypes)}）至 {outputDir}", "Info");
            }

            // 写入 _exported 目录（OPXX/ACT_AutoRunSTXX.st + 0.Struct/报警文件）
            await SaveSfcToExportedDirectoryAsync(SfcGeneratedCode, IoOperationNumber, SfcStationNo, usedAlarmTypes, faultDbBase);
        }
        catch (Exception ex)
        {
            AddLog("SFC 生成", $"保存/导入流程失败：{ex.Message}", "Warning");
        }
    }

    private async Task SaveSfcToExportedDirectoryAsync(string code, string opNo, string stationNo,
        IReadOnlyList<string> usedAlarmTypes, string faultDbBase)
    {
        var exportedDir = TryGetExportedDirectory();
        if (string.IsNullOrWhiteSpace(exportedDir))
        {
            AddLog("SFC 生成", "未配置工程目录，跳过写入 _exported。", "Info");
            return;
        }

        var normalizedOp = opNo.Trim().ToUpperInvariant().StartsWith("OP")
            ? opNo.Trim().ToUpperInvariant()
            : $"OP{opNo.Trim().ToUpperInvariant()}";

        // 自动流程 Action → OPXX/2.PRG/OPXX_Graph/ACT_AutoRunSTXX_程序名.st
        var graphDir = Path.Combine(exportedDir, normalizedOp, "2.PRG", $"{normalizedOp}_Graph");
        Directory.CreateDirectory(graphDir);
        // 程序名净化：去除文件名非法字符
        var safeProgramName = Regex.Replace(SfcProgramName.Trim(), @"[\\/:*?""<>|]", "_");
        var actionFileName = string.IsNullOrWhiteSpace(safeProgramName)
            ? $"ACT_AutoRunST{stationNo}.st"
            : $"ACT_AutoRunST{stationNo}_{safeProgramName}.st";
        await Compat.WriteAllTextAsync(Path.Combine(graphDir, actionFileName), code, Encoding.UTF8);
        AddLog("SFC 生成", $"已写入 _exported：{normalizedOp}/2.PRG/{normalizedOp}_Graph/{actionFileName}", "Info");

        // 报警结构体 DUT → OPXX/0.Struct/Str_OPXX_FaultXXX.st
        // 注意：只更新结构体（追加模式），不修改 GVL 文件（DBXX70_Fault.st）
        if (usedAlarmTypes.Count > 0)
        {
            var opStructDir = Path.Combine(exportedDir, normalizedOp, "0.Struct");
            Directory.CreateDirectory(opStructDir);

            foreach (var alarmType in usedAlarmTypes)
            {
                // 大小写与已有文件保持一致：FaultEstop / FaultStop / FaultRun
                var alarmTypePascal = char.ToUpperInvariant(alarmType[0]) + alarmType.Substring(1).ToLowerInvariant();
                var dutName = $"Str_{normalizedOp}_Fault{alarmTypePascal}.st";
                var dutPath = Path.Combine(opStructDir, dutName);

                // 读取已有文件内容（追加模式，不覆盖 MAP/Space 占位符和已有变量）
                string? existingContent = null;
                if (File.Exists(dutPath))
                {
                    try { existingContent = await Compat.ReadAllTextAsync(dutPath, Encoding.UTF8); }
                    catch { /* 读取失败则视为新建 */ }
                }

                var dutContent = SfcCodeGeneratorService.MergeAlarmDut(existingContent, SfcSteps, opNo, alarmType);
                await Compat.WriteAllTextAsync(dutPath, dutContent, Encoding.UTF8);
                AddLog("SFC 生成", $"已{(existingContent != null ? "追加更新" : "新建")}报警结构体：{normalizedOp}/0.Struct/{dutName}", "Info");
            }
        }

        // 调用 InProShop 导入脚本（自动将新文件注册到 .project）
        try
        {
            await RunInProShopImportAsync();
        }
        catch (Exception ex)
        {
            AddLog("SFC 生成", $"运行导入脚本失败：{ex.Message}", "Warning");
        }
    }

    // ========== 初始化程序生成（独立步骤集合，生成 ACT_InitSTxx.st）==========

    [RelayCommand]
    private async Task GenerateSfcInitCode()
    {
        if (SfcInitSteps.Count == 0) { SystemMessage = "请先在初始化程序页添加步骤"; return; }

        var driveDb     = BuildSfcDriveDb();
        var opBase      = ResolveOperationBaseNumber(IoOperationNumber);
        var controlDb   = $"DB{opBase}_Control";
        var faultDb     = $"DB{opBase + 70}";
        if (!int.TryParse(SfcInitStationNo, out var stationNo)) stationNo = 1;

        var initCode = SfcCodeGeneratorService.GenerateInit(
            SfcInitSteps, driveDb, controlDb, faultDb, stationNo,
            SfcInitProgramName, IoOperationNumber, GetProjectRoot());

        SfcInitGeneratedCode = initCode;
        SystemMessage = "初始化程序已生成";
        _ = PersistConfigAsync(updateStatus: false);
        AddLog("Init 生成", $"初始化程序已生成 {SfcInitSteps.Count} 步", "Info");

        try
        {
            var outputDir = Path.Combine(AppContext.BaseDirectory, "Generated", "SfcProgram");
            Directory.CreateDirectory(outputDir);

            // 本地备份：主程序 .txt
            var safeName = Regex.Replace(SfcInitProgramName.Trim(), @"[\\/:*?""<>|]", "_");
            var backupFileName = string.IsNullOrWhiteSpace(safeName)
                ? $"{IoOperationNumber}_Init_S{SfcInitStationNo}.txt"
                : $"{IoOperationNumber}_Init_{safeName}_S{SfcInitStationNo}.txt";
            File.WriteAllText(Path.Combine(outputDir, backupFileName), initCode, Encoding.UTF8);
            AddLog("Init 生成", $"已保存本地备份：{backupFileName}", "Info");

            // 本地备份：报警 DUT 文件（与自动程序一致）
            var usedAlarmTypes = SfcCodeGeneratorService.GetUsedAlarmTypes(SfcInitSteps);
            if (usedAlarmTypes.Count > 0)
            {
                var normalizedOpLocal = IoOperationNumber.Trim().ToUpperInvariant().StartsWith("OP")
                    ? IoOperationNumber.Trim().ToUpperInvariant()
                    : $"OP{IoOperationNumber.Trim().ToUpperInvariant()}";
                foreach (var alarmType in usedAlarmTypes)
                {
                    var dutContent = SfcCodeGeneratorService.MergeAlarmDut(null, SfcInitSteps, IoOperationNumber, alarmType);
                    File.WriteAllText(Path.Combine(outputDir, $"Init_Str_{normalizedOpLocal}_Fault{alarmType}.st"), dutContent, Encoding.UTF8);
                }
                AddLog("Init 生成", $"已保存报警文件（{string.Join(", ", usedAlarmTypes)}）至 {outputDir}", "Info");
            }

            // 写入 _exported
            var exportedDir = TryGetExportedDirectory();
            if (!string.IsNullOrWhiteSpace(exportedDir))
            {
                var normalizedOp = IoOperationNumber.Trim().ToUpperInvariant().StartsWith("OP")
                    ? IoOperationNumber.Trim().ToUpperInvariant()
                    : $"OP{IoOperationNumber.Trim().ToUpperInvariant()}";

                // _exported：OPXX/2.PRG/OPXX_Graph/ACT_InitSTxx.st
                var graphDir = Path.Combine(exportedDir, normalizedOp, "2.PRG", $"{normalizedOp}_Graph");
                Directory.CreateDirectory(graphDir);
                var initFileName = $"ACT_InitST{SfcInitStationNo}.st";
                await Compat.WriteAllTextAsync(Path.Combine(graphDir, initFileName), initCode, Encoding.UTF8);
                AddLog("Init 生成", $"已写入 _exported：{normalizedOp}/2.PRG/{normalizedOp}_Graph/{initFileName}", "Info");

                // _exported：OPXX/0.Struct/Str_OPXX_FaultXXX.st（追加合并，与自动程序逻辑一致）
                if (usedAlarmTypes.Count > 0)
                {
                    var opStructDir = Path.Combine(exportedDir, normalizedOp, "0.Struct");
                    Directory.CreateDirectory(opStructDir);
                    foreach (var alarmType in usedAlarmTypes)
                    {
                        var alarmTypePascal = char.ToUpperInvariant(alarmType[0]) + alarmType.Substring(1).ToLowerInvariant();
                        var dutName = $"Str_{normalizedOp}_Fault{alarmTypePascal}.st";
                        var dutPath = Path.Combine(opStructDir, dutName);
                        string? existingContent = null;
                        if (File.Exists(dutPath))
                            try { existingContent = await Compat.ReadAllTextAsync(dutPath, Encoding.UTF8); } catch { }
                        var dutContent = SfcCodeGeneratorService.MergeAlarmDut(existingContent, SfcInitSteps, IoOperationNumber, alarmType);
                        await Compat.WriteAllTextAsync(dutPath, dutContent, Encoding.UTF8);
                        AddLog("Init 生成", $"已{(existingContent != null ? "追加更新" : "新建")}报警结构体：{normalizedOp}/0.Struct/{dutName}", "Info");
                    }
                }
            }

            // 运行 InProShop 导入脚本
            try { await RunInProShopImportAsync(); }
            catch (Exception ex) { AddLog("Init 生成", $"导入脚本失败：{ex.Message}", "Warning"); }
        }
        catch (Exception ex)
        {
            AddLog("Init 生成", $"保存失败：{ex.Message}", "Warning");
        }
    }

    [RelayCommand]
    private void CopySfcCode()
    {
        if (string.IsNullOrWhiteSpace(SfcGeneratedCode)) return;
        System.Windows.Clipboard.SetText(SfcGeneratedCode);
        SystemMessage = "代码已复制到剪贴板";
    }

    [RelayCommand]
    private void SaveSfcCodeToFile()
    {
        if (string.IsNullOrWhiteSpace(SfcGeneratedCode)) { SystemMessage = "请先生成代码"; return; }
        try
        {
            var outputDir = Path.Combine(AppContext.BaseDirectory, "Generated", "SfcProgram");
            Directory.CreateDirectory(outputDir);
            var fileName = $"{IoOperationNumber}_{SfcProgramName}_S{SfcStationNo}.txt";
            var filePath = Path.Combine(outputDir, fileName);
            File.WriteAllText(filePath, SfcGeneratedCode, Encoding.UTF8);
            SystemMessage = $"已保存：{filePath}";
            AddLog("SFC 生成", $"保存至 {filePath}", "Info");
        }
        catch (Exception ex)
        {
            SystemMessage = $"保存失败：{ex.Message}";
        }
    }

    private string BuildSfcDriveDb()
    {
        var baseNo = ResolveOperationBaseNumber(IoOperationNumber);
        return $"DB{baseNo + _driveDbOffset}";
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
    private Task SaveDesignerLayoutAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task LoadDesignerLayoutAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task SaveDesignerProjectAsync() => Task.CompletedTask;

    [RelayCommand]
    private async Task LoadDesignerProjectAsync()
    {
        await Task.CompletedTask;
        var project = (DesignerProject?)null;
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
        if (IsRuntimeMode)
        {
            await RefreshTagsAsync();
            Navigate("运行页面");
        }
        else
        {
            Navigate("画布设计");
        }
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
