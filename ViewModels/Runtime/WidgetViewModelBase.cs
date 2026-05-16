using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>所有运行时 Widget ViewModel 的基类。</summary>
public abstract partial class WidgetViewModelBase : ObservableObject
{
    protected readonly IWidgetDataContext _dataContext;

    /// <summary>M3.1: 当前主绑定 Tag 的最新质量码（OPC UA StatusCode 映射），用于 UI 显示规则切换。</summary>
    private TagQuality _currentQuality = TagQuality.Good;
    public TagQuality CurrentQuality
    {
        get => _currentQuality;
        protected set
        {
            if (_currentQuality == value) return;
            _currentQuality = value;
            OnPropertyChanged(nameof(CurrentQuality));
            OnPropertyChanged(nameof(IsQualityGood));
            OnPropertyChanged(nameof(IsQualityBad));
            OnPropertyChanged(nameof(IsQualityUncertain));
            OnPropertyChanged(nameof(QualityBadge));
            OnQualityChanged(value);
        }
    }

    /// <summary>M3.1: 子类钩子，质量变化时被通知（如 IoNumeric 切到 ####/yellow）。</summary>
    protected virtual void OnQualityChanged(TagQuality quality) { }

    public bool IsQualityGood => _currentQuality == TagQuality.Good;
    public bool IsQualityBad => _currentQuality == TagQuality.Bad;
    public bool IsQualityUncertain => _currentQuality == TagQuality.Uncertain;

    /// <summary>M3.1: UI 角标符号。Good→空；Uncertain→黄三角 ⚠；Bad→红叉 ✕。</summary>
    public string QualityBadge => _currentQuality switch
    {
        TagQuality.Uncertain => "⚠",
        TagQuality.Bad => "✕",
        _ => string.Empty,
    };

    protected WidgetViewModelBase(WidgetInstance model, IWidgetDataContext dataContext)
    {
        Model = model;
        _dataContext = dataContext;

        if (model.Binding is { TagId.Length: > 0 } binding)
        {
            // M3.1: 主绑定 Tag 走 quality 回调，同时驱动 OnTagValueChanged + CurrentQuality
            dataContext.RegisterValueCallback(binding.TagId, (val, q) =>
            {
                CurrentQuality = q;
                OnTagValueChanged(val);
            });
        }

        // P2.5 状态动画：每个动画的 TagId 注册回调，触发时重新计算属性
        foreach (var anim in model.Animations)
        {
            if (string.IsNullOrWhiteSpace(anim.TagId)) continue;
            var captured = anim;
            dataContext.RegisterValueCallback(captured.TagId, val => ApplyAnimation(captured, val));
        }

        // P6B: 语言切换 → 通知所有属性变化以刷新文本绑定
        DesignerContext.LanguageChanged += OnLanguageOrResourcesChanged;
        DesignerContext.ResourcesChanged += OnResourcesChanged;
        // 注：不在这里监听 Model.PropertyChanged 触发 OnPropertyChanged(string.Empty)。
    }

    private void OnLanguageOrResourcesChanged(string _) => OnPropertyChanged(string.Empty);
    private void OnResourcesChanged() => OnPropertyChanged(string.Empty);

    /// <summary>计算并应用单个动画规则。</summary>
    private void ApplyAnimation(WidgetAnimation anim, string rawValue)
    {
        bool match = anim.Op?.ToLowerInvariant() switch
        {
            "true"  => rawValue is "1" or "True" or "true" or "TRUE",
            "false" => !(rawValue is "1" or "True" or "true" or "TRUE"),
            "eq"    => string.Equals(rawValue, anim.CompareTo, System.StringComparison.OrdinalIgnoreCase),
            "ne"    => !string.Equals(rawValue, anim.CompareTo, System.StringComparison.OrdinalIgnoreCase),
            "gt"    => CompareNum(rawValue, anim.CompareTo) > 0,
            "lt"    => CompareNum(rawValue, anim.CompareTo) < 0,
            "gte"   => CompareNum(rawValue, anim.CompareTo) >= 0,
            "lte"   => CompareNum(rawValue, anim.CompareTo) <= 0,
            _       => false
        };

        if (match && !string.IsNullOrEmpty(anim.TargetProperty))
        {
            // 写入 Model.Properties 触发 NotifyPropertiesChanged → DesignerEditor 中 view 自动重建
            Model.Properties[anim.TargetProperty] = anim.TargetValue;
            Model.NotifyPropertiesChanged();
        }
    }

