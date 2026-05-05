#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels.Shell;
using Serilog;

namespace ApexHMI.ViewModels.Modules;

/// <summary>
/// 开放平台设计编辑器 ViewModel。
/// 使用 ProjectDocument / WidgetInstance 模型替代 V1 的 DesignerPage / DesignerElement。
/// </summary>
public partial class DesignerEditorViewModel : ModuleViewModelBase
{
    private readonly IProjectEditorService _projectEditor;
    private readonly IWidgetEditorService _widgetEditor;
    private readonly RuntimeProjectService _runtimeProjectService;
    private readonly WidgetBlockGenerator _blockGenerator;
    private readonly EditStack _editStack = new();
    private bool _suppressEditRecording;

    /// <summary>设计模式 widget 渲染所需的工厂。</summary>
    public IWidgetViewFactory WidgetViewFactory { get; }

    /// <summary>设计模式数据上下文（无 OPC UA 推送，但暴露 Shell 供业务 widget 取真实数据）。</summary>
    public IWidgetDataContext DesignModeContext { get; }

    /// <summary>工具箱控件类型列表（与 WidgetEditorService.DefaultProperties 对齐）。</summary>
    public static readonly IReadOnlyList<string> ToolboxTypes = new[]
    {
        "text", "bool-lamp", "numeric-readonly", "button",
        "motor", "cylinder", "axis", "robot",
        "stopper", "alarm-banner", "page-button"
    };

    public DesignerEditorViewModel(
        MainViewModel shell,
        IProjectEditorService projectEditor,
        IWidgetEditorService widgetEditor,
        RuntimeProjectService runtimeProjectService,
        WidgetBlockGenerator blockGenerator,
        IWidgetViewFactory widgetViewFactory)
        : base(shell, "画布设计")
    {
        _projectEditor = projectEditor;
        _widgetEditor = widgetEditor;
        _runtimeProjectService = runtimeProjectService;
        _blockGenerator = blockGenerator;
        WidgetViewFactory = widgetViewFactory;
        DesignModeContext = new DesignModeWidgetDataContext(shell);
        InitDocument();
    }

    // ========== 工程文档 ==========

    [ObservableProperty]
    private ProjectDocument _document = new();

    /// <summary>保存/发布操作的状态提示，显示在标题栏。</summary>
    [ObservableProperty]
    private string _saveStatus = string.Empty;

    // ========== 选中项 ==========

    [ObservableProperty]
    private PageDefinition? _selectedPage;

    [ObservableProperty]
    private WidgetInstance? _selectedWidget;

    /// <summary>当前选中页面的控件列表，绑定到画布 ItemsControl。</summary>
    public ObservableCollection<WidgetInstance> CurrentWidgets { get; } = new();

    /// <summary>设计器画布渲染项：模型 + 预创建的真容视图（避免 XAML 中即时调用工厂引发卡死）。</summary>
    public ObservableCollection<DesignerWidgetItem> CurrentWidgetItems { get; } = new();

    /// <summary>当前选中控件的属性列表，绑定到属性编辑器 ItemsControl。</summary>
    public ObservableCollection<WidgetPropertyItem> CurrentWidgetProperties { get; } = new();

    /// <summary>已订阅 PropertyChanged 的 widget → 处理器映射，便于解订避免内存泄漏。</summary>
    private readonly Dictionary<WidgetInstance, PropertyChangedEventHandler> _widgetHandlers = new();

    private FrameworkElement BuildWidgetView(WidgetInstance widget) =>
        WidgetViewFactory.Create(widget, DesignModeContext);

    private void RefreshCurrentWidgetItems()
    {
        // 解订所有旧 widget
        foreach (var kv in _widgetHandlers)
            kv.Key.PropertyChanged -= kv.Value;
        _widgetHandlers.Clear();
        CurrentWidgetItems.Clear();

        if (SelectedPage is null) return;
        foreach (var w in SelectedPage.Widgets)
            AddWidgetItem(w);
    }

    private void AddWidgetItem(WidgetInstance widget)
    {
        var item = new DesignerWidgetItem(widget, BuildWidgetView(widget));
        PropertyChangedEventHandler handler = (_, e) => OnWidgetModelChanged(item, e);
        widget.PropertyChanged += handler;
        _widgetHandlers[widget] = handler;
        CurrentWidgetItems.Add(item);
    }

