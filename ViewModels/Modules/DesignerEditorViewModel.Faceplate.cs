#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.ViewModels.Modules;

/// <summary>
/// P7D / P7E / P7C: Faceplate 相关功能切片（实例化、接口属性面板、版本升级、编辑模式）。
/// </summary>
public partial class DesignerEditorViewModel
{
    // ========== P7C: Faceplate 编辑模式 ==========

    /// <summary>是否处于 Faceplate 编辑模式（与普通页面编辑互斥）。</summary>
    [ObservableProperty]
    private bool _isFaceplateEditMode;

    /// <summary>当前选中正在编辑的 Faceplate。</summary>
    [ObservableProperty]
    private Faceplate? _selectedFaceplate;

    /// <summary>页面编辑模式 → Faceplate 编辑模式切换时，缓存原 SelectedPage 以便切回。</summary>
    private PageDefinition? _pageBeforeFaceplateEdit;

    /// <summary>Document.Faceplates.Faceplates 的便捷绑定（左栏列表用）。</summary>
    public ObservableCollection<Faceplate> AvailableFaceplates
    {
        get
        {
            if (Document is null) return _emptyFaceplateList;
            Document.Faceplates ??= new FaceplateLibrary();
            return Document.Faceplates.Faceplates;
        }
    }

    partial void OnIsFaceplateEditModeChanged(bool value)
    {
        if (value)
        {
            _pageBeforeFaceplateEdit = SelectedPage;
            if (SelectedFaceplate is null)
                SelectedFaceplate = AvailableFaceplates.FirstOrDefault();
            if (SelectedFaceplate is not null)
                SelectedPage = SelectedFaceplate.InnerScreen;
        }
        else
        {
            // 切回页面编辑：恢复原页面
            SelectedPage = _pageBeforeFaceplateEdit ?? Document?.Pages.FirstOrDefault();
            _pageBeforeFaceplateEdit = null;
        }
        OnPropertyChanged(nameof(FaceplateEditBannerText));
    }

    partial void OnSelectedFaceplateChanged(Faceplate? value)
    {
        if (IsFaceplateEditMode && value is not null)
        {
            SelectedPage = value.InnerScreen;
        }
        RefreshFaceplateMetaProperties();
        OnPropertyChanged(nameof(FaceplateEditBannerText));
    }

    /// <summary>编辑模式画布顶部 banner 文本。</summary>
    public string FaceplateEditBannerText
    {
        get
        {
            if (!IsFaceplateEditMode || SelectedFaceplate is null) return string.Empty;
            return $"正在编辑 Faceplate: {SelectedFaceplate.Name} (v{SelectedFaceplate.Version})"
                + (SelectedFaceplate.IsBuiltIn ? "  [内置]" : "");
        }
    }

    /// <summary>当前 Faceplate 的接口属性元数据（编辑用 ObservableCollection）。</summary>
    public ObservableCollection<FaceplateProperty> SelectedFaceplateProperties
    {
        get
        {
            if (SelectedFaceplate is null) return _emptyFaceplateProps;
            return SelectedFaceplate.InterfaceProperties;
        }
    }
    private static readonly ObservableCollection<FaceplateProperty> _emptyFaceplateProps = new();
    private static readonly ObservableCollection<Faceplate> _emptyFaceplateList = new();

    private void RefreshFaceplateMetaProperties()
    {
        OnPropertyChanged(nameof(SelectedFaceplateProperties));
    }

    [RelayCommand]
    private void AddNewFaceplate()
    {
        Document!.Faceplates ??= new FaceplateLibrary();
        var fp = new Faceplate
        {
            Name = $"未命名 Faceplate {Document.Faceplates.Faceplates.Count + 1}",
            Category = "通用",
            DefaultWidth = 200,
            DefaultHeight = 120,
        };
        Document.Faceplates.Faceplates.Add(fp);
        SelectedFaceplate = fp;
        IsFaceplateEditMode = true;
        RefreshFaceplateToolbox();
        MarkPageEdited();
        Log.Information("DesignerEditor: 新建 Faceplate id={Id} name={Name}", fp.Id, fp.Name);
    }

    [RelayCommand]
    private void DeleteFaceplate()
    {
        if (SelectedFaceplate is null || Document?.Faceplates is null) return;
        if (SelectedFaceplate.IsBuiltIn)
        {
            Shell.ShowPopup("删除 Faceplate", "内置 Faceplate 不可删除", "Warning");
            return;
        }
        if (!Shell.RequestConfirmation("删除 Faceplate", $"确定删除 Faceplate【{SelectedFaceplate.Name}】？\n已实例化的引用将变为占位提示。"))
            return;
        var deleted = SelectedFaceplate;
        Document.Faceplates.Faceplates.Remove(deleted);
        SelectedFaceplate = Document.Faceplates.Faceplates.FirstOrDefault();
        RefreshFaceplateToolbox();
        MarkPageEdited();
        Log.Information("DesignerEditor: 删除 Faceplate id={Id} name={Name}", deleted.Id, deleted.Name);
    }

    [RelayCommand]
    private void AddInterfaceProperty()
    {
        if (SelectedFaceplate is null) return;
        var prop = new FaceplateProperty
        {
            Key = $"prop{SelectedFaceplate.InterfaceProperties.Count + 1}",
            DisplayName = "新属性",
            Type = FaceplatePropertyType.String,
        };
        SelectedFaceplate.InterfaceProperties.Add(prop);
        MarkPageEdited();
    }

    [RelayCommand]
    private void RemoveInterfaceProperty(FaceplateProperty? prop)
    {
        if (SelectedFaceplate is null || prop is null) return;
        SelectedFaceplate.InterfaceProperties.Remove(prop);
        MarkPageEdited();
    }

