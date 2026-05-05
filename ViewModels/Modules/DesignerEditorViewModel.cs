#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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
        WidgetBlockGenerator blockGenerator)
        : base(shell, "画布设计")
    {
        _projectEditor = projectEditor;
        _widgetEditor = widgetEditor;
        _runtimeProjectService = runtimeProjectService;
        _blockGenerator = blockGenerator;
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

    /// <summary>当前选中控件的属性列表，绑定到属性编辑器 ItemsControl。</summary>
    public ObservableCollection<WidgetPropertyItem> CurrentWidgetProperties { get; } = new();

    // ========== B-07: Tag 数据源 ==========

    /// <summary>可用 Tag 列表，用于绑定选择器下拉框。</summary>
    public IEnumerable<TagItem> AvailableTags => Shell.Tags;

    // ========== 选中页切换 ==========

    partial void OnSelectedPageChanged(PageDefinition? value)
    {
        CurrentWidgets.Clear();
        if (value is not null)
        {
            foreach (var w in value.Widgets)
                CurrentWidgets.Add(w);
        }

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

        if (string.IsNullOrWhiteSpace(BatchNamePrefix))
        {
            Log.Warning("DesignerEditor: 批量生成失败，命名前缀为空");
            return;
        }

        var count = Math.Max(1, Math.Min(BatchCount, 20));
        var generated = _blockGenerator.Generate(
            SelectedPage,
            BatchBlockType,
            BatchNamePrefix,
            count,
            BatchStartX,
            BatchStartY,
            BatchLayoutHorizontal);

        foreach (var w in generated)
            CurrentWidgets.Add(w);

        if (generated.Count > 0)
            SelectedWidget = generated[0];

        Log.Information("DesignerEditor: 批量生成 {Count} 个 {BlockType} 功能块", generated.Count, BatchBlockType);
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