    private void RemoveWidgetItem(WidgetInstance widget)
    {
        var item = CurrentWidgetItems.FirstOrDefault(i => ReferenceEquals(i.Model, widget));
        if (item is null) return;

        if (_widgetHandlers.TryGetValue(widget, out var handler))
        {
            widget.PropertyChanged -= handler;
            _widgetHandlers.Remove(widget);
        }
        CurrentWidgetItems.Remove(item);
    }

    /// <summary>
    /// widget 模型变化时按需重建视图：
    /// - Properties / Binding / ActionType / ActionParam / TypeId 改变 → 重建 View
    /// - X/Y/Width/Height 改变 → 不重建（已有 binding 自动同步 Canvas.Left/Top + Border 尺寸）
    /// </summary>
    private void OnWidgetModelChanged(DesignerWidgetItem item, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WidgetInstance.Properties):
            case nameof(WidgetInstance.Binding):
            case nameof(WidgetInstance.ActionType):
            case nameof(WidgetInstance.ActionParam):
            case nameof(WidgetInstance.TypeId):
                item.View = BuildWidgetView(item.Model);
                break;
        }
    }

    // ========== B-07: Tag 数据源 ==========

    /// <summary>可用 Tag 列表，用于绑定选择器下拉框。</summary>
    public IEnumerable<TagItem> AvailableTags => Shell.Tags;

    /// <summary>
    /// 当前选中 widget 的可用设备名列表（按 TypeId 自动从 Shell 取真实设备）。
    /// 用于属性面板 deviceName 下拉框。
    /// </summary>
    public IEnumerable<string> AvailableDeviceNames
    {
        get
        {
            if (SelectedWidget?.TypeId is null) return Enumerable.Empty<string>();
            return SelectedWidget.TypeId.ToLowerInvariant() switch
            {
                "cylinder" or "manual-cylinder-block" =>
                    Shell.ManualCylinderBlockCards.Select(c =>
                        string.IsNullOrWhiteSpace(c.DisplayName) ? $"Cyl{c.CylinderIndex}" : c.DisplayName),
                "axis" or "manual-axis-block" =>
                    Shell.ManualAxisBlockCards.Select(a =>
                        string.IsNullOrWhiteSpace(a.DisplayName) ? $"Axis{a.AxisIndex}" : a.DisplayName),
                "stopper" or "manual-stopper-block" =>
                    Shell.Tags
                        .Where(t => t.Name?.IndexOf("stopper", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(t => t.Name),
                _ => Enumerable.Empty<string>(),
            };
        }
    }

    // ========== 选中页切换 ==========

    partial void OnSelectedPageChanged(PageDefinition? value)
    {
        CurrentWidgets.Clear();
        if (value is not null)
        {
            foreach (var w in value.Widgets)
                CurrentWidgets.Add(w);
        }

        RefreshCurrentWidgetItems();
        SelectedWidget = null;
    }

    partial void OnSelectedWidgetChanged(WidgetInstance? value)
    {
        // 取消之前的订阅
        foreach (var item in CurrentWidgetProperties)
            item.PropertyChanged -= OnWidgetPropertyItemChanged;

        CurrentWidgetProperties.Clear();
        if (value is not null)
        {
            foreach (var kv in value.Properties)
            {
                var propItem = new WidgetPropertyItem(kv.Key, kv.Value);
                propItem.PropertyChanged += OnWidgetPropertyItemChanged;
                CurrentWidgetProperties.Add(propItem);
            }
        }

        OnPropertyChanged(nameof(CurrentBindingTagId));
        OnPropertyChanged(nameof(AvailableDeviceNames));
    }

    private void OnWidgetPropertyItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetPropertyItem.Value) && sender is WidgetPropertyItem item)
        {
            UpdateWidgetProperty(item.Key, item.Value);
        }
    }

    // ========== 页面命令 ==========

    [RelayCommand]
    private void AddPage()
    {
        var title = $"页面{Document.Pages.Count + 1}";
        var page = _projectEditor.AddPage(Document, title);
        SelectedPage = page;
        Log.Information("DesignerEditor: 已添加页面 title={Title} routeKey={RouteKey}", title, page.RouteKey);
    }

    [RelayCommand]
    private void RemovePage()
    {
        if (SelectedPage is null) return;

        if (!_projectEditor.RemovePage(Document, SelectedPage.Id, out var error))
        {
            Log.Warning("DesignerEditor: 删除页面被阻止 —— {Error}", error);
            return;
        }

        var next = Document.Pages.FirstOrDefault();
        SelectedPage = next;
        if (next is null)
        {
            SelectedWidget = null;
            CurrentWidgets.Clear();
        }
    }

    // ========== 控件命令 ==========

    [RelayCommand]
    private void AddWidget(string? typeId)
    {
        if (SelectedPage is null || string.IsNullOrWhiteSpace(typeId)) return;

        var widget = _widgetEditor.AddWidget(SelectedPage, typeId, 40, 40);
        CurrentWidgets.Add(widget);
        AddWidgetItem(widget);
        SelectedWidget = widget;

        _editStack.Execute(new AddWidgetEdit(_widgetEditor, SelectedPage, widget));
        OnPropertyChanged(nameof(CanUndo));
        Log.Information("DesignerEditor: 已添加控件 typeId={TypeId} id={Id}", typeId, widget.Id);
    }

    [RelayCommand]
    private void AddWidgetAtDrop(string? payload)
    {
        if (SelectedPage is null || string.IsNullOrWhiteSpace(payload)) return;

        var parts = payload.Split('|');
        if (parts.Length < 3) return;
        var typeId = parts[0];
        if (!double.TryParse(parts[1], out var x)) x = 40;
        if (!double.TryParse(parts[2], out var y)) y = 40;

        var widget = _widgetEditor.AddWidget(SelectedPage, typeId, x, y);
        CurrentWidgets.Add(widget);
        AddWidgetItem(widget);
        SelectedWidget = widget;
        Log.Information("DesignerEditor: 拖拽添加控件 typeId={TypeId} x={X} y={Y}", typeId, x, y);
    }

    [RelayCommand]
    private void RemoveSelectedWidget()
    {
        if (SelectedPage is null || SelectedWidget is null) return;

        var widget = SelectedWidget;
        var index = SelectedPage.Widgets.IndexOf(widget);
        _editStack.Execute(new RemoveWidgetEdit(_widgetEditor, SelectedPage, widget, index));

        _widgetEditor.RemoveWidget(SelectedPage, SelectedWidget.Id);
        CurrentWidgets.Remove(SelectedWidget);
        RemoveWidgetItem(SelectedWidget);
        SelectedWidget = null;
        OnPropertyChanged(nameof(CanUndo));
    }

    [RelayCommand]
    private void SelectWidget(WidgetInstance? widget)
    {
        SelectedWidget = widget;
    }

    // ========== B-06: 属性修改 ==========

    /// <summary>修改当前选中控件的属性键值。</summary>
    public void UpdateWidgetProperty(string key, string? value)
    {
        if (SelectedWidget is null) return;

        var oldValue = SelectedWidget.Properties.TryGetValue(key, out var existing) ? existing : null;
        _widgetEditor.UpdateProperty(SelectedWidget, key, value);

        // 同步 CurrentWidgetProperties 以保持 UI 一致（防止递归）
        var item = CurrentWidgetProperties.FirstOrDefault(p => p.Key == key);
        if (item is not null)
        {
            item.PropertyChanged -= OnWidgetPropertyItemChanged;
            item.Value = value ?? string.Empty;
            item.PropertyChanged += OnWidgetPropertyItemChanged;
        }
        else if (value is not null)
        {
            var newItem = new WidgetPropertyItem(key, value);
            newItem.PropertyChanged += OnWidgetPropertyItemChanged;
            CurrentWidgetProperties.Add(newItem);
        }

        // 记录撤销项（仅在用户操作时，Undo/Redo 回放时不记录）
        if (!_suppressEditRecording && string.Equals(oldValue, value) != true)
        {
            _editStack.Execute(new PropertyEdit(_widgetEditor, SelectedWidget, key, oldValue, value));
            OnPropertyChanged(nameof(CanUndo));
        }
    }

    /// <summary>移动当前选中控件（拖拽实时调用，不记录撤销）。</summary>
    public void MoveSelectedWidget(double x, double y)
    {
        if (SelectedWidget is null) return;
        _widgetEditor.MoveWidget(SelectedWidget, x, y);
    }

    /// <summary>拖拽结束后将移动写入撤销栈。</summary>
    public void CommitMove(double oldX, double oldY, double newX, double newY)
    {
        if (SelectedWidget is null) return;
        _editStack.Execute(new MoveWidgetEdit(_widgetEditor, SelectedWidget, oldX, oldY, newX, newY));
        OnPropertyChanged(nameof(CanUndo));
    }

    /// <summary>调整当前选中控件尺寸。</summary>
    public void ResizeSelectedWidget(double width, double height)
    {
        if (SelectedWidget is null) return;
        _widgetEditor.ResizeWidget(SelectedWidget, width, height);
    }

    // ========== B-07: 绑定修改 ==========

    /// <summary>当前选中控件的绑定 Tag 名称（用于双向绑定到 ComboBox）。</summary>
    public string? CurrentBindingTagId
    {
        get => SelectedWidget?.Binding?.TagId;
        set => UpdateWidgetBinding(value);
    }

    /// <summary>修改当前选中控件的 OPC UA 绑定。</summary>
    public void UpdateWidgetBinding(string? tagId)
    {
        if (SelectedWidget is null) return;

        if (string.IsNullOrWhiteSpace(tagId))
        {
            _widgetEditor.UpdateBinding(SelectedWidget, null);
        }
        else
        {
            _widgetEditor.UpdateBinding(SelectedWidget, new BindingSpec
            {
                TagId = tagId,
                AccessMode = BindingAccessMode.Subscribe,
                DataType = "String"
            });
        }
    }

    // ========== 动作配置 ==========

    /// <summary>修改当前选中控件的动作类型。</summary>
    public void UpdateWidgetAction(string? actionType, string? actionParam)
    {
        if (SelectedWidget is null) return;
        SelectedWidget.ActionType = actionType;
        SelectedWidget.ActionParam = actionParam;
    }

    // ========== B-08: 主题 ==========

    /// <summary>可用主题预设列表。</summary>
    public IReadOnlyList<ThemePreset> ThemePresets => ThemePreset.Presets;

    [ObservableProperty]
    private ThemePreset? _selectedTheme;

    [RelayCommand]
    private void ApplyTheme(ThemePreset? theme)
    {
        if (theme is null || SelectedPage is null) return;

        SelectedTheme = theme;

        // 批量更新当前页面所有控件的配色属性
        var updates = theme.ToPropertyUpdates();
        foreach (var widget in SelectedPage.Widgets)
        {
            foreach (var kv in updates)
            {
                _widgetEditor.UpdateProperty(widget, kv.Key, kv.Value);
            }
        }

        // 刷新选中控件的属性网格
        if (SelectedWidget is not null)
        {
            OnSelectedWidgetChanged(SelectedWidget);
        }

        Log.Information("DesignerEditor: 已应用主题 theme={Theme}", theme.Name);
    }

    // ========== B-09: 撤销/重做 ==========

    public bool CanUndo => _editStack.CanUndo;
    public bool CanRedo => _editStack.CanRedo;
    public string? UndoDescription => _editStack.UndoDescription;
    public string? RedoDescription => _editStack.RedoDescription;

    [RelayCommand]
    private void Undo()
    {
        _suppressEditRecording = true;
        try
        {
            _editStack.Undo();

            // 刷新 UI
            if (SelectedPage is not null)
            {
                CurrentWidgets.Clear();
                foreach (var w in SelectedPage.Widgets)
                    CurrentWidgets.Add(w);
                RefreshCurrentWidgetItems();
            }

            // 刷新属性网格
            if (SelectedWidget is not null)
            {
                OnSelectedWidgetChanged(SelectedWidget);
            }
        }
        finally
        {
            _suppressEditRecording = false;
        }

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(RedoDescription));
    }

    [RelayCommand]
    private void Redo()
    {
        _suppressEditRecording = true;
        try
        {
            _editStack.Redo();

            if (SelectedPage is not null)
            {
                CurrentWidgets.Clear();
                foreach (var w in SelectedPage.Widgets)
                    CurrentWidgets.Add(w);
            }

            if (SelectedWidget is not null)
            {
                OnSelectedWidgetChanged(SelectedWidget);
            }
        }
        finally
        {
            _suppressEditRecording = false;
        }

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(RedoDescription));
    }

    // ========== 批量生成功能块 ==========

    /// <summary>可选的功能块类型列表。</summary>
    public IReadOnlyList<string> BatchBlockTypes => WidgetBlockGenerator.BlockTypes;

    /// <summary>功能块中文名称映射，用于 UI 显示。</summary>
    public IReadOnlyDictionary<string, string> BatchBlockTypeLabels => WidgetBlockGenerator.BlockTypeLabels;

    [ObservableProperty]
    private string _batchBlockType = "cylinder";

    [ObservableProperty]
    private string _batchNamePrefix = "CYL";

    [ObservableProperty]
    private int _batchCount = 3;

    [ObservableProperty]
    private double _batchStartX = 40;

    [ObservableProperty]
    private double _batchStartY = 40;

    [ObservableProperty]
    private bool _batchLayoutHorizontal = true;

    partial void OnBatchBlockTypeChanged(string value)
    {
        // 自动更新默认前缀
        BatchNamePrefix = value.ToUpperInvariant() switch
        {
            "CYLINDER" => "CYL",
            "MOTOR"    => "MOT",
            "AXIS"     => "AXIS",
            "ROBOT"    => "ROB",
            "STOPPER"  => "STP",
            _          => value.ToUpperInvariant()[..Math.Min(3, value.Length)]
        };
    }

    [RelayCommand]
    private void BatchGenerate()
    {
        if (SelectedPage is null)
        {
            Log.Warning("DesignerEditor: 批量生成失败，未选中页面");
            return;
        }

        var count = Math.Max(1, Math.Min(BatchCount, 50));
        var deviceNames = ResolveBatchDeviceNames(BatchBlockType, count);

        var generated = _blockGenerator.GenerateForDevices(
            SelectedPage,
            BatchBlockType,
            deviceNames,
            BatchStartX,
            BatchStartY,
            BatchLayoutHorizontal);

        foreach (var w in generated)
        {
            CurrentWidgets.Add(w);
            AddWidgetItem(w);
        }

        if (generated.Count > 0)
            SelectedWidget = generated[0];

        Log.Information("DesignerEditor: 批量生成 {Count} 个 {BlockType} 功能块", generated.Count, BatchBlockType);
    }

    /// <summary>
    /// 解析批量生成时使用的设备名列表。
    /// 优先从 Shell 取真实设备（IO 已导入时），否则用 前缀+序号 占位名。
    /// </summary>
    private IReadOnlyList<string> ResolveBatchDeviceNames(string blockType, int desiredCount)
    {
        var fromShell = blockType.ToLowerInvariant() switch
        {
            "cylinder" => Shell.ManualCylinderBlockCards.Select(c =>
                string.IsNullOrWhiteSpace(c.DisplayName) ? $"Cyl{c.CylinderIndex}" : c.DisplayName).ToList(),
            "axis"     => Shell.ManualAxisBlockCards.Select(a =>
                string.IsNullOrWhiteSpace(a.DisplayName) ? $"Axis{a.AxisIndex}" : a.DisplayName).ToList(),
            _ => new List<string>()
        };

        if (fromShell.Count > 0)
        {
            return fromShell.Take(desiredCount).ToList();
        }

        // 占位命名
        var prefix = string.IsNullOrWhiteSpace(BatchNamePrefix) ? "DEV" : BatchNamePrefix;
        var list = new List<string>();
        for (int i = 0; i < desiredCount; i++) list.Add($"{prefix}{i + 1}");
        return list;
    }

    // ========== 保存 / 发布 ==========

    [RelayCommand]
    private void Save()
    {
        try
        {
            _runtimeProjectService.Save(Document);
            SaveStatus = $"已保存  {DateTime.Now:HH:mm:ss}";
            Log.Information("DesignerEditor: 工程已保存");
        }
        catch (Exception ex)
        {
            SaveStatus = "保存失败";
            Log.Error(ex, "DesignerEditor: 保存工程失败");
        }
    }

    [RelayCommand]
    private async Task PublishAsync()
    {
        try
        {
            _runtimeProjectService.Save(Document);
            SaveStatus = $"已发布  {DateTime.Now:HH:mm:ss}";

            if (Shell is MainWindowViewModel mvm)
                await mvm.PublishProjectAsync();

            Log.Information("DesignerEditor: 工程已发布到运行时");
        }
        catch (Exception ex)
        {
            SaveStatus = "发布失败";
            Log.Error(ex, "DesignerEditor: 发布工程失败");
        }
    }

    // ========== 文档初始化 ==========

    private void InitDocument()
    {
        // 共享运行时已加载的 ProjectDocument 实例（InitializeDynamicRuntime 先于此方法执行）
        Document = _runtimeProjectService.Current ?? _runtimeProjectService.LoadDefault();
        SelectedPage = Document.Pages.FirstOrDefault();
    }
}