    private static int CompareNum(string a, string b)
    {
        if (double.TryParse(a, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(b, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var y))
            return x.CompareTo(y);
        return string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    public WidgetInstance Model { get; }

    protected virtual void OnTagValueChanged(string rawValue) { }

    /// <summary>便捷：从 Properties 取值，无则返回 fallback。
    /// <para>P3.2 i18n: 如果值是 ${KEY} 形式，自动解析为内置 .resx 本地化资源。</para>
    /// <para>P6A: 如果值是 <c>{style:colors/xxx}</c> / <c>{style:fonts/xxx}</c>，解析工程级样式。</para>
    /// <para>P6B: 如果值是 <c>{text:keyName}</c>，按当前语言解析工程级多语言文本。</para>
    /// </summary>
    protected string Prop(string key, string fallback = "")
    {
        var raw = Model.Properties.TryGetValue(key, out var v) ? v : fallback;
        raw = ResolveLocalized(raw);
        // P7B: 若处于 Faceplate 实例的 InnerScreen 上下文，先解析 {prop:keyName} 引用为实例属性值
        raw = FaceplateResolver.Resolve(raw, _dataContext.CurrentFaceplateProperties);
        raw = StyleResolver.Resolve(raw, DesignerContext.Document?.Styles);
        raw = TextResolver.Resolve(raw, DesignerContext.Document?.Texts, DesignerContext.CurrentLanguage);
        return raw;
    }

    /// <summary>取原始 Property 值（不做任何解析），用于属性编辑器需要看到 <c>{style:...}</c> 等原始引用语法的场景。</summary>
    protected string PropRaw(string key, string fallback = "")
        => Model.Properties.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>B1C: 操作授权检查。检查 widget 的 RequiredRole 与 Properties["authorization"] 双重权限。
    /// <para>RequiredRole 沿用 P3.1（UserRole 数值递增比较）。</para>
    /// <para>Properties["authorization"] 是 WinCC 风格的"权限组名"，需要当前用户角色名（或角色名包含/等于此值）才能操作。</para>
    /// <para>留空 → 跳过该项检查。两项都通过才允许操作。</para>
    /// </summary>
    /// <param name="reason">权限不足时返回原因；通过时为 null。</param>
    protected bool CheckAuthorization(out string? reason)
    {
        reason = null;
        if (_dataContext.Shell is not ApexHMI.ViewModels.MainViewModel shell) return true;

        // 1) 老 RequiredRole 检查（按 UserRole 枚举比较）
        if (!string.IsNullOrWhiteSpace(Model.RequiredRole))
        {
            if (System.Enum.TryParse<ApexHMI.Models.UserRole>(Model.RequiredRole, true, out var required)
                && shell.CurrentUserRole < required)
            {
                reason = $"权限不足：操作需要 {required} 角色";
                return false;
            }
        }

        // 2) B1C 新增 Authorization 权限组检查（基于 Properties["authorization"]）
        var authGroup = PropRaw("authorization", "").Trim();
        if (string.IsNullOrEmpty(authGroup)) return true;

        // 当前用户角色名字符串
        var currentRole = shell.CurrentUserRole.ToString();
        if (string.Equals(currentRole, authGroup, System.StringComparison.OrdinalIgnoreCase)) return true;

        // 兼容写多个权限组（逗号/分号分隔，任一匹配即通过）
        var groups = authGroup.Split(new[] { ',', ';', '|' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var g in groups)
        {
            if (string.Equals(g.Trim(), currentRole, System.StringComparison.OrdinalIgnoreCase)) return true;
            // 名称为 UserRole 枚举值时按等级比较
            if (System.Enum.TryParse<ApexHMI.Models.UserRole>(g.Trim(), true, out var roleReq)
                && shell.CurrentUserRole >= roleReq) return true;
        }

        reason = $"权限不足：操作需要权限组 \"{authGroup}\"，当前角色 {currentRole}";
        Serilog.Log.Warning("WidgetAuthz: 拒绝操作 widget={Id} require={Auth} current={Cur}",
            Model.Id, authGroup, currentRole);
        return false;
    }

    /// <summary>B1C: 简化封装，权限不足时把原因塞进 Shell.SystemMessage。</summary>
    protected bool CheckAuthorizationAndNotify()
    {
        if (CheckAuthorization(out var reason)) return true;
        if (_dataContext.Shell is ApexHMI.ViewModels.MainViewModel shell && !string.IsNullOrEmpty(reason))
        {
            shell.SystemMessage = reason;
        }
        return false;
    }

    // ================== B2A: B1 通用字段统一暴露给所有 widget View ==================
    // 这些属性都从 Properties 字典读，无 schema 时回退默认值，因此对所有 widget 安全。

    /// <summary>B2A: 字体族（schema fontFamily, 默认 Microsoft YaHei UI）。</summary>
    public string FontFamilyName  => Prop("fontFamily", "Microsoft YaHei UI");

    /// <summary>B2A: 粗体 bool 串（fontBold）。</summary>
    public string FontBold        => Prop("fontBold", "false");

    /// <summary>B2A: 斜体 bool 串（fontItalic）。</summary>
    public string FontItalic      => Prop("fontItalic", "false");

    /// <summary>B2A: 下划线 bool 串（fontUnderline）。</summary>
    public string FontUnderline   => Prop("fontUnderline", "false");

    /// <summary>B2A: 删除线 bool 串（fontStrikeThrough）。</summary>
    public string FontStrikeThrough => Prop("fontStrikeThrough", "false");

    /// <summary>B2A: 边框颜色（borderColor）。</summary>
    public string BorderColor     => Prop("borderColor", "#CBD5E1");

    /// <summary>B2A: 边框样式（borderStyle: None / Solid / Dashed / Dotted / Double）。</summary>
    public string BorderStyleName => Prop("borderStyle", "Solid");

    /// <summary>B2A: 边框宽度（borderWidth）。</summary>
    public string BorderWidthValue => Prop("borderWidth", "0");

    /// <summary>B2A: 边框背景色（borderBackColor — 虚线/点线间隙色，目前未使用）。</summary>
    public string BorderBackColor => Prop("borderBackColor", "#FFFFFF");

    /// <summary>B3.4: 边框 StrokeDashArray（WPF Border 不支持，View 用 Rectangle 模拟）。
    /// <list type="bullet">
    ///   <item>Solid → 空 collection（None DashArray = 实线）</item>
    ///   <item>Dashed → 4,2</item>
    ///   <item>Dotted → 1,1</item>
    ///   <item>Double / None → 空（View 可用 IsBorderDouble / IsBorderVisible 控制）</item>
    /// </list></summary>
    public System.Windows.Media.DoubleCollection BorderDashArrayCollection =>
        BorderStyleName.ToLowerInvariant() switch
        {
            "dashed" => new System.Windows.Media.DoubleCollection { 4, 2 },
            "dotted" => new System.Windows.Media.DoubleCollection { 1, 1 },
            _ => new System.Windows.Media.DoubleCollection(),
        };

    /// <summary>B3.4: 是否绘制 Rectangle 边框覆盖层（Dashed/Dotted/Double 时显示）。</summary>
    public bool IsBorderDashStyle
    {
        get
        {
            var s = BorderStyleName.ToLowerInvariant();
            return s is "dashed" or "dotted" or "double";
        }
    }

    /// <summary>B3.4: 双线模式（borderStyle=Double 时 View 渲染双 Rectangle）。</summary>
    public bool IsBorderDouble =>
        string.Equals(BorderStyleName, "Double", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>B3.4: 数值化 borderWidth（View 直接绑 StrokeThickness 用）。</summary>
    public double BorderWidthDouble
    {
        get
        {
            var s = BorderWidthValue;
            return double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }

    /// <summary>B2A: 内边距 Thickness（top/right/bottom/left Margin 4 字段拼成）。</summary>
    public System.Windows.Thickness PaddingThickness
    {
        get
        {
            double Parse(string k, double def)
            {
                var s = Prop(k, def.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return double.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
            }
            return new System.Windows.Thickness(Parse("leftMargin", 0), Parse("topMargin", 0),
                                                Parse("rightMargin", 0), Parse("bottomMargin", 0));
        }
    }

    /// <summary>B2A: operatorEnable=false 时控件 IsEnabled=false（WinCC OperatorEnable）。</summary>
    public bool IsEnabledByOperator
    {
        get
        {
            var s = Prop("operatorEnable", "true");
            return !string.IsNullOrEmpty(s) &&
                   (s.Equals("true", System.StringComparison.OrdinalIgnoreCase) || s == "1");
        }
    }

    /// <summary>解析 ${KEY} 引用为本地化文本（不匹配时原样返回）。</summary>
    protected static string ResolveLocalized(string raw)
    {
        if (string.IsNullOrEmpty(raw) || !raw.StartsWith("${") || !raw.EndsWith("}"))
            return raw;
        var key = raw.Substring(2, raw.Length - 3);
        var loc = ApexHMI.Converters.LocExtension.LocalizationService;
        return loc?.GetString(key) ?? raw;
    }
}