    // ========== P7D: 选中 widget 是否为 Faceplate 实例 ==========

    /// <summary>选中控件是 Faceplate 实例（TypeId 以 faceplate: 开头）。</summary>
    public bool IsSelectedWidgetFaceplateInstance
        => SelectedWidget?.TypeId?.StartsWith("faceplate:", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>当前选中 Faceplate 实例对应的 Faceplate 定义（找不到为 null）。</summary>
    public Faceplate? SelectedWidgetFaceplate
    {
        get
        {
            if (!IsSelectedWidgetFaceplateInstance || SelectedWidget is null) return null;
            var fpId = SelectedWidget.TypeId!.Substring("faceplate:".Length);
            return Document?.Faceplates?.Faceplates.FirstOrDefault(f => string.Equals(f.Id, fpId, StringComparison.Ordinal));
        }
    }

    /// <summary>接口属性编辑行集合（动态填充：Faceplate.InterfaceProperties × SelectedWidget.Properties）。</summary>
    public ObservableCollection<FaceplateInterfaceArg> FaceplateInterfaceArgs { get; } = new();

    /// <summary>P7E: 选中实例的版本号是否落后于 Faceplate 当前版本。</summary>
    public bool IsFaceplateVersionOutdated
    {
        get
        {
            var fp = SelectedWidgetFaceplate;
            if (fp is null || SelectedWidget is null) return false;
            var instVer = SelectedWidget.FaceplateVersion;
            return !string.Equals(instVer, fp.Version, StringComparison.Ordinal);
        }
    }

    /// <summary>P7E: 升级提示文本。</summary>
    public string FaceplateVersionUpgradeText
    {
        get
        {
            var fp = SelectedWidgetFaceplate;
            if (fp is null) return string.Empty;
            return $"Faceplate 已更新到 v{fp.Version}（实例版本 v{SelectedWidget?.FaceplateVersion ?? "?"}）";
        }
    }

    /// <summary>SelectedWidget 变化时刷新接口属性编辑器与版本提示。</summary>
    private void RefreshFaceplateInterfaceArgs()
    {
        // 解订旧
        foreach (var arg in FaceplateInterfaceArgs) arg.PropertyChanged -= OnFaceplateArgValueChanged;
        FaceplateInterfaceArgs.Clear();

        var fp = SelectedWidgetFaceplate;
        if (fp is not null && SelectedWidget is not null)
        {
            foreach (var def in fp.InterfaceProperties)
            {
                var value = SelectedWidget.Properties.TryGetValue(def.Key, out var v) ? v : def.DefaultValue;
                var arg = new FaceplateInterfaceArg(def.Key, def.DisplayName, def.Type, value ?? string.Empty);
                arg.PropertyChanged += OnFaceplateArgValueChanged;
                FaceplateInterfaceArgs.Add(arg);
            }
        }

        OnPropertyChanged(nameof(IsSelectedWidgetFaceplateInstance));
        OnPropertyChanged(nameof(IsFaceplateVersionOutdated));
        OnPropertyChanged(nameof(FaceplateVersionUpgradeText));
    }

    private void OnFaceplateArgValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FaceplateInterfaceArg.Value)) return;
        if (sender is not FaceplateInterfaceArg arg || SelectedWidget is null) return;
        SelectedWidget.Properties[arg.Key] = arg.Value;
        SelectedWidget.NotifyPropertiesChanged();
        MarkPageEdited();
    }

    // ========== P7E: 版本升级命令 ==========

    /// <summary>升级单个选中 Faceplate 实例到当前 Faceplate 定义的版本。</summary>
    /// <remarks>
    /// 版本号匹配规则极简：直接 string 不等即视为过期。
    /// <para>TODO（v2.0）：</para>
    /// <list type="bullet">
    ///   <item>SemVer 解析 + Major 增量时弹窗确认（破坏性升级）</item>
    ///   <item>类型变更检查（DataType 改变时提示兼容性，旧值无法转换则恢复 DefaultValue）</item>
    ///   <item>批量升级当前页 / 整工程的同源实例</item>
    /// </list>
    /// </remarks>
    [RelayCommand]
    private void UpgradeFaceplateInstance()
    {
        var fp = SelectedWidgetFaceplate;
        if (fp is null || SelectedWidget is null) return;

        int added = 0;
        // 新增 key 用默认值填充；删除的 key 保留旧值（按需求不动）
        foreach (var def in fp.InterfaceProperties)
        {
            if (!SelectedWidget.Properties.ContainsKey(def.Key))
            {
                SelectedWidget.Properties[def.Key] = def.DefaultValue ?? string.Empty;
                added++;
            }
        }

        var oldVer = SelectedWidget.FaceplateVersion;
        SelectedWidget.FaceplateVersion = fp.Version;
        SelectedWidget.NotifyPropertiesChanged();
        MarkPageEdited();
        RefreshFaceplateInterfaceArgs();
        Log.Information("DesignerEditor: 升级 Faceplate 实例 widgetId={Wid} {Old} → {New}, 新增 {Added} 个接口属性",
            SelectedWidget.Id, oldVer ?? "(null)", fp.Version, added);
    }
}

/// <summary>P7D: 接口属性编辑行 VM。</summary>
public partial class FaceplateInterfaceArg : ObservableObject
{
    public FaceplateInterfaceArg(string key, string displayName, FaceplatePropertyType type, string value)
    {
        Key = key;
        DisplayName = string.IsNullOrEmpty(displayName) ? key : displayName;
        Type = type;
        _value = value;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public FaceplatePropertyType Type { get; }

    [ObservableProperty] private string _value;

    /// <summary>是否为 TagAddress 类型 — XAML DataTrigger 用，挂 TagAutoComplete。</summary>
    public bool IsTagAddress => Type == FaceplatePropertyType.TagAddress;
}
