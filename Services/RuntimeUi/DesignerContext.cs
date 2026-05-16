#nullable enable
using System;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// P6: 工程级共享上下文。Widget VM / 设计器 / 运行时通过它访问：
///   - 当前 <see cref="ProjectDocument"/>（样式、文本、库、列表资源）
///   - 当前语言（驱动 <see cref="TextResolver"/>）
///
/// 用静态单例而非注入接口是为了避免对 IWidgetDataContext 做侵入式扩展——
/// 已有近 20 个 widget VM 通过 base.Prop() 取值，由该入口统一注入解析。
/// MainWindowViewModel 在加载工程后设置 <see cref="Document"/>。
/// </summary>
public static class DesignerContext
{
    public static ProjectDocument? Document { get; set; }

    private static string _currentLanguage = "zh-CN";
    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value) return;
            _currentLanguage = value;
            LanguageChanged?.Invoke(value);
        }
    }

    /// <summary>语言切换事件：widget VM 可订阅以触发文本相关属性的 PropertyChanged 通知。</summary>
    public static event Action<string>? LanguageChanged;

    /// <summary>样式或文本资源变更事件：编辑后调用以驱动设计器和运行时刷新。</summary>
    public static event Action? ResourcesChanged;

    public static void NotifyResourcesChanged() => ResourcesChanged?.Invoke();
}
