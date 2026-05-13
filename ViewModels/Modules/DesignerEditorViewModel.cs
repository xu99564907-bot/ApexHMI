#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
    private readonly PlcVariableImportService _plcVariableImport;
    private readonly EditStack _editStack = new();
    private bool _suppressEditRecording;

    /// <summary>从 PLC Device.Application.xml 加载的变量表（与 Shell.Tags 并列；非空时作为绑定首选）。</summary>
    public ApexHMI.Common.BulkObservableCollection<TagItem> PlcTags { get; } = new();

    /// <summary>当前 PLC 变量文件路径（成功导入后回显）。</summary>
    [ObservableProperty]
    private string _plcVariableFilePath = string.Empty;

    /// <summary>导入状态提示。</summary>
    [ObservableProperty]
    private string _plcVariableStatus = string.Empty;

    /// <summary>设计模式 widget 渲染所需的工厂。</summary>
    public IWidgetViewFactory WidgetViewFactory { get; }

    // ========== P1.2 网格 / 吸附 ==========
    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _snapToGrid = true;

    [ObservableProperty]
    private int _gridSize = 8;

    // ========== 画布缩放 ==========
    public double[] ZoomOptions { get; } = new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };

    [ObservableProperty]
    private double _canvasZoom = 1.0;

    [ObservableProperty]
    private string _fitButtonText = "适应窗口";

    [ObservableProperty]
    private bool _isFitToWindow;

    private double? _zoomBeforeFit;
    private bool _isApplyingFit;

    partial void OnCanvasZoomChanged(double value)
    {
        if (value < 0.1) { CanvasZoom = 0.1; return; }
        if (value > 4.0) { CanvasZoom = 4.0; return; }
        if (IsFitToWindow && !_isApplyingFit)
        {
            IsFitToWindow = false;
            _zoomBeforeFit = null;
            FitButtonText = "适应窗口";
        }
    }

    public void ApplyFitToWindow(double fitZoom)
    {
        _isApplyingFit = true;
        try
        {
            _zoomBeforeFit = CanvasZoom;
            CanvasZoom = fitZoom;
        }
        finally { _isApplyingFit = false; }
        IsFitToWindow = true;
        FitButtonText = $"恢复 {(_zoomBeforeFit ?? 1.0) * 100:0}%";
    }

    public void RestoreFromFit()
    {
        if (_zoomBeforeFit is null) return;
        _isApplyingFit = true;
        try { CanvasZoom = _zoomBeforeFit.Value; }
        finally { _isApplyingFit = false; }
        IsFitToWindow = false;
        _zoomBeforeFit = null;
        FitButtonText = "适应窗口";
    }

    /// <summary>对齐到网格（仅当 SnapToGrid=true 时生效）。</summary>
    public double SnapValue(double v)
    {
        if (!SnapToGrid || GridSize <= 0) return v;
        return System.Math.Round(v / GridSize) * GridSize;
    }

    [ObservableProperty]
    private string _mouseCoord = "0, 0";

    public void UpdateMouseCoord(double x, double y) => MouseCoord = $"{(int)x}, {(int)y}";

    // ========== 框选（Marquee）状态 ==========
    [ObservableProperty]
    private bool _isMarqueeActive;

    [ObservableProperty]
    private double _marqueeX;

    [ObservableProperty]
    private double _marqueeY;

    [ObservableProperty]
    private double _marqueeWidth;

    [ObservableProperty]
    private double _marqueeHeight;

    // ========== P1.3 多选 + 对齐 ==========

    /// <summary>多选集合（包含 SelectedWidget；空时表示无多选）。</summary>
    public ObservableCollection<WidgetInstance> SelectedWidgets { get; } = new();

    private void RefreshItemSelection()
    {
        foreach (var item in CurrentWidgetItems)
            item.IsSelected = SelectedWidgets.Contains(item.Model);
    }

    /// <summary>切换某 widget 的选中状态（Ctrl+点击）。</summary>
    public void ToggleWidgetSelection(WidgetInstance widget)
    {
        if (SelectedWidgets.Contains(widget))
        {
            SelectedWidgets.Remove(widget);
            if (ReferenceEquals(SelectedWidget, widget))
                SelectedWidget = SelectedWidgets.LastOrDefault();
        }
        else
        {
            SelectedWidgets.Add(widget);
            SelectedWidget = widget;
        }
        RefreshItemSelection();
        OnPropertyChanged(nameof(HasMultiSelection));
    }

    /// <summary>清空多选 + 设置单选。</summary>
    public void SelectSingleWidget(WidgetInstance? widget)
    {
        SelectedWidgets.Clear();
        if (widget is not null) SelectedWidgets.Add(widget);
        SelectedWidget = widget;
        RefreshItemSelection();
        OnPropertyChanged(nameof(HasMultiSelection));
    }

    /// <summary>框选范围内所有 widget。</summary>
    public void SelectInRectangle(double x1, double y1, double x2, double y2)
    {
        if (SelectedPage is null) return;
        var minX = System.Math.Min(x1, x2);
        var maxX = System.Math.Max(x1, x2);
        var minY = System.Math.Min(y1, y2);
        var maxY = System.Math.Max(y1, y2);

        SelectedWidgets.Clear();
        foreach (var w in SelectedPage.Widgets)
        {
            // widget 中心点落在矩形内
            var cx = w.X + w.Width / 2;
            var cy = w.Y + w.Height / 2;
            if (cx >= minX && cx <= maxX && cy >= minY && cy <= maxY)
                SelectedWidgets.Add(w);
        }
        SelectedWidget = SelectedWidgets.FirstOrDefault();
        RefreshItemSelection();
        OnPropertyChanged(nameof(HasMultiSelection));
    }

    public bool HasMultiSelection => SelectedWidgets.Count > 1;

    [RelayCommand]
    private void AlignLeft()
    {
        if (SelectedWidgets.Count < 2) return;
        var minX = SelectedWidgets.Min(w => w.X);
        foreach (var w in SelectedWidgets) _widgetEditor.MoveWidget(w, minX, w.Y);
        MarkPageEdited();
    }
    [RelayCommand]
    private void AlignRight()
    {
        if (SelectedWidgets.Count < 2) return;
        var maxR = SelectedWidgets.Max(w => w.X + w.Width);
        foreach (var w in SelectedWidgets) _widgetEditor.MoveWidget(w, maxR - w.Width, w.Y);
        MarkPageEdited();
    }
    [RelayCommand]
    private void AlignTop()
    {
        if (SelectedWidgets.Count < 2) return;
        var minY = SelectedWidgets.Min(w => w.Y);
        foreach (var w in SelectedWidgets) _widgetEditor.MoveWidget(w, w.X, minY);
        MarkPageEdited();
    }
    [RelayCommand]
    private void AlignBottom()
    {
        if (SelectedWidgets.Count < 2) return;
        var maxB = SelectedWidgets.Max(w => w.Y + w.Height);
        foreach (var w in SelectedWidgets) _widgetEditor.MoveWidget(w, w.X, maxB - w.Height);
        MarkPageEdited();
    }
    [RelayCommand]
    private void DistributeHorizontal()
    {
        if (SelectedWidgets.Count < 3) return;
        var sorted = SelectedWidgets.OrderBy(w => w.X).ToList();
        var first = sorted.First(); var last = sorted.Last();
        var span = last.X - first.X;
        var step = span / (sorted.Count - 1);
        for (int i = 1; i < sorted.Count - 1; i++)
            _widgetEditor.MoveWidget(sorted[i], first.X + step * i, sorted[i].Y);
        MarkPageEdited();
    }
    [RelayCommand]
    private void DistributeVertical()
    {
        if (SelectedWidgets.Count < 3) return;
        var sorted = SelectedWidgets.OrderBy(w => w.Y).ToList();
        var first = sorted.First(); var last = sorted.Last();
        var span = last.Y - first.Y;
        var step = span / (sorted.Count - 1);
        for (int i = 1; i < sorted.Count - 1; i++)
            _widgetEditor.MoveWidget(sorted[i], sorted[i].X, first.Y + step * i);
        MarkPageEdited();
    }
    [RelayCommand]
    private void SameWidth()
    {
        if (SelectedWidgets.Count < 2 || SelectedWidget is null) return;
        var w0 = SelectedWidget.Width;
        foreach (var w in SelectedWidgets) _widgetEditor.ResizeWidget(w, w0, w.Height);
        MarkPageEdited();
    }
    [RelayCommand]
    private void SameHeight()
    {
        if (SelectedWidgets.Count < 2 || SelectedWidget is null) return;
        var h0 = SelectedWidget.Height;
        foreach (var w in SelectedWidgets) _widgetEditor.ResizeWidget(w, w.Width, h0);
        MarkPageEdited();
    }

    // ========== P1.4 复制 / 粘贴 ==========

    /// <summary>剪贴板：保存被复制 widget 的快照（深拷贝）。</summary>
    private readonly List<WidgetInstance> _clipboard = new();

    public bool HasClipboard => _clipboard.Count > 0;

    [RelayCommand]
    private void CopySelected()
    {
        if (SelectedWidgets.Count == 0) return;
        _clipboard.Clear();
        foreach (var w in SelectedWidgets)
            _clipboard.Add(CloneWidget(w));
        OnPropertyChanged(nameof(HasClipboard));
        Log.Information("DesignerEditor: 已复制 {Count} 个控件到剪贴板", _clipboard.Count);
    }

    [RelayCommand]
    private void PasteClipboard()
    {
        if (SelectedPage is null || _clipboard.Count == 0) return;
        const double offset = 16;
        SelectedWidgets.Clear();
        foreach (var src in _clipboard)
        {
            var clone = CloneWidget(src);
            clone.X = SnapValue(src.X + offset);
            clone.Y = SnapValue(src.Y + offset);
            SelectedPage.Widgets.Add(clone);
            CurrentWidgets.Add(clone);
            AddWidgetItem(clone);
            SelectedWidgets.Add(clone);
        }
        SelectedWidget = SelectedWidgets.FirstOrDefault();
        RefreshItemSelection();
        MarkPageEdited();
        OnPropertyChanged(nameof(HasMultiSelection));
        Log.Information("DesignerEditor: 已粘贴 {Count} 个控件", _clipboard.Count);
    }

    private static WidgetInstance CloneWidget(WidgetInstance src)
    {
        var clone = new WidgetInstance
        {
            Id = System.Guid.NewGuid().ToString("N"),
            TypeId = src.TypeId,
            X = src.X,
            Y = src.Y,
            Width = src.Width,
            Height = src.Height,
            ActionType = src.ActionType,
            ActionParam = src.ActionParam,
            Binding = src.Binding is null ? null : new BindingSpec
            {
                TagId = src.Binding.TagId,
                AccessMode = src.Binding.AccessMode,
                DataType = src.Binding.DataType
            }
        };
        foreach (var kv in src.Properties)
            clone.Properties[kv.Key] = kv.Value;
        return clone;
    }

    [RelayCommand]
    private void DeleteSelectedWidgets()
    {
        if (SelectedPage is null || SelectedWidgets.Count == 0) return;
        var toDelete = SelectedWidgets.ToList();
        foreach (var w in toDelete)
        {
            _widgetEditor.RemoveWidget(SelectedPage, w.Id);
            CurrentWidgets.Remove(w);
            RemoveWidgetItem(w);
        }
        SelectedWidgets.Clear();
        SelectedWidget = null;
        MarkPageEdited();
        OnPropertyChanged(nameof(HasMultiSelection));
    }

    /// <summary>设计模式数据上下文（无 OPC UA 推送，但暴露 Shell 供业务 widget 取真实数据）。</summary>
    public IWidgetDataContext DesignModeContext { get; }

    /// <summary>工具箱固定分组（基本对象 / 元素 / 控件），不随工程变化。</summary>
    private static readonly IReadOnlyList<ToolboxGroup> _staticToolboxGroups = new[]
    {
        new ToolboxGroup("基本对象", new[]
        {
            new ToolboxItem("text",         "文本",       "FormatText"),
            new ToolboxItem("rectangle",    "矩形",       "RectangleOutline"),
            new ToolboxItem("ellipse",      "椭圆",       "EllipseOutline"),
            new ToolboxItem("line",         "直线",       "VectorLine"),
            new ToolboxItem("polyline",     "折线",       "VectorPolyline"),
            new ToolboxItem("polygon",      "多边形",     "VectorPolygon"),
            new ToolboxItem("graphic-view", "图形视图",   "ImageOutline"),
            new ToolboxItem("io-numeric",   "数字 I/O",   "Numeric"),
            new ToolboxItem("io-symbolic",  "符号 I/O",   "FormatListBulleted"),
            new ToolboxItem("io-graphic",   "图形 I/O",   "ImageMultiple"),
            new ToolboxItem("datetime",     "日期时间",   "Clock"),
        }),
        new ToolboxGroup("元素", new[]
        {
            new ToolboxItem("button",        "按钮",       "GestureTap"),
            new ToolboxItem("switch",        "开关",       "ToggleSwitch"),
            new ToolboxItem("round-button",  "圆形按钮",   "CircleSlice8"),
            new ToolboxItem("bar",           "棒图",       "ChartBar"),
            new ToolboxItem("gauge",         "量规",       "GaugeFull"),
            new ToolboxItem("slider",        "滑块",       "SlideVariant"),
            new ToolboxItem("scrollbar",     "滚动条",     "ArrowExpandVertical"),
            new ToolboxItem("clock",         "时钟",       "ClockOutline"),
            new ToolboxItem("combobox",      "组合框",     "FormDropdown"),
            new ToolboxItem("listbox",       "列表框",     "ViewList"),
            new ToolboxItem("checkbox",      "复选框",     "CheckboxOutline"),
            new ToolboxItem("optiongroup",   "单选",       "RadioboxMarked"),
        }),
        // P5 高级控件
        new ToolboxGroup("控件", new[]
        {
            new ToolboxItem("trend-view",     "趋势视图",   "ChartLine"),
            new ToolboxItem("alarm-view",     "报警视图",   "BellOutline"),
            new ToolboxItem("table-view",     "表格视图",   "Table"),
            new ToolboxItem("screen-window",  "画面窗口",   "WindowMaximize"),
            // P8A 配方视图
            new ToolboxItem("recipe-view",    "配方视图",   "BookCogOutline"),
            // P8B 用户视图
            new ToolboxItem("user-view",      "用户视图",   "AccountMultipleOutline"),
            // P8C 系统诊断
            new ToolboxItem("diagnostic-view","系统诊断",   "Stethoscope"),
            // P8D 报警指示器
            new ToolboxItem("alarm-indicator","报警指示器", "AlertCircle"),
            // P8E 状态强制（调试）
            new ToolboxItem("status-force",   "状态强制",   "Pencil"),
        }),
    };

    /// <summary>P7D: 工具箱分组定义，动态拼接基础分组 + "我的 Faceplate"。</summary>
    public IReadOnlyList<ToolboxGroup> ToolboxGroups
    {
        get
        {
            var groups = new List<ToolboxGroup>(_staticToolboxGroups);
            var fpLib = Document?.Faceplates;
            if (fpLib is not null && fpLib.Faceplates.Count > 0)
            {
                var items = new List<ToolboxItem>();
                foreach (var fp in fpLib.Faceplates)
                {
                    var name = fp.IsBuiltIn ? $"[内置] {fp.Name}" : fp.Name;
                    items.Add(new ToolboxItem($"faceplate:{fp.Id}", name, fp.IconKind));
                }
                groups.Add(new ToolboxGroup("我的 Faceplate", items));
            }
            return groups;
        }
    }

    /// <summary>P7D: Document 切换或 Faceplate 库变化时调用，刷新工具箱。</summary>
    public void RefreshFaceplateToolbox() => OnPropertyChanged(nameof(ToolboxGroups));

    public sealed class ToolboxGroup
    {
        public ToolboxGroup(string title, IReadOnlyList<ToolboxItem> items) { Title = title; Items = items; }
        public string Title { get; }
        public IReadOnlyList<ToolboxItem> Items { get; }
    }

    /// <summary>工具箱单项：TypeId(添加用) + 中文显示名 + MaterialDesign 图标 Kind。</summary>
    public sealed class ToolboxItem
    {
        public ToolboxItem(string typeId, string displayName, string iconKind)
        {
            TypeId = typeId; DisplayName = displayName; IconKind = iconKind;
        }
        public string TypeId { get; }
        public string DisplayName { get; }
        public string IconKind { get; }
    }

    public DesignerEditorViewModel(
        MainViewModel shell,
        IProjectEditorService projectEditor,
        IWidgetEditorService widgetEditor,
        RuntimeProjectService runtimeProjectService,
        IWidgetViewFactory widgetViewFactory,
        PlcVariableImportService plcVariableImport)
        : base(shell, "画布设计")
    {
        _projectEditor = projectEditor;
        _widgetEditor = widgetEditor;
        _runtimeProjectService = runtimeProjectService;
        _plcVariableImport = plcVariableImport;
        WidgetViewFactory = widgetViewFactory;
        DesignModeContext = new DesignModeWidgetDataContext(shell);

        // G7 未保存提示：任何编辑都标记 dirty，Save 后清除
        _editStack.EditApplied += (_, _) =>
        {
            IsDirty = true;
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };
        shell.RegisterDirtySource(() => IsDirty);

        _autoSaveTimer.Tick += (_, _) =>
        {
            _autoSaveTimer.Stop();
            if (IsDirty) SaveCommand.Execute(null);
        };

        InitDocument();
        // 启动时自动尝试从当前 PLC 工程目录加载一次变量表（失败静默）
        // 用 Dispatcher.BeginInvoke + Background 优先级，让窗口先显示出来再加载
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
            new Action(TryAutoLoadPlcVariables),
            System.Windows.Threading.DispatcherPriority.Background);
        // PLC 程序保存目录变更时（Shell.GitPullVm.GitTargetFolder）自动重新加载
        Shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.GitTargetFolder)) TryAutoLoadPlcVariables();
        };
    }

    /// <summary>启动 / GitTargetFolder 变更时静默加载；找不到文件不弹错。</summary>
    public void TryAutoLoadPlcVariables()
    {
        // 优先用上次手动导入保存的设置（路径 + OP 选择）
        var saved = _plcVariableImport.LoadSettings();
        if (saved != null && !string.IsNullOrWhiteSpace(saved.FilePath) && System.IO.File.Exists(saved.FilePath))
        {
            ISet<string>? ops = saved.SelectedOps is { Count: > 0 }
                ? new HashSet<string>(saved.SelectedOps, StringComparer.Ordinal)
                : null;
            LoadPlcVariablesSilent(saved.FilePath, ops);
            return;
        }
        // 回退：从 PLC 程序保存目录自动查找
        var path = _plcVariableImport.ResolveDefaultPath(Shell.GitTargetFolder);
        if (string.IsNullOrEmpty(path)) return;
        LoadPlcVariablesSilent(path!, null);
    }

    private void LoadPlcVariablesSilent(string path, ISet<string>? selectedOps)
    {
        PlcVariableStatus = "正在加载…";
        var ui = System.Threading.SynchronizationContext.Current;
        Task.Run(() =>
        {
            try
            {
                return _plcVariableImport.LoadFromFile(path, selectedOps);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DesignerEditor: 静默加载 PLC 变量失败 path={Path}", path);
                return (IReadOnlyList<TagItem>)Array.Empty<TagItem>();
            }
        }).ContinueWith(t =>
        {
            var tags = t.Result;
            ApplyLoadedTags(path, tags, selectedOps, "静默");
        }, ui is null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ApplyLoadedTags(string path, IReadOnlyList<TagItem> tags, ISet<string>? selectedOps, string mode)
    {
        // 批量替换：一次 Reset 事件，避免 N 次 Add 的级联通知
        PlcTags.ReplaceAll(tags);
        PlcVariableFilePath = path;
        PlcVariableStatus = tags.Count > 0
            ? $"已加载 {tags.Count} 个变量"
            : "未解析到变量";
        OnPropertyChanged(nameof(AvailableTags));
        Log.Information("DesignerEditor: {Mode}加载 PLC 变量 path={Path} count={Count} ops={Ops}",
            mode, path, tags.Count, selectedOps is null ? "ALL" : string.Join(",", selectedOps));
    }

    private void LoadPlcVariablesFrom(string path, bool silent = false)
    {
        ISet<string>? selectedOps = null;
        if (!silent)
        {
            var topGroups = _plcVariableImport.ScanTopGroups(path);
            if (topGroups.Count > 0)
            {
                var dlg = new ApexHMI.Views.Dialogs.PlcVariableOpFilterDialog(topGroups)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };
                if (dlg.ShowDialog() != true) return;
                selectedOps = dlg.SelectedOps;
                if (selectedOps is null || selectedOps.Count == 0)
                {
                    PlcVariableStatus = "未选择任何工位，已取消导入";
                    return;
                }
            }
        }

        PlcVariableStatus = "正在加载…";
        var capturedOps = selectedOps;
        var ui = System.Threading.SynchronizationContext.Current;
        Task.Run(() => _plcVariableImport.LoadFromFile(path, capturedOps))
            .ContinueWith(t =>
            {
                var tags = t.Result;
                ApplyLoadedTags(path, tags, capturedOps, "手动");
                if (tags.Count > 0) _plcVariableImport.SaveSettings(path, capturedOps);
            }, ui is null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>"重新导入 PLC 变量"按钮：从 GitTargetFolder 找 Device.Application.xml 并解析。</summary>
    [RelayCommand]
    private void ReloadPlcVariables()
    {
        var path = _plcVariableImport.ResolveDefaultPath(Shell.GitTargetFolder);
        if (string.IsNullOrEmpty(path))
        {
            PlcVariableStatus = "未找到 Device.Application.xml（请在「PLC 程序保存目录」中设置）";
            return;
        }
        LoadPlcVariablesFrom(path!, silent: false);
    }

    /// <summary>"浏览 XML 文件"命令：手动选择一个 Device.Application.xml。</summary>
    [RelayCommand]
    private void BrowsePlcVariableFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PLC 变量表 (*.Device.Application.xml)|*Device.Application.xml|XML 文件 (*.xml)|*.xml",
            Title = "选择 PLC 变量表 XML",
        };
        var initial = Shell.GitTargetFolder;
        if (!string.IsNullOrWhiteSpace(initial) && System.IO.Directory.Exists(initial))
            dlg.InitialDirectory = initial;
        if (dlg.ShowDialog() == true) LoadPlcVariablesFrom(dlg.FileName, silent: false);
    }

    /// <summary>当前画布是否有未保存改动（G7 切页确认依据）。</summary>
    [ObservableProperty]
    private bool _isDirty;

    private readonly DispatcherTimer _autoSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };

    partial void OnIsDirtyChanged(bool value)
    {
        Shell.NotifyDirtyStateChanged();
        if (value)
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }
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

    /// <summary>当前选中控件的属性列表，绑定到属性编辑器 ItemsControl（旧 fallback 通道）。</summary>
    public ObservableCollection<WidgetPropertyItem> CurrentWidgetProperties { get; } = new();

    /// <summary>P7.5B: Schema-driven 属性编辑器组（按 Category 分组）。
    /// 若 schema 找不到，则该集合为空，UI 回退到旧 <see cref="CurrentWidgetProperties"/>。</summary>
    public ObservableCollection<PropertyCategoryGroup> GroupedPropertyEditors { get; } = new();

    /// <summary>P7.5B: 当前选中控件是否有 Schema（控制 schema 面板 vs fallback 显隐）。</summary>
    public bool HasSchemaForSelectedWidget => GroupedPropertyEditors.Count > 0;

    /// <summary>P7.5B: HasSchemaForSelectedWidget 的反值（XAML 绑定用，BooleanToVisibilityConverter 不支持 Invert 参数）。</summary>
    public bool HasNoSchemaForSelectedWidget => !HasSchemaForSelectedWidget;

    /// <summary>已订阅 PropertyChanged 的 widget → 处理器映射，便于解订避免内存泄漏。</summary>
    private readonly Dictionary<WidgetInstance, PropertyChangedEventHandler> _widgetHandlers = new();

    private FrameworkElement BuildWidgetView(WidgetInstance widget)
    {
        var view = WidgetViewFactory.Create(widget, DesignModeContext);
        // 设计模式：禁止 widget 自身响应鼠标，所有点击穿透到上层选中层 Border
        view.IsHitTestVisible = false;
        return view;
    }

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

    /// <summary>
    /// 可用 Tag 列表：PLC 变量表（Device.Application.xml）非空时优先使用，
    /// 否则回退到 Shell.Tags（OPC UA 在线变量 + 演示数据）。
    /// </summary>
    public IEnumerable<TagItem> AvailableTags => PlcTags.Count > 0
        ? (IEnumerable<TagItem>)PlcTags
        : Shell.Tags.Where(t => !string.Equals(t.Group, "Imported", StringComparison.Ordinal));

    /// <summary>「页面跳转」可选目标：固定段名 + 工程内用户页面。</summary>
    public IEnumerable<NavigateTarget> NavigateTargets
    {
        get
        {
            var fixedTargets = new (string Title, string Key)[]
            {
                ("主界面",          "主界面"),
                ("监控",            "监控"),
                ("手动操作",        "手动操作"),
                ("参数设定",        "参数设定"),
                ("报警画面",        "报警画面"),
                ("配方管理",        "配方管理"),
                ("登录",            "登录"),
                ("画布设计",        "画布设计"),
                ("运行页面",        "运行页面"),
            };
            foreach (var (t, k) in fixedTargets)
                yield return new NavigateTarget { DisplayName = t, RouteKey = k, Group = "固定页" };
            if (Document is not null)
                foreach (var p in Document.Pages)
                    yield return new NavigateTarget { DisplayName = p.Title, RouteKey = p.RouteKey, Group = "用户页" };
        }
    }

    public sealed class NavigateTarget
    {
        public string DisplayName { get; init; } = string.Empty;
        public string RouteKey { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
    }

    /// <summary>
    /// 当前选中 widget 的可用设备名列表（P0 清洗后仅基础控件，永远为空）。
    /// 保留属性以兼容现有 XAML 绑定。
    /// </summary>
    public IEnumerable<string> AvailableDeviceNames => Enumerable.Empty<string>();

    // ========== 选中页切换 ==========

    private PageDefinition? _subscribedPage;

    private void OnSelectedPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 父段/标题/导航排序/顶栏可见性变更 → 立即刷新左侧导航与顶栏，
        // 这样把已绑定的页面重绑到别的父画面后无需保存即可看到效果。
        if (e.PropertyName is nameof(PageDefinition.ParentRouteKey)
                          or nameof(PageDefinition.Title)
                          or nameof(PageDefinition.NavOrder)
                          or nameof(PageDefinition.ShowInTopNav))
        {
            (Shell as MainWindowViewModel)?.RefreshTopNavUserPages();
        }
    }

    partial void OnSelectedPageChanged(PageDefinition? value)
    {
        if (_subscribedPage is not null)
            _subscribedPage.PropertyChanged -= OnSelectedPagePropertyChanged;
        _subscribedPage = value;
        if (value is not null)
            value.PropertyChanged += OnSelectedPagePropertyChanged;

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
        OnPropertyChanged(nameof(WriteAddress));
        OnPropertyChanged(nameof(WriteValue));
        OnPropertyChanged(nameof(AvailableDeviceNames));
        RefreshAnimationsList();
        RefreshCurrentEventSteps();
        NotifyAnimationSelectionChanged();
        // P7D: 刷新 Faceplate 接口属性面板
        RefreshFaceplateInterfaceArgs();
        // P7.5B: 刷新 Schema-driven 属性编辑器
        RefreshGroupedPropertyEditors();
    }

    /// <summary>P7.5B: 根据 SelectedWidget 的 TypeId 重建 schema-driven 属性面板分组。
    /// <para>找不到 schema 时清空集合，UI 回退到旧 fallback。</para></summary>
    private void RefreshGroupedPropertyEditors()
    {
        GroupedPropertyEditors.Clear();
        if (SelectedWidget is null)
        {
            OnPropertyChanged(nameof(HasSchemaForSelectedWidget));
        OnPropertyChanged(nameof(HasNoSchemaForSelectedWidget));
            return;
        }

        var schema = ApexHMI.Services.RuntimeUi.WidgetSchemaCatalog.Lookup(SelectedWidget.TypeId);
        if (schema is null)
        {
            OnPropertyChanged(nameof(HasSchemaForSelectedWidget));
        OnPropertyChanged(nameof(HasNoSchemaForSelectedWidget));
            return;
        }

        // 按 Category 分组（保持原顺序）
        var groupOrder = new List<string>();
        var groups = new Dictionary<string, List<PropertyEditorVM>>();
        foreach (var desc in schema.Properties)
        {
            var value = SelectedWidget.Properties.TryGetValue(desc.Key, out var v) ? v : desc.DefaultValue;
            var editor = new PropertyEditorVM(desc, value ?? string.Empty, OnSchemaEditorChanged);
            if (!groups.TryGetValue(desc.Category, out var list))
            {
                list = new List<PropertyEditorVM>();
                groups[desc.Category] = list;
                groupOrder.Add(desc.Category);
            }
            list.Add(editor);
        }

        foreach (var cat in groupOrder)
            GroupedPropertyEditors.Add(new PropertyCategoryGroup(cat, groups[cat]));

        OnPropertyChanged(nameof(HasSchemaForSelectedWidget));
        OnPropertyChanged(nameof(HasNoSchemaForSelectedWidget));
    }

    /// <summary>P7.5B: schema 编辑器 Value 变化 → 写入 widget。</summary>
    private void OnSchemaEditorChanged(string key, string? value)
    {
        UpdateWidgetProperty(key, value);
    }

    private void OnWidgetPropertyItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetPropertyItem.Value) && sender is WidgetPropertyItem item)
        {
            UpdateWidgetProperty(item.Key, item.Value);
        }
    }

    // ========== 页面命令 ==========

    /// <summary>新建页面时使用的标题（默认自动）。</summary>
    [ObservableProperty]
    private string _newPageTitle = string.Empty;

    /// <summary>新建页面时挂载的父页面 RouteKey（null = 顶层页）。</summary>
    [ObservableProperty]
    private string? _newPageParentRouteKey;

    /// <summary>
    /// 软件内置的固定父页面（顶级导航段）。新建画布页面时可挂载到这些固定画面之下，
    /// 而不是其他画布页。值即 CurrentSection 字符串。
    /// </summary>
    public IReadOnlyList<string> BuiltInParentSections { get; } = new[]
    {
        "主界面", "监控", "手动操作", "参数设定", "报警画面", "登录", "设计器"
    };

    [RelayCommand]
    private void AddPage()
    {
        var title = string.IsNullOrWhiteSpace(NewPageTitle)
            ? $"页面{Document.Pages.Count + 1}"
            : NewPageTitle.Trim();
        var page = _projectEditor.AddPage(Document, title);
        page.ParentRouteKey = string.IsNullOrWhiteSpace(NewPageParentRouteKey) ? null : NewPageParentRouteKey;
        NewPageTitle = string.Empty;
        SelectedPage = page;
        Log.Information("DesignerEditor: 已添加页面 title={Title} routeKey={RouteKey} parent={Parent}",
            title, page.RouteKey, page.ParentRouteKey ?? "(顶层)");
    }

    [RelayCommand]
    private void RemovePage()
    {
        if (SelectedPage is null)
        {
            Shell.ShowPopup("删除页面", "请先在下拉列表中选中要删除的页面", "Warning");
            return;
        }

        if (Document.Pages.Count <= 1)
        {
            Shell.ShowPopup("删除页面", "工程至少需要保留一个页面，当前不能删除最后一个", "Warning");
            return;
        }

        var pageTitle = SelectedPage.Title;
        if (!Shell.RequestConfirmation("删除页面", $"确定删除页面【{pageTitle}】？"))
        {
            return;
        }

        if (!_projectEditor.RemovePage(Document, SelectedPage.Id, out var error))
        {
            Log.Warning("DesignerEditor: 删除页面被阻止 —— {Error}", error);
            Shell.ShowPopup("删除被阻止", error ?? $"页面【{pageTitle}】无法删除", "Warning");
            return;
        }

        var next = Document.Pages.FirstOrDefault();
        SelectedPage = next;
        if (next is null)
        {
            SelectedWidget = null;
            CurrentWidgets.Clear();
        }
        Shell.AddLog("设计器", $"已删除页面：{pageTitle}", "Info");
    }

    // ========== 控件命令 ==========

    [RelayCommand]
    private void AddWidget(string? typeId)
    {
        if (SelectedPage is null || string.IsNullOrWhiteSpace(typeId)) return;

        var widget = _widgetEditor.AddWidget(SelectedPage, typeId, 40, 40);
        ApplyFaceplateDefaultsIfNeeded(widget);
        CurrentWidgets.Add(widget);
        AddWidgetItem(widget);
        SelectedWidget = widget;
        MarkPageEdited();

        _editStack.Execute(new AddWidgetEdit(_widgetEditor, SelectedPage, widget));
        OnPropertyChanged(nameof(CanUndo));
        Log.Information("DesignerEditor: 已添加控件 typeId={TypeId} id={Id}", typeId, widget.Id);
    }

    /// <summary>P7D: 若新建的控件是 faceplate:<id>，应用 Faceplate 默认尺寸 + 接口属性默认值 + 版本号。</summary>
    private void ApplyFaceplateDefaultsIfNeeded(WidgetInstance widget)
    {
        if (widget.TypeId is not { Length: > 10 } tid) return;
        if (!tid.StartsWith("faceplate:", StringComparison.OrdinalIgnoreCase)) return;
        var fpId = tid.Substring("faceplate:".Length);
        var fp = Document?.Faceplates?.Faceplates.FirstOrDefault(f => string.Equals(f.Id, fpId, StringComparison.Ordinal));
        if (fp is null) return;
        widget.Width = fp.DefaultWidth;
        widget.Height = fp.DefaultHeight;
        foreach (var def in fp.InterfaceProperties)
        {
            if (!widget.Properties.ContainsKey(def.Key))
                widget.Properties[def.Key] = def.DefaultValue ?? string.Empty;
        }
        widget.FaceplateVersion = fp.Version;
        widget.NotifyPropertiesChanged();
    }

    /// <summary>变量拖放生成控件：在指定坐标添加 typeId 控件，并预填 variable 属性 = tagName。</summary>
    public WidgetInstance? AddWidgetWithVariable(string typeId, string tagName, double x, double y)
    {
        if (SelectedPage is null || string.IsNullOrWhiteSpace(typeId)) return null;
        var widget = _widgetEditor.AddWidget(SelectedPage, typeId, x, y);
        if (!string.IsNullOrWhiteSpace(tagName))
        {
            // 大多数 P3/P4 控件用 Properties["variable"] 作读地址；同时设置 Binding.TagId 兼容基础控件
            widget.Properties["variable"] = tagName;
            _widgetEditor.UpdateBinding(widget, new BindingSpec
            {
                TagId = tagName,
                AccessMode = BindingAccessMode.Subscribe,
                DataType = "String"
            });
            widget.NotifyPropertiesChanged();
        }
        CurrentWidgets.Add(widget);
        AddWidgetItem(widget);
        SelectedWidget = widget;
        MarkPageEdited();
        Log.Information("DesignerEditor: 变量拖放添加控件 typeId={TypeId} tag={Tag}", typeId, tagName);
        return widget;
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
        ApplyFaceplateDefaultsIfNeeded(widget);
        CurrentWidgets.Add(widget);
        AddWidgetItem(widget);
        SelectedWidget = widget;
        MarkPageEdited();
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
        MarkPageEdited();
        OnPropertyChanged(nameof(CanUndo));
    }

    [RelayCommand]
    private void SelectWidget(WidgetInstance? widget)
    {
        SelectedWidget = widget;
    }

    // ========== B-06: 属性修改 ==========

    /// <summary>标记当前页面为"用户已编辑"，防止 IO 重新导入时被自动覆盖。</summary>
    private void MarkPageEdited()
    {
        if (SelectedPage is not null && !SelectedPage.IsUserEdited)
            SelectedPage.IsUserEdited = true;
    }

    /// <summary>修改当前选中控件的属性键值。</summary>
    public void UpdateWidgetProperty(string key, string? value)
    {
        if (SelectedWidget is null) return;
        MarkPageEdited();

        var oldValue = SelectedWidget.Properties.TryGetValue(key, out var existing) ? existing : null;
        _widgetEditor.UpdateProperty(SelectedWidget, key, value);

        // P7.5B: 同步 schema-driven 编辑器值（防止外部修改时 UI 不刷新）
        foreach (var grp in GroupedPropertyEditors)
        {
            foreach (var ed in grp.Items)
            {
                if (string.Equals(ed.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    ed.SetValueSilent(value ?? string.Empty);
                }
            }
        }

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

    /// <summary>移动当前选中控件（拖拽实时调用，不记录撤销）；自动吸附到网格。</summary>
    public void MoveSelectedWidget(double x, double y)
    {
        if (SelectedWidget is null) return;
        _widgetEditor.MoveWidget(SelectedWidget, SnapValue(x), SnapValue(y));
        MarkPageEdited();
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

    /// <summary>拖角实时改变 位置+尺寸（不记录撤销）；自动吸附到网格。</summary>
    public void MoveAndResizeSelectedWidget(double x, double y, double width, double height)
    {
        if (SelectedWidget is null) return;
        _widgetEditor.MoveWidget(SelectedWidget, SnapValue(x), SnapValue(y));
        _widgetEditor.ResizeWidget(SelectedWidget, SnapValue(width), SnapValue(height));
        MarkPageEdited();
    }

    /// <summary>拖角缩放结束后写入撤销栈。</summary>
    public void CommitResize(double oldX, double oldY, double oldW, double oldH,
                             double newX, double newY, double newW, double newH)
    {
        if (SelectedWidget is null) return;
        _editStack.Execute(new ResizeWidgetEdit(
            _widgetEditor, SelectedWidget,
            oldX, oldY, oldW, oldH,
            newX, newY, newW, newH));
        OnPropertyChanged(nameof(CanUndo));
    }

    // ========== B-07: 绑定修改 ==========

    /// <summary>当前选中控件的绑定 Tag 名称（用于双向绑定到 ComboBox）。</summary>
    public string? CurrentBindingTagId
    {
        get => SelectedWidget?.Binding?.TagId;
        set
        {
            UpdateWidgetBinding(value);
            OnPropertyChanged();
        }
    }

    /// <summary>写入动作（write-bool/int/float）的地址部分。底层存到 <c>ActionParam</c>，格式 <c>addr|value</c>。</summary>
    public string WriteAddress
    {
        get
        {
            var s = SelectedWidget?.ActionParam ?? string.Empty;
            var i = s.IndexOf('|');
            return i < 0 ? s : s.Substring(0, i);
        }
        set
        {
            if (SelectedWidget is null) return;
            var v = WriteValue;
            SelectedWidget.ActionParam = string.IsNullOrEmpty(v) ? (value ?? string.Empty) : $"{value}|{v}";
            MarkPageEdited();
            OnPropertyChanged();
        }
    }

    /// <summary>写入动作（write-bool/int/float）的值部分（True/False/整数/浮点）。</summary>
    public string WriteValue
    {
        get
        {
            var s = SelectedWidget?.ActionParam ?? string.Empty;
            var i = s.IndexOf('|');
            return i < 0 ? string.Empty : s.Substring(i + 1);
        }
        set
        {
            if (SelectedWidget is null) return;
            var a = WriteAddress;
            SelectedWidget.ActionParam = string.IsNullOrEmpty(value) ? a : $"{a}|{value}";
            MarkPageEdited();
            OnPropertyChanged();
        }
    }

    /// <summary>打开 Tag 浏览器对话框，选中后写入当前控件绑定。</summary>
    // ========== P2.5 状态动画 编辑命令 ==========

    public ObservableCollection<WidgetAnimation> CurrentAnimations { get; } = new();

    private void RefreshAnimationsList()
    {
        CurrentAnimations.Clear();
        if (SelectedWidget is null) return;
        foreach (var a in SelectedWidget.Animations) CurrentAnimations.Add(a);
    }

    [RelayCommand]
    private void AddAnimation()
    {
        if (SelectedWidget is null) return;
        var a = new WidgetAnimation { TagId = "", Op = "true", TargetProperty = "background", TargetValue = "#EF4444" };
        SelectedWidget.Animations.Add(a);
        CurrentAnimations.Add(a);
        MarkPageEdited();
        // 重建 view 让动画注册生效
        var item = CurrentWidgetItems.FirstOrDefault(i => ReferenceEquals(i.Model, SelectedWidget));
        if (item is not null) item.View = BuildWidgetView(SelectedWidget);
    }

    [RelayCommand]
    private void RemoveAnimation(WidgetAnimation? anim)
    {
        if (anim is null || SelectedWidget is null) return;
        SelectedWidget.Animations.Remove(anim);
        CurrentAnimations.Remove(anim);
        MarkPageEdited();
        var item = CurrentWidgetItems.FirstOrDefault(i => ReferenceEquals(i.Model, SelectedWidget));
        if (item is not null) item.View = BuildWidgetView(SelectedWidget);
    }

    [RelayCommand]
    private void OpenTagBrowser()
    {
        if (SelectedWidget is null) return;
        var dlg = new ApexHMI.Views.Dialogs.TagBrowserDialog(AvailableTags)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.SelectedTagName))
        {
            CurrentBindingTagId = dlg.SelectedTagName;
        }
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

    // ========== P1 事件 Tab ==========

    /// <summary>左侧 6 个事件入口（按钮控件实际只走 click/press/release，其余事件保留扩展）。</summary>
    public ObservableCollection<EventEntryViewModel> AvailableEvents { get; } = new()
    {
        new() { EventId = "click",        DisplayName = "单击"      },
        new() { EventId = "press",        DisplayName = "按下"      },
        new() { EventId = "release",      DisplayName = "释放"      },
        new() { EventId = "activate",     DisplayName = "激活"      },
        new() { EventId = "deactivate",   DisplayName = "取消激活"  },
        new() { EventId = "valueChanged", DisplayName = "数值更改"  },
    };

    /// <summary>当前选中的事件名。</summary>
    [ObservableProperty]
    private string _selectedEventName = "click";

    /// <summary>当前事件下的动作步骤集合（绑定到事件 Tab 右栏 ListBox）。</summary>
    public ObservableCollection<ActionStepViewModel> CurrentEventSteps { get; } = new();

    partial void OnSelectedEventNameChanged(string value) => RefreshCurrentEventSteps();

    /// <summary>当前选中事件入口（用于左列高亮）。</summary>
    public EventEntryViewModel? CurrentEventEntry =>
        AvailableEvents.FirstOrDefault(e => e.EventId == SelectedEventName);

    /// <summary>把当前选中 widget+event 下的 ActionStep 列表刷到 VM 集合，并更新徽章计数。</summary>
    private void RefreshCurrentEventSteps()
    {
        CurrentEventSteps.Clear();
        var w = SelectedWidget;
        if (w is not null && w.Events.TryGetValue(SelectedEventName, out var steps))
        {
            foreach (var s in steps)
                CurrentEventSteps.Add(new ActionStepViewModel(s));
        }
        RefreshEventEntryBadges();
        OnPropertyChanged(nameof(CurrentEventEntry));
    }

    /// <summary>刷新左列每个事件入口的动作数徽章 + 选中态。</summary>
    private void RefreshEventEntryBadges()
    {
        var w = SelectedWidget;
        foreach (var ev in AvailableEvents)
        {
            ev.StepCount = (w is not null && w.Events.TryGetValue(ev.EventId, out var lst))
                ? lst.Count : 0;
            ev.IsSelected = ev.EventId == SelectedEventName;
        }
    }

    /// <summary>切换当前选中事件。</summary>
    [RelayCommand]
    private void SelectEvent(string? eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        SelectedEventName = eventId!;
    }

    /// <summary>新建动作：弹系统函数选择对话框，将选中函数加入当前事件链尾。</summary>
    [RelayCommand]
    private void AddAction()
    {
        if (SelectedWidget is null) return;
        var dlg = new ApexHMI.Views.Dialogs.SystemFunctionPickerDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.SelectedFunctionId)) return;

        EnsureEventList(SelectedWidget, SelectedEventName)
            .Add(new ActionStep { FunctionId = dlg.SelectedFunctionId! });

        MarkPageEdited();
        RefreshCurrentEventSteps();
        SyncLegacyFromEvents();
    }

    /// <summary>从当前事件链中删除指定动作步骤。</summary>
    [RelayCommand]
    private void DeleteAction(ActionStepViewModel? stepVm)
    {
        if (stepVm is null || SelectedWidget is null) return;
        if (!SelectedWidget.Events.TryGetValue(SelectedEventName, out var steps)) return;
        steps.Remove(stepVm.Model);
        if (steps.Count == 0) SelectedWidget.Events.Remove(SelectedEventName);
        MarkPageEdited();
        RefreshCurrentEventSteps();
        SyncLegacyFromEvents();
    }

    /// <summary>当前事件链中上移指定动作（同时上移 model 与 VM 集合）。</summary>
    [RelayCommand]
    private void MoveActionUp(ActionStepViewModel? stepVm)
    {
        if (stepVm is null || SelectedWidget is null) return;
        if (!SelectedWidget.Events.TryGetValue(SelectedEventName, out var steps)) return;
        var idx = steps.IndexOf(stepVm.Model);
        if (idx <= 0) return;
        (steps[idx - 1], steps[idx]) = (steps[idx], steps[idx - 1]);
        MarkPageEdited();
        RefreshCurrentEventSteps();
        SyncLegacyFromEvents();
    }

    /// <summary>当前事件链中下移指定动作。</summary>
    [RelayCommand]
    private void MoveActionDown(ActionStepViewModel? stepVm)
    {
        if (stepVm is null || SelectedWidget is null) return;
        if (!SelectedWidget.Events.TryGetValue(SelectedEventName, out var steps)) return;
        var idx = steps.IndexOf(stepVm.Model);
        if (idx < 0 || idx >= steps.Count - 1) return;
        (steps[idx + 1], steps[idx]) = (steps[idx], steps[idx + 1]);
        MarkPageEdited();
        RefreshCurrentEventSteps();
        SyncLegacyFromEvents();
    }

    private static List<ActionStep> EnsureEventList(WidgetInstance w, string eventName)
    {
        if (!w.Events.TryGetValue(eventName, out var steps))
            w.Events[eventName] = steps = new List<ActionStep>();
        return steps;
    }

    /// <summary>把 Events["click"] 的第一个 step 反向同步回旧 ActionType/ActionParam，
    /// 让旧 Expander UI 与新事件 Tab 在 click 单动作场景下数据保持一致。
    /// 多动作场景下旧 Expander 只反映第一个动作（提示用户走新 Tab）。</summary>
    private void SyncLegacyFromEvents()
    {
        var w = SelectedWidget;
        if (w is null) return;
        if (w.Events.TryGetValue("click", out var steps) && steps.Count > 0)
        {
            var first = steps[0];
            w.ActionType = first.FunctionId;
            w.ActionParam = first.Args.TryGetValue("address", out var a) ? a
                : first.Args.TryGetValue("routeKey", out var r) ? r
                : first.Args.TryGetValue("text", out var t) ? t
                : first.Args.TryGetValue("value", out var v) ? v
                : string.Empty;
            // write-bool/int/float 需要 addr|value 合并
            if (first.FunctionId is "write-bool" or "write-int" or "write-float")
            {
                first.Args.TryGetValue("address", out var addr);
                first.Args.TryGetValue("value", out var val);
                w.ActionParam = string.IsNullOrEmpty(val) ? (addr ?? "") : $"{addr}|{val}";
            }
        }
        else
        {
            // click 链为空 → 清空旧字段
            w.ActionType = null;
            w.ActionParam = null;
        }
        OnPropertyChanged(nameof(WriteAddress));
        OnPropertyChanged(nameof(WriteValue));
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

    // ========== 保存 / 发布 ==========

    [RelayCommand]
    private void Save()
    {
        try
        {
            _runtimeProjectService.Save(Document);
            SaveStatus = $"已保存  {DateTime.Now:HH:mm:ss}";
            IsDirty = false;
            (Shell as MainWindowViewModel)?.RefreshTopNavUserPages();
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

    /// <summary>预览：保存当前工程并切到 Tab 10 加载当前编辑页（不影响运行模式 IsRuntimeMode）。</summary>
    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (SelectedPage is null) return;
        try
        {
            _runtimeProjectService.Save(Document);
            if (Shell is MainWindowViewModel mvm)
            {
                await mvm.NavigateToRuntimePageAsync(SelectedPage.RouteKey);
                mvm.NavigateCommand.Execute("运行页面");
            }
            Log.Information("DesignerEditor: 预览页面 {Route}", SelectedPage.RouteKey);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DesignerEditor: 预览失败");
        }
    }

    // ========== 文档初始化 ==========

    private void InitDocument()
    {
        // 共享运行时已加载的 ProjectDocument 实例（InitializeDynamicRuntime 先于此方法执行）
        Document = _runtimeProjectService.Current ?? _runtimeProjectService.LoadDefault();
        // P6: 确保资源节点存在（旧工程加载兜底；ProjectMigration 也会做同样的事）
        Document.Styles ??= new StyleDefinitions();
        Document.Styles.EnsureDefaults();
        Document.Texts ??= new TextResources();
        Document.Texts.EnsureDefaults();
        Document.Library ??= new ProjectLibrary();
        Document.Lists ??= new ListResources();
        // P7: Faceplate 库默认空集合
        Document.Faceplates ??= new FaceplateLibrary();
        DesignerContext.Document = Document;
        SelectedPage = Document.Pages.FirstOrDefault();
        // P7D: 通知工具箱刷新（"我的 Faceplate"分组）
        RefreshFaceplateToolbox();
    }

    // ========== P6 资源编辑入口 ==========

    /// <summary>P6A: 打开全局样式编辑器（色板 + 字体）。</summary>
    [RelayCommand]
    private void OpenStyleEditor()
    {
        Document.Styles ??= new StyleDefinitions();
        Document.Styles.EnsureDefaults();
        var dlg = new ApexHMI.Views.Dialogs.StyleEditorDialog(Document.Styles)
        {
            Owner = Application.Current?.MainWindow,
        };
        dlg.ShowDialog();
    }

    /// <summary>P6B: 打开多语言文本资源编辑器。</summary>
    [RelayCommand]
    private void OpenTextEditor()
    {
        Document.Texts ??= new TextResources();
        Document.Texts.EnsureDefaults();
        var dlg = new ApexHMI.Views.Dialogs.TextResourceDialog(Document.Texts)
        {
            Owner = Application.Current?.MainWindow,
        };
        dlg.ShowDialog();
        // 强制刷新当前语言下拉项
        OnPropertyChanged(nameof(AvailableLanguages));
    }

    /// <summary>P6B: 当前设计/运行时语言。绑定到顶部语言下拉。</summary>
    public string CurrentLanguage
    {
        get => DesignerContext.CurrentLanguage;
        set
        {
            if (DesignerContext.CurrentLanguage == value) return;
            DesignerContext.CurrentLanguage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>P6B: 工程支持的语言列表（绑定到顶部语言 ComboBox）。</summary>
    public System.Collections.Generic.IEnumerable<string> AvailableLanguages
        => Document?.Texts?.SupportedLanguages ?? new System.Collections.ObjectModel.ObservableCollection<string> { "zh-CN" };

    /// <summary>P6C: 把当前选中的 widget 保存到项目库。</summary>
    [RelayCommand]
    private void SaveSelectedToProjectLibrary()
    {
        if (SelectedWidget is null) return;
        Document.Library ??= new ProjectLibrary();
        var clone = LibraryService.CloneWidget(SelectedWidget);
        Document.Library.Assets.Add(new LibraryAsset
        {
            Name = $"{SelectedWidget.TypeId}_{System.DateTime.Now:HHmmss}",
            Category = "通用",
            Widget = clone,
        });
        SaveStatus = "已存入项目库";
        OnPropertyChanged(nameof(ProjectLibraryAssets));
    }

    /// <summary>P6C: 把当前选中的 widget 保存到全局库（跨工程共享）。</summary>
    [RelayCommand]
    private void SaveSelectedToGlobalLibrary()
    {
        if (SelectedWidget is null) return;
        var clone = LibraryService.CloneWidget(SelectedWidget);
        GlobalLibraryService.Instance.AddAsset(new LibraryAsset
        {
            Name = $"{SelectedWidget.TypeId}_{System.DateTime.Now:HHmmss}",
            Category = "通用",
            Widget = clone,
        });
        SaveStatus = "已存入全局库";
        OnPropertyChanged(nameof(GlobalLibraryAssets));
    }

    /// <summary>P6C: 项目库资产列表（绑定到工具箱"我的库"分组）。</summary>
    public System.Collections.ObjectModel.ObservableCollection<LibraryAsset> ProjectLibraryAssets
        => Document?.Library?.Assets ?? new System.Collections.ObjectModel.ObservableCollection<LibraryAsset>();

    /// <summary>P6C: 全局库资产列表（绑定到工具箱"全局库"分组）。</summary>
    public System.Collections.ObjectModel.ObservableCollection<LibraryAsset> GlobalLibraryAssets
        => GlobalLibraryService.Instance.Library.Assets;

    /// <summary>P6C: 从库面板插入一个资产到当前页面（指定位置）。</summary>
    public void InsertLibraryAsset(LibraryAsset asset, double x, double y)
    {
        if (SelectedPage is null) return;
        var w = LibraryService.CloneWidget(asset.Widget);
        w.X = SnapValue(x);
        w.Y = SnapValue(y);
        SelectedPage.Widgets.Add(w);
        AddWidgetItem(w);
        SelectSingleWidget(w);
    }

    /// <summary>P6C: 从项目库中移除资产。</summary>
    [RelayCommand]
    private void RemoveProjectLibraryAsset(LibraryAsset? asset)
    {
        if (asset is null || Document.Library is null) return;
        Document.Library.Assets.Remove(asset);
    }

    /// <summary>P6C: 从全局库中移除资产。</summary>
    [RelayCommand]
    private void RemoveGlobalLibraryAsset(LibraryAsset? asset)
    {
        if (asset is null) return;
        GlobalLibraryService.Instance.RemoveAsset(asset);
        OnPropertyChanged(nameof(GlobalLibraryAssets));
    }

    // ========== P6D 符号库 ==========

    /// <summary>P6D: 内置工业符号按 Category 分组，绑定到左栏 ItemsControl。</summary>
    public System.Collections.Generic.IReadOnlyList<SymbolGroup> SymbolGroups { get; } =
        SymbolLibrary.Groups().Select(g => new SymbolGroup(g.Key, g.ToList())).ToList();

    /// <summary>P6D: 把符号库一项插入当前页面（生成 graphic-view widget + iconKind 属性）。</summary>
    public void InsertSymbol(IndustrialSymbol symbol, double x, double y)
    {
        if (SelectedPage is null) return;
        var w = new WidgetInstance
        {
            TypeId = "graphic-view",
            X = SnapValue(x),
            Y = SnapValue(y),
            Width = 64,
            Height = 64,
        };
        w.Properties["iconKind"] = symbol.IconKind;
        w.Properties["iconColor"] = "#1E40AF";
        SelectedPage.Widgets.Add(w);
        AddWidgetItem(w);
        SelectSingleWidget(w);
    }

    public sealed class SymbolGroup
    {
        public SymbolGroup(string title, System.Collections.Generic.IReadOnlyList<IndustrialSymbol> items)
        { Title = title; Items = items; }
        public string Title { get; }
        public System.Collections.Generic.IReadOnlyList<IndustrialSymbol> Items { get; }
    }

    /// <summary>P6E: 打开文本/图形列表资源编辑器。</summary>
    [RelayCommand]
    private void OpenListEditor()
    {
        Document.Lists ??= new ListResources();
        var dlg = new ApexHMI.Views.Dialogs.ListResourceDialog(Document.Lists)
        {
            Owner = Application.Current?.MainWindow,
        };
        dlg.ShowDialog();
    }
}
