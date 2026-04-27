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
    // ========== 监控 / OPC UA 浏览 / 程序监控 ==========
    private bool _isDisconnecting;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            if (_opcUaService.IsConnected)
            {
                SystemMessage = $"已连接：{Connection.GetEndpointUrl()}";
                OnPropertyChanged(nameof(CommunicationStatus));
                return;
            }

            if (!string.Equals(Connection.Protocol, "OPC UA", StringComparison.OrdinalIgnoreCase))
            {
                SystemMessage = $"当前版本仅支持 OPC UA 通讯，暂不支持 {Connection.Protocol}";
                AddLog("通讯", SystemMessage, "Warning");
                return;
            }

            // Run connect on a thread-pool thread to avoid UI-thread deadlocks inside the OPC UA stack.
            await Task.Run(() => _opcUaService.ConnectAsync(Connection, CancellationToken.None));
            ClearOpcBindingValueCache();
            SystemMessage = $"连接成功：{Connection.GetEndpointUrl()}";
            OnPropertyChanged(nameof(CommunicationStatus));
            await LoadOpcUaBrowserRootAsync();
            // 连接后先刷新当前页，避免全量刷新导致首屏长时间卡顿。
            await AutoRefreshTickAsync();
            await LoadAlarmHistoryAsync();
            InitializeIoMonitorItems();
            UpdateAutoRefreshState();
            AddLog("通讯", SystemMessage, "Info");
        }
        catch (Exception ex)
        {
            SystemMessage = $"连接失败：{ex.Message}";
            AddLog("通讯", SystemMessage, "Error");
        }
    }

    [RelayCommand]
    private Task DisconnectAsync()
    {
        if (_isDisconnecting)
        {
            SystemMessage = "正在断开 OPC UA 连接，请稍候...";
            return Task.CompletedTask;
        }

        _isDisconnecting = true;
        _subscriptionTimer.Stop();
        _opcUaBrowserRefreshTimer.Stop();
        SystemMessage = "正在断开 OPC UA 连接...";

        OpcUaBrowserNodes.Clear();
        SelectedOpcUaBrowseNode = null;
        SelectedOpcUaNodeValue = "--";
        SelectedOpcUaNodeStatus = "未读取";
        SelectedOpcUaNodeTimestamp = "--";
        OpcUaBrowserStatus = "连接 OPC UA 后，可在这里浏览服务器节点。";
        OnPropertyChanged(nameof(CommunicationStatus));

        _ = Task.Run(async () =>
        {
            string finalMessage;
            string finalLogLevel;

            try
            {
                await _opcUaService.DisconnectAsync();
                finalMessage = "已断开 OPC UA 连接";
                finalLogLevel = "Info";
            }
            catch (Exception ex)
            {
                finalMessage = $"断开连接时出现异常：{ex.Message}";
                finalLogLevel = "Warning";
            }

            await RunOnUiThreadAsync(() =>
            {
                _isDisconnecting = false;
                SystemMessage = finalMessage;
                OnPropertyChanged(nameof(CommunicationStatus));
                AddLog("通讯", finalMessage, finalLogLevel);
                ClearOpcBindingValueCache();
            });
        });

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadOpcUaBrowserRootAsync()
    {
        try
        {
            OpcUaBrowserNodes.Clear();
            foreach (var node in await _opcUaService.BrowseNodeAsync())
            {
                OpcUaBrowserNodes.Add(node);
            }

            OpcUaBrowserStatus = OpcUaBrowserNodes.Count == 0
                ? "当前服务器没有返回可浏览节点。"
                : $"已加载根节点，共 {OpcUaBrowserNodes.Count} 项。";
        }
        catch (Exception ex)
        {
            OpcUaBrowserStatus = $"节点浏览失败：{ex.Message}";
            AddLog("OPC UA", OpcUaBrowserStatus, "Error");
        }
    }

    [RelayCommand]
    private async Task ExpandOpcUaBrowserNodeAsync(OpcUaBrowseNode? node)
    {
        if (node is null || node.IsPlaceholder || !node.HasChildren || node.IsLoaded)
        {
            return;
        }

        try
        {
            node.Children.Clear();
            foreach (var child in await _opcUaService.BrowseNodeAsync(node.NodeId))
            {
                node.Children.Add(child);
            }

            node.IsLoaded = true;
            OpcUaBrowserStatus = $"已展开 {node.DisplayName}";
        }
        catch (Exception ex)
        {
            node.Children.Clear();
            node.IsLoaded = false;
            OpcUaBrowserStatus = $"展开节点失败：{ex.Message}";
            AddLog("OPC UA", OpcUaBrowserStatus, "Error");
        }
    }

    [RelayCommand]
    private async Task RefreshSelectedOpcUaNodeAsync()
    {
        if (_isRefreshingSelectedOpcUaNode)
        {
            return;
        }

        if (SelectedOpcUaBrowseNode is null || SelectedOpcUaBrowseNode.IsPlaceholder)
        {
            return;
        }

        if (!string.Equals(SelectedOpcUaBrowseNode.NodeClass, "Variable", StringComparison.OrdinalIgnoreCase))
        {
            SelectedOpcUaNodeValue = "--";
        SelectedOpcUaNodeStatus = "当前节点不是变量节点";
            SelectedOpcUaNodeTimestamp = "--";
            SelectedOpcUaBrowseNode.DataType = "--";
            OpcUaBrowserStatus = $"宸查€変腑 {SelectedOpcUaBrowseNode.DisplayName}";
            return;
        }

        try
        {
            _isRefreshingSelectedOpcUaNode = true;
            var result = await _opcUaService.ReadNodeAsync(SelectedOpcUaBrowseNode.NodeId);
            SelectedOpcUaBrowseNode.DataType = result.DataType;
            SelectedOpcUaBrowseNode.Value = result.Value;
            SelectedOpcUaNodeValue = string.IsNullOrWhiteSpace(result.Value) ? "(绌?" : result.Value;
            SelectedOpcUaNodeStatus = result.StatusCode;
            SelectedOpcUaNodeTimestamp = result.Timestamp;
            OpcUaBrowserStatus = $"已读取节点：{SelectedOpcUaBrowseNode.DisplayName}";
        }
        catch (Exception ex)
        {
            SelectedOpcUaNodeValue = "--";
            SelectedOpcUaNodeStatus = $"读取失败：{ex.Message}";
            SelectedOpcUaNodeTimestamp = "--";
            OpcUaBrowserStatus = SelectedOpcUaNodeStatus;
            AddLog("OPC UA", OpcUaBrowserStatus, "Error");
        }
        finally
        {
            _isRefreshingSelectedOpcUaNode = false;
        }
    }

    [RelayCommand]
    private void AddSelectedOpcUaNodeAsTag()
    {
        if (SelectedOpcUaBrowseNode is null || SelectedOpcUaBrowseNode.IsPlaceholder)
        {
            SystemMessage = "请先选择一个 OPC UA 节点。";
            return;
        }

        if (!string.Equals(SelectedOpcUaBrowseNode.NodeClass, "Variable", StringComparison.OrdinalIgnoreCase))
        {
            SystemMessage = "只有变量节点才能加入变量表。";
            return;
        }

        if (Tags.Any(tag => string.Equals(tag.NodeId, SelectedOpcUaBrowseNode.NodeId, StringComparison.OrdinalIgnoreCase)))
        {
            SystemMessage = "该节点已经在变量表中。";
            return;
        }

        Tags.Add(new TagItem
        {
            Name = string.IsNullOrWhiteSpace(SelectedOpcUaBrowseNode.DisplayName) ? $"Tag_{Tags.Count + 1}" : SelectedOpcUaBrowseNode.DisplayName,
            NodeId = SelectedOpcUaBrowseNode.NodeId,
            DataType = SelectedOpcUaBrowseNode.DataType,
            Category = "OPC UA Browser",
            Group = "Imported",
            Direction = "Input",
            CurrentValue = SelectedOpcUaNodeValue == "(绌?" ? string.Empty : SelectedOpcUaNodeValue,
            Description = "由内置 OPC UA 浏览器导入",
            IsWritable = false
        });

        RefreshMonitorView();
        SystemMessage = $"已加入变量表：{SelectedOpcUaBrowseNode.DisplayName}";
    }

    [RelayCommand]
    private async Task ImportTagsAsync()
    {
        var dialog = new OpenFileDialog { Filter = "CSV 文件|*.csv|所有文件|*.*" };
        if (dialog.ShowDialog() != true) return;
        var imported = await _csvImportService.ImportTagsAsync(dialog.FileName);
        Tags.Clear();
        foreach (var tag in imported) Tags.Add(tag);
        OnPropertyChanged(nameof(TagCount));
        RefreshMonitorView();
        SystemMessage = $"已导入变量表：{Path.GetFileName(dialog.FileName)}，共 {Tags.Count} 项";
        AddLog("配置", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task ImportXmlTagsAsync()
    {
        var dialog = new OpenFileDialog { Filter = "XML 文件|*.xml|所有文件|*.*" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var imported = await _xmlImportService.ImportTagsAsync(dialog.FileName);
            Tags.Clear();
            foreach (var tag in imported) Tags.Add(tag);
            OnPropertyChanged(nameof(TagCount));
            RefreshMonitorView();
            SystemMessage = $"已导入 XML 变量表：{Path.GetFileName(dialog.FileName)}，共 {Tags.Count} 项";
            AddLog("配置", SystemMessage, "Info");
        }
        catch (Exception ex)
        {
            ShowPopup("XML导入失败", ex.Message, "Warning");
        }
    }

    [RelayCommand]
    private void AddCustomTag()
    {
        if (string.IsNullOrWhiteSpace(NewTagName) || string.IsNullOrWhiteSpace(NewTagNodeId))
        {
            SystemMessage = "璇峰～鍐欏彉閲忓悕鍜?NodeId";
            return;
        }

        Tags.Add(new TagItem
        {
            Name = NewTagName,
            NodeId = NewTagNodeId,
            DataType = NewTagDataType,
            Category = NewTagCategory,
            Group = NewTagGroup,
            Direction = NewTagDirection,
            IsAlarm = NewTagIsAlarm,
            IsWritable = NewTagIsWritable,
            Description = "自定义变量"
        });
        NewTagName = string.Empty;
        NewTagNodeId = string.Empty;
        OnPropertyChanged(nameof(TagCount));
        RefreshMonitorView();
        SystemMessage = "已新增自定义变量";
        AddLog("配置", SystemMessage, "Info");
    }

    [RelayCommand]
    private void AddEventBinding()
    {
        if (string.IsNullOrWhiteSpace(NewEventTagName) || string.IsNullOrWhiteSpace(NewEventName))
        {
            SystemMessage = "请填写事件绑定信息";
            return;
        }

        EventBindings.Add(new EventBinding
        {
            TagName = NewEventTagName,
            TriggerCondition = NewEventTriggerCondition,
            EventName = NewEventName,
            ActionTarget = NewEventTarget,
            ActionParameter = NewEventParameter,
            Description = "用户自定义事件"
        });
        SystemMessage = "已新增事件绑定";
        AddLog("配置", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task ManualWriteAsync()
    {
        var tag = Tags.FirstOrDefault(t => t.Name.Equals(ManualWriteTagName, StringComparison.OrdinalIgnoreCase));
        if (tag is null) { SystemMessage = "未找到要写入的变量"; return; }
        if (!tag.IsWritable) { SystemMessage = "该变量不可写"; return; }
        try
        {
            object value = ConvertValue(ManualWriteValue, tag.DataType);
            await _opcUaService.WriteTagAsync(tag, value);
            tag.CurrentValue = ManualWriteValue;
            SystemMessage = $"写入成功：{tag.Name} = {ManualWriteValue}";
            AddLog("手动操作", SystemMessage, "Info");
            await RefreshTagsAsync();
        }
        catch (Exception ex)
        {
            SystemMessage = $"写入失败：{ex.Message}";
            AddLog("手动操作", SystemMessage, "Error");
        }
    }

    private void RefreshMonitorView()
    {
        MonitorTagsView.Filter = item =>
        {
            if (item is not TagItem tag) return false;
            if (IsMonitorIoPageVisible)
            {
                return IsPlcIoTag(tag);
            }
            if (SelectedMonitorCategory == "全部") return true;
            return tag.Category.Equals(SelectedMonitorCategory, StringComparison.OrdinalIgnoreCase)
                || tag.Group.Equals(SelectedMonitorCategory, StringComparison.OrdinalIgnoreCase);
        };
        MonitorTagsView.Refresh();
    }

    private static bool IsPlcIoTag(TagItem tag)
    {
        var name = (tag.Name ?? string.Empty).Trim().ToUpperInvariant();
        var nodeId = (tag.NodeId ?? string.Empty).Trim().ToUpperInvariant();
        var category = (tag.Category ?? string.Empty).Trim().ToUpperInvariant();
        var group = (tag.Group ?? string.Empty).Trim().ToUpperInvariant();

        if (category == "IO" || group == "INPUT" || group == "OUTPUT")
        {
            return true;
        }

        var ioPrefixes = new[] { "X_", "Y_", "IX", "IB", "IW", "ID", "QX", "QB", "QW", "QD", "I", "Q", "X", "Y" };
        if (ioPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return true;
        }

        return nodeId.Contains(".X", StringComparison.Ordinal)
            || nodeId.Contains(".Y", StringComparison.Ordinal)
            || nodeId.Contains(".I", StringComparison.Ordinal)
            || nodeId.Contains(".Q", StringComparison.Ordinal);
    }

    // ========== IO 监控相关属性和方法 ==========

    // IO 监控数据源
    private ObservableCollection<IoMonitorItem> _ioMonitorItems = new();

    // 当前显示类型 "DI" 或 "DO"
    [ObservableProperty] private string ioMonitorType = "DI";

    // 当前页码（每页 16 个点，分左右各 8 个）
    [ObservableProperty] private int ioMonitorCurrentPage = 0;

    // 标题
    public string IoMonitorTitle => IoMonitorType == "DI" ? "输入监控" : "输出监控";

    // DI 按钮高亮
    public string IoMonitorDiButtonBackground => IoMonitorType == "DI" ? "#2563EB" : "#E2E8F0";
    public string IoMonitorDiButtonForeground => IoMonitorType == "DI" ? "White" : "#0F172A";
    public string IoMonitorDoButtonBackground => IoMonitorType == "DO" ? "#2563EB" : "#E2E8F0";
    public string IoMonitorDoButtonForeground => IoMonitorType == "DO" ? "White" : "#0F172A";

    // 左半列表（索引 0-7）
    public ObservableCollection<IoMonitorItem> IoMonitorLeftItems { get; } = new();

    // 右半列表（索引 8-15）
    public ObservableCollection<IoMonitorItem> IoMonitorRightItems { get; } = new();

    partial void OnIoMonitorTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IoMonitorTitle));
        OnPropertyChanged(nameof(IoMonitorDiButtonBackground));
        OnPropertyChanged(nameof(IoMonitorDiButtonForeground));
        OnPropertyChanged(nameof(IoMonitorDoButtonBackground));
        OnPropertyChanged(nameof(IoMonitorDoButtonForeground));
        RefreshIoMonitorPagedItems();
    }

    partial void OnIoMonitorCurrentPageChanged(int value)
    {
        RefreshIoMonitorPagedItems();
        _ = WriteIoMonitorPageToPLC();
    }

    private void RefreshIoMonitorPagedItems()
    {
        IoMonitorLeftItems.Clear();
        IoMonitorRightItems.Clear();
        var offset = IoMonitorCurrentPage * 16;
        foreach (var item in _ioMonitorItems.Where(x => x.Direction == IoMonitorType && x.Index >= offset && x.Index < offset + 8))
            IoMonitorLeftItems.Add(item);
        foreach (var item in _ioMonitorItems.Where(x => x.Direction == IoMonitorType && x.Index >= offset + 8 && x.Index < offset + 16))
            IoMonitorRightItems.Add(item);
    }

    [RelayCommand]
    private void SwitchIoMonitorType(string type)
    {
        IoMonitorType = type;
        IoMonitorCurrentPage = 0;
    }

    [RelayCommand]
    private void IoMonitorPageUp()
    {
        if (IoMonitorCurrentPage > 0)
            IoMonitorCurrentPage--;
    }

    [RelayCommand]
    private void IoMonitorPageDown()
    {
        IoMonitorCurrentPage++;
    }

    // 初始化 IO 监控项（在连接成功后调用）
    private void InitializeIoMonitorItems()
    {
        _ioMonitorItems.Clear();

        // 创建 DI 监控点 (0-15)
        for (int i = 0; i < 16; i++)
        {
            _ioMonitorItems.Add(new IoMonitorItem
            {
                Index = i,
                Address = $"DI[{i}]",
                Direction = "DI",
                StatusTagName = $"OP80_DI_Mirror.Monitor[{i}].Status",
                CommentTagName = $"OP80_DI_Mirror.Monitor[{i}].Comment"
            });
        }

        // 创建 DO 监控点 (0-15)
        for (int i = 0; i < 16; i++)
        {
            _ioMonitorItems.Add(new IoMonitorItem
            {
                Index = i,
                Address = $"DO[{i}]",
                Direction = "DO",
                StatusTagName = $"OP80_DO_Mirror.Monitor[{i}].Status",
                CommentTagName = $"OP80_DO_Mirror.Monitor[{i}].Comment"
            });
        }

        RefreshIoMonitorPagedItems();
    }

    // 刷新 IO 监控状态（在自动刷新中调用）
    private void RefreshIoMonitorStates()
    {
        foreach (var item in _ioMonitorItems.Where(x => x.Direction == IoMonitorType))
        {
            var statusTag = FindTagByNameOrNodeId(item.StatusTagName);
            var commentTag = FindTagByNameOrNodeId(item.CommentTagName);

            if (statusTag != null)
                item.Status = string.Equals(statusTag.CurrentValue, "True", StringComparison.OrdinalIgnoreCase);
            if (commentTag != null)
                item.Comment = commentTag.CurrentValue ?? string.Empty;
        }
    }

    private async Task WriteIoMonitorPageToPLC()
    {
        try
        {
            var pageTagName = IoMonitorType == "DI" ? "OP80_DI_Mirror.Page" : "OP80_DO_Mirror.Page";
            var tag = FindTagByNameOrNodeId(pageTagName);
            if (tag != null)
            {
                await _opcUaService.WriteTagAsync(tag, IoMonitorCurrentPage);
            }
        }
        catch { /* ignore */ }
    }

    // ========== 趋势图 ==========

    private string BuildTrendSeriesPath(string category)
    {
        var samples = TrendSamples
            .Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Time)
            .ToList();

        if (samples.Count == 0)
        {
            return string.Empty;
        }

        var minTime = samples.Min(x => x.Time);
        var maxTime = samples.Max(x => x.Time);
        if (minTime == maxTime)
        {
            maxTime = minTime.AddMinutes(1);
        }

        var minValue = samples.Min(x => x.Value);
        var maxValue = samples.Max(x => x.Value);
        var valueRange = Math.Max(1.0, maxValue - minValue);
        var timeRange = Math.Max(1.0, (maxTime - minTime).TotalSeconds);

        var points = samples.Select(sample =>
        {
            var x = ((sample.Time - minTime).TotalSeconds / timeRange) * 100.0;
            var y = 92.0 - (((sample.Value - minValue) / valueRange) * 76.0);
            return $"{x.ToString("F1", CultureInfo.InvariantCulture)},{y.ToString("F1", CultureInfo.InvariantCulture)}";
        });

        return "M " + string.Join(" L ", points);
    }

    private DateTime GetTrendWindowStart()
    {
        return TrendSamples.Count == 0 ? DateTime.Now.AddMinutes(-5) : TrendSamples.Min(x => x.Time);
    }

    private DateTime GetTrendWindowEnd()
    {
        return TrendSamples.Count == 0 ? DateTime.Now : TrendSamples.Max(x => x.Time);
    }

    private string BuildTrendLatestText(string category, string label)
    {
        var latest = TrendSamples
            .Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Time)
            .FirstOrDefault();

        if (latest is null)
        {
            return $"{label}：--";
        }

        return category.Equals("OEE", StringComparison.OrdinalIgnoreCase)
            ? $"{label}：{latest.Value:F1}%"
            : $"{label}：{latest.Value:F0}";
    }

    private void RefreshTrendVisuals()
    {
        OnPropertyChanged(nameof(ProductionTrendPath));
        OnPropertyChanged(nameof(OeeTrendPath));
        OnPropertyChanged(nameof(AlarmTrendPath));
        OnPropertyChanged(nameof(TimeAxisSummary));
    }

    // ========== 程序监控 Trace ==========

    private void RebuildProgramMonitorTraceHistory()
    {
        _programMonitorTraceHistory.Clear();
        AppendProgramMonitorTraceHistory(FlowSteps);
    }

    private void AppendProgramMonitorTraceHistory(IEnumerable<FlowStepRecord> source)
    {
        foreach (var item in source
                     .Where(x => x.FlowId.Equals("F1", StringComparison.OrdinalIgnoreCase)
                              || x.FlowName.Equals("主线1", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Time))
        {
            var exists = _programMonitorTraceHistory.Any(x =>
                x.Time == item.Time &&
                x.StepNo == item.StepNo &&
                string.Equals(x.FlowId, item.FlowId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Comment, item.Comment, StringComparison.Ordinal));

            if (exists)
            {
                continue;
            }

            _programMonitorTraceHistory.Add(CloneFlowStepRecord(item));
        }

        _programMonitorTraceHistory.Sort((a, b) => a.Time.CompareTo(b.Time));
        ProgramMonitorTraceReplayMaximum = Math.Max(0, _programMonitorTraceHistory.Count - 1);
        OnPropertyChanged(nameof(HasProgramMonitorTraceHistory));

        if (!ProgramMonitorTraceReplayMode && !ProgramMonitorTracePaused && _programMonitorTraceHistory.Count > 0)
        {
            ProgramMonitorTraceReplayPosition = ProgramMonitorTraceReplayMaximum;
            _programMonitorTraceWindowEnd = _programMonitorTraceHistory[^1].Time;
        }
    }

    private static FlowStepRecord CloneFlowStepRecord(FlowStepRecord source) => new()
    {
        FlowId = source.FlowId,
        FlowName = source.FlowName,
        Time = source.Time,
        StartTime = source.StartTime,
        EndTime = source.EndTime,
        DurationSeconds = source.DurationSeconds,
        StepNo = source.StepNo,
        Icon = source.Icon,
        Title = source.Title,
        Comment = source.Comment,
        Result = source.Result,
        RelatedAlarm = source.RelatedAlarm,
        IsAbnormal = source.IsAbnormal,
        ShiftKey = source.ShiftKey,
        ArchiveDate = source.ArchiveDate,
        IsHighlighted = source.IsHighlighted
    };

    private IEnumerable<FlowStepRecord> GetTraceSource()
    {
        return _programMonitorTraceHistory.Count > 0 ? _programMonitorTraceHistory : FlowSteps.ToList();
    }

    private IEnumerable<FlowStepRecord> GetTraceWindowSamples()
    {
        var end = _programMonitorTraceFollowNow ? DateTime.Now : _programMonitorTraceWindowEnd;
        var start = end.AddMinutes(-Math.Max(1, ProgramMonitorTraceWindowMinutes));
        return GetTraceSource()
            .Where(x => x.Time >= start && x.Time <= end)
            .OrderBy(x => x.Time);
    }

    private IEnumerable<FlowStepRecord> GetTraceSamples(string flowId, string flowName)
    {
        return GetTraceWindowSamples()
            .Where(x => x.FlowId.Equals(flowId, StringComparison.OrdinalIgnoreCase) || x.FlowName.Equals(flowName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Time);
    }

    private IEnumerable<FlowStepRecord> GetSelectedTraceSamples()
    {
        return SelectedProgramMonitorTraceFlow switch
        {
            "主线2" => GetTraceSamples("F2", "主线2"),
            "主线3" => GetTraceSamples("F3", "主线3"),
            _ => GetTraceSamples("F1", "主线1")
        };
    }

    private double GetTraceAxisMaxStep()
    {
        var maxStep = 0;
        if (ProgramMonitorTraceShowLine1)
        {
            maxStep = Math.Max(maxStep, GetTraceSamples("F1", "主线1").Select(x => x.StepNo).DefaultIfEmpty(0).Max());
        }

        if (ProgramMonitorTraceShowLine2)
        {
            maxStep = Math.Max(maxStep, GetTraceSamples("F2", "主线2").Select(x => x.StepNo).DefaultIfEmpty(0).Max());
        }

        if (ProgramMonitorTraceShowLine3)
        {
            maxStep = Math.Max(maxStep, GetTraceSamples("F3", "主线3").Select(x => x.StepNo).DefaultIfEmpty(0).Max());
        }

        maxStep = Math.Max(10, maxStep);
        return Math.Ceiling(maxStep / 10.0) * 10.0;
    }

    private string GetTraceAxisLabel(int segment)
    {
        var maxStep = GetTraceAxisMaxStep();
        var value = segment switch
        {
            3 => maxStep,
            2 => maxStep * 2.0 / 3.0,
            1 => maxStep / 3.0,
            _ => 0
        };
        return Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
    }

    private string BuildTracePath(string flowId, string flowName)
    {
        var samples = GetTraceSamples(flowId, flowName).ToList();
        if (samples.Count == 0)
        {
            return string.Empty;
        }

        var start = GetTraceStart();
        var end = GetTraceEnd();
        if (start == end)
        {
            end = start.AddSeconds(1);
        }

        const double minStep = 0;
        var maxStep = GetTraceAxisMaxStep();
        var timeRange = Math.Max(1.0, (end - start).TotalSeconds);
        var points = samples.Select(sample =>
        {
            var x = ((sample.Time - start).TotalSeconds / timeRange) * 100.0;
            var y = 92.0 - (((sample.StepNo - minStep) / (maxStep - minStep)) * 76.0);
            return $"{x.ToString("F1", CultureInfo.InvariantCulture)},{y.ToString("F1", CultureInfo.InvariantCulture)}";
        });

        return "M " + string.Join(" L ", points);
    }

    private DateTime GetTraceStart()
    {
        var samples = GetTraceWindowSamples().ToList();
        return samples.Count == 0 ? DateTime.Now.AddMinutes(-Math.Max(1, ProgramMonitorTraceWindowMinutes)) : samples.Min(x => x.Time);
    }

    private DateTime GetTraceEnd()
    {
        var samples = GetTraceWindowSamples().ToList();
        return samples.Count == 0 ? DateTime.Now : samples.Max(x => x.Time);
    }

    private string BuildMainFlowTraceLatestText()
    {
        var parts = new List<string> { ProgramMonitorTraceReplayText };

        if (ProgramMonitorTraceShowLine1)
        {
            var latest1 = GetTraceSamples("F1", "主线1").LastOrDefault();
            parts.Add(latest1 is null ? "主线1：--" : $"主线1 STEP {latest1.StepNo:000}");
        }

        if (ProgramMonitorTraceShowLine2)
        {
            var latest2 = GetTraceSamples("F2", "主线2").LastOrDefault();
            parts.Add(latest2 is null ? "主线2：--" : $"主线2 STEP {latest2.StepNo:000}");
        }

        if (ProgramMonitorTraceShowLine3)
        {
            var latest3 = GetTraceSamples("F3", "主线3").LastOrDefault();
            parts.Add(latest3 is null ? "主线3：--" : $"主线3 STEP {latest3.StepNo:000}");
        }

        return string.Join(" / ", parts);
    }

    private void RefreshProgramMonitorTrace()
    {
        OnPropertyChanged(nameof(ProgramMonitorMainFlowTracePath));
        OnPropertyChanged(nameof(ProgramMonitorSubFlow2TracePath));
        OnPropertyChanged(nameof(ProgramMonitorSubFlow3TracePath));
        OnPropertyChanged(nameof(ProgramMonitorTraceAxisTopLabel));
        OnPropertyChanged(nameof(ProgramMonitorTraceAxisMidHighLabel));
        OnPropertyChanged(nameof(ProgramMonitorTraceAxisMidLowLabel));
        OnPropertyChanged(nameof(ProgramMonitorTraceStartLabel));
        OnPropertyChanged(nameof(ProgramMonitorTraceEndLabel));
        OnPropertyChanged(nameof(ProgramMonitorTraceLatestText));
        OnPropertyChanged(nameof(ProgramMonitorTraceWindowText));
        RefreshProgramMonitorCursorTexts();
    }

    public void UpdateProgramMonitorCursor(double normalizedPosition)
    {
        var sample = ResolveProgramMonitorSample(normalizedPosition);
        if (sample is null)
        {
            ProgramMonitorCursorText = "光标：--";
            return;
        }

        ProgramMonitorCursorText = $"光标：{sample.Time:HH:mm:ss} / {sample.FlowName} / STEP {sample.StepNo:000} / {sample.Comment}";
    }

    public void SetProgramMonitorCursorA(double normalizedPosition)
    {
        _programMonitorCursorASample = ResolveProgramMonitorSample(normalizedPosition);
        RefreshProgramMonitorCursorTexts();
    }

    public void SetProgramMonitorCursorB(double normalizedPosition)
    {
        _programMonitorCursorBSample = ResolveProgramMonitorSample(normalizedPosition);
        RefreshProgramMonitorCursorTexts();
    }

    public void ZoomProgramMonitorTrace(double delta)
    {
        var next = ProgramMonitorTraceWindowMinutes + (delta > 0 ? -1 : 1);
        ProgramMonitorTraceWindowMinutes = Math.Max(1, Math.Min(30, next));
    }

    public void ZoomProgramMonitorTraceRange(double startNormalized, double endNormalized)
    {
        var samples = GetSelectedTraceSamples().ToList();
        if (samples.Count < 2)
        {
            return;
        }

        var startSample = ResolveProgramMonitorSample(startNormalized);
        var endSample = ResolveProgramMonitorSample(endNormalized);
        if (startSample is null || endSample is null)
        {
            return;
        }

        if (endSample.Time < startSample.Time)
        {
            (startSample, endSample) = (endSample, startSample);
        }

        var rangeMinutes = Math.Max(1, Math.Ceiling((endSample.Time - startSample.Time).TotalMinutes));
        _programMonitorTraceFollowNow = false;
        ProgramMonitorTracePaused = true;
        ProgramMonitorTraceReplayMode = false;
        ProgramMonitorTraceReplayText = "模式：暂停采样";
        _programMonitorTraceWindowEnd = endSample.Time;
        ProgramMonitorTraceWindowMinutes = rangeMinutes;
        RefreshProgramMonitorTrace();
    }

    public void ResetProgramMonitorTraceZoom()
    {
        _programMonitorTraceFollowNow = true;
        ProgramMonitorTracePaused = false;
        ProgramMonitorTraceReplayMode = false;
        ProgramMonitorTraceReplayText = "模式：实时采样";
        _programMonitorTraceWindowEnd = DateTime.Now;
        ProgramMonitorTraceWindowMinutes = 5;
        ProgramMonitorTraceReplayPosition = ProgramMonitorTraceReplayMaximum;
        RefreshProgramMonitorTrace();
    }

    [RelayCommand]
    private void PauseProgramMonitorTrace()
    {
        ProgramMonitorTracePaused = true;
        ProgramMonitorTraceReplayMode = false;
        ProgramMonitorTraceReplayText = "模式：暂停采样";
        _programMonitorTraceFollowNow = false;
        _programMonitorTraceWindowEnd = (_programMonitorTraceHistory.LastOrDefault() ?? GetTraceWindowSamples().LastOrDefault())?.Time ?? DateTime.Now;
        RefreshProgramMonitorTrace();
    }

    [RelayCommand]
    private void ResumeProgramMonitorTrace()
    {
        ProgramMonitorTracePaused = false;
        ProgramMonitorTraceReplayMode = false;
        ProgramMonitorTraceReplayText = "模式：实时采样";
        _programMonitorTraceFollowNow = true;
        _programMonitorTraceWindowEnd = DateTime.Now;
        ProgramMonitorTraceReplayPosition = ProgramMonitorTraceReplayMaximum;
        RefreshProgramMonitorTrace();
    }

    [RelayCommand]
    private void ReturnProgramMonitorTraceToRealtime()
    {
        ResumeProgramMonitorTrace();
    }

    [RelayCommand]
    private void EnterProgramMonitorTraceReplay()
    {
        if (_programMonitorTraceHistory.Count == 0)
        {
            return;
        }

        ProgramMonitorTracePaused = true;
        ProgramMonitorTraceReplayMode = true;
        ProgramMonitorTraceReplayText = "模式：历史回放";
        _programMonitorTraceFollowNow = false;
        ProgramMonitorTraceReplayPosition = Math.Max(0, Math.Min(ProgramMonitorTraceReplayMaximum, ProgramMonitorTraceReplayPosition));
        _programMonitorTraceWindowEnd = _programMonitorTraceHistory[ProgramMonitorTraceReplayPosition].Time;
        RefreshProgramMonitorTrace();
    }

    private FlowStepRecord? ResolveProgramMonitorSample(double normalizedPosition)
    {
        var samples = GetSelectedTraceSamples().ToList();
        if (samples.Count == 0)
        {
            return null;
        }

        normalizedPosition = Compat.Clamp(normalizedPosition, 0, 1);
        var index = (int)Math.Round((samples.Count - 1) * normalizedPosition, MidpointRounding.AwayFromZero);
        index = Math.Max(0, Math.Min(samples.Count - 1, index));
        return samples[index];
    }

    private void RefreshProgramMonitorCursorTexts()
    {
        ProgramMonitorCursorAText = _programMonitorCursorASample is null
            ? "光标A：--"
            : $"光标A：{_programMonitorCursorASample.Time:HH:mm:ss} / STEP {_programMonitorCursorASample.StepNo:000}";

        ProgramMonitorCursorBText = _programMonitorCursorBSample is null
            ? "光标B：--"
            : $"光标B：{_programMonitorCursorBSample.Time:HH:mm:ss} / STEP {_programMonitorCursorBSample.StepNo:000}";

        if (_programMonitorCursorASample is null || _programMonitorCursorBSample is null)
        {
            ProgramMonitorCursorDeltaText = "Δ：--";
            return;
        }

        var deltaTime = (_programMonitorCursorBSample.Time - _programMonitorCursorASample.Time).Duration();
        var deltaStep = Math.Abs(_programMonitorCursorBSample.StepNo - _programMonitorCursorASample.StepNo);
        var sameFlow = string.Equals(_programMonitorCursorASample.FlowId, _programMonitorCursorBSample.FlowId, StringComparison.OrdinalIgnoreCase);
        ProgramMonitorCursorDeltaText = sameFlow
            ? $"Δ：{deltaTime.TotalSeconds:F1}s / {deltaStep} step"
            : $"Δ：{deltaTime.TotalSeconds:F1}s / 跨流程";
    }

    [RelayCommand]
    private void LocateProgramMonitorTraceTime()
    {
        if (_programMonitorTraceHistory.Count == 0 || string.IsNullOrWhiteSpace(ProgramMonitorTraceLocateTime))
        {
            return;
        }

        DateTime targetTime;
        var text = ProgramMonitorTraceLocateTime.Trim();
        if (!DateTime.TryParse(text, out targetTime))
        {
            if (TimeSpan.TryParse(text, out var timeOfDay))
            {
                var baseDate = _programMonitorTraceHistory.Last().Time.Date;
                targetTime = baseDate.Add(timeOfDay);
            }
            else
            {
                SystemMessage = $"无法识别定位时间：{ProgramMonitorTraceLocateTime}";
                return;
            }
        }

        var nearest = _programMonitorTraceHistory
            .OrderBy(x => Math.Abs((x.Time - targetTime).Ticks))
            .FirstOrDefault();
        if (nearest is null)
        {
            return;
        }

        ProgramMonitorTracePaused = true;
        ProgramMonitorTraceReplayMode = true;
        ProgramMonitorTraceReplayText = "模式：历史回放";
        _programMonitorTraceFollowNow = false;
        _programMonitorTraceWindowEnd = nearest.Time;
        var index = _programMonitorTraceHistory.FindIndex(x =>
            x.Time == nearest.Time &&
            x.StepNo == nearest.StepNo &&
            string.Equals(x.FlowId, nearest.FlowId, StringComparison.OrdinalIgnoreCase));
        ProgramMonitorTraceReplayPosition = Math.Max(0, index);
        RefreshProgramMonitorTrace();
        ProgramMonitorCursorText = $"定位：{nearest.Time:HH:mm:ss} / {nearest.FlowName} / STEP {nearest.StepNo:000} / {nearest.Comment}";
    }

    [RelayCommand]
    private async Task ExportProgramMonitorTraceCsvAsync()
    {
        var exportDir = Path.Combine(GetProjectRoot(), "Generated", "Trace");
        Directory.CreateDirectory(exportDir);
        var filePath = Path.Combine(exportDir, $"ProgramTrace_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var rows = GetTraceWindowSamples()
            .Where(x =>
                (ProgramMonitorTraceShowLine1 && (x.FlowId.Equals("F1", StringComparison.OrdinalIgnoreCase) || x.FlowName.Equals("主线1", StringComparison.OrdinalIgnoreCase))) ||
                (ProgramMonitorTraceShowLine2 && (x.FlowId.Equals("F2", StringComparison.OrdinalIgnoreCase) || x.FlowName.Equals("主线2", StringComparison.OrdinalIgnoreCase))) ||
                (ProgramMonitorTraceShowLine3 && (x.FlowId.Equals("F3", StringComparison.OrdinalIgnoreCase) || x.FlowName.Equals("主线3", StringComparison.OrdinalIgnoreCase))))
            .OrderBy(x => x.Time)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Time,FlowId,FlowName,StepNo,Comment,Result,DurationSeconds");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                row.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                EscapeCsv(row.FlowId),
                EscapeCsv(row.FlowName),
                row.StepNo.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(row.Comment),
                EscapeCsv(row.Result),
                row.DurationSeconds.ToString("F3", CultureInfo.InvariantCulture)));
        }

        await Compat.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(false));
        SystemMessage = $"Trace 已导出：{filePath}";
        AddLog("程序监控", SystemMessage, "Info");
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    [RelayCommand]
    private async Task SaveProgramMonitorTraceSessionAsync()
    {
        var saveDialog = new SaveFileDialog
        {
            Title = "保存 Trace 会话",
            Filter = "Trace 会话 (*.json)|*.json",
            FileName = $"ProgramTraceSession_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            InitialDirectory = Path.Combine(GetProjectRoot(), "Generated", "Trace")
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(saveDialog.FileName) ?? Path.Combine(GetProjectRoot(), "Generated", "Trace"));
        var session = new ProgramTraceSessionFile
        {
            SavedAt = DateTime.Now,
            WindowMinutes = ProgramMonitorTraceWindowMinutes,
            SelectedFlow = SelectedProgramMonitorTraceFlow,
            ShowLine1 = ProgramMonitorTraceShowLine1,
            ShowLine2 = ProgramMonitorTraceShowLine2,
            ShowLine3 = ProgramMonitorTraceShowLine3,
            Samples = _programMonitorTraceHistory
                .Select(x => new ProgramTraceSessionSample
                {
                    FlowId = x.FlowId,
                    FlowName = x.FlowName,
                    Time = x.Time,
                    StartTime = x.StartTime,
                    EndTime = x.EndTime,
                    DurationSeconds = x.DurationSeconds,
                    StepNo = x.StepNo,
                    Title = x.Title,
                    Comment = x.Comment,
                    Result = x.Result,
                    RelatedAlarm = x.RelatedAlarm,
                    IsAbnormal = x.IsAbnormal
                })
                .ToList()
        };

        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        await Compat.WriteAllTextAsync(saveDialog.FileName, json, new UTF8Encoding(false));
        SystemMessage = $"Trace 会话已保存：{saveDialog.FileName}";
        AddLog("程序监控", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task LoadProgramMonitorTraceSessionAsync()
    {
        var openDialog = new OpenFileDialog
        {
            Title = "加载 Trace 会话",
            Filter = "Trace 会话 (*.json)|*.json",
            InitialDirectory = Path.Combine(GetProjectRoot(), "Generated", "Trace")
        };

        if (openDialog.ShowDialog() != true)
        {
            return;
        }

        var json = await Compat.ReadAllTextAsync(openDialog.FileName, Encoding.UTF8);
        var session = JsonSerializer.Deserialize<ProgramTraceSessionFile>(json);
        if (session is null || session.Samples.Count == 0)
        {
            SystemMessage = "Trace 会话文件为空或格式无效";
            AddLog("程序监控", SystemMessage, "Warning");
            return;
        }

        _programMonitorTraceHistory.Clear();
        foreach (var sample in session.Samples.OrderBy(x => x.Time))
        {
            _programMonitorTraceHistory.Add(new FlowStepRecord
            {
                FlowId = sample.FlowId,
                FlowName = sample.FlowName,
                Time = sample.Time,
                StartTime = sample.StartTime,
                EndTime = sample.EndTime,
                DurationSeconds = sample.DurationSeconds,
                StepNo = sample.StepNo,
                Icon = "●",
                Title = sample.Title,
                Comment = sample.Comment,
                Result = sample.Result,
                RelatedAlarm = sample.RelatedAlarm,
                IsAbnormal = sample.IsAbnormal
            });
        }

        ProgramMonitorTraceWindowMinutes = Math.Max(1, Math.Min(30, session.WindowMinutes <= 0 ? 5 : session.WindowMinutes));
        SelectedProgramMonitorTraceFlow = string.IsNullOrWhiteSpace(session.SelectedFlow) ? "主线1" : session.SelectedFlow;
        ProgramMonitorTraceShowLine1 = session.ShowLine1;
        ProgramMonitorTraceShowLine2 = session.ShowLine2;
        ProgramMonitorTraceShowLine3 = session.ShowLine3;
        ProgramMonitorTracePaused = true;
        ProgramMonitorTraceReplayMode = true;
        ProgramMonitorTraceReplayText = "模式：加载会话";
        ProgramMonitorTraceReplayMaximum = Math.Max(0, _programMonitorTraceHistory.Count - 1);
        ProgramMonitorTraceReplayPosition = ProgramMonitorTraceReplayMaximum;
        _programMonitorTraceFollowNow = false;
        _programMonitorTraceWindowEnd = _programMonitorTraceHistory[^1].Time;
        RefreshProgramMonitorTrace();

        SystemMessage = $"Trace 会话已加载：{openDialog.FileName}";
        AddLog("程序监控", SystemMessage, "Info");
    }

    private sealed class ProgramTraceSessionFile
    {
        public DateTime SavedAt { get; set; }
        public double WindowMinutes { get; set; }
        public string SelectedFlow { get; set; } = "主线1";
        public bool ShowLine1 { get; set; } = true;
        public bool ShowLine2 { get; set; } = true;
        public bool ShowLine3 { get; set; } = true;
        public List<ProgramTraceSessionSample> Samples { get; set; } = new();
    }

    private sealed class ProgramTraceSessionSample
    {
        public string FlowId { get; set; } = string.Empty;
        public string FlowName { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationSeconds { get; set; }
        public int StepNo { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string RelatedAlarm { get; set; } = string.Empty;
        public bool IsAbnormal { get; set; }
    }
}
