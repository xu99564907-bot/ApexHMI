using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>所有运行时 Widget ViewModel 的基类。</summary>
public abstract partial class WidgetViewModelBase : ObservableObject
{
    protected readonly IWidgetDataContext _dataContext;

    protected WidgetViewModelBase(WidgetInstance model, IWidgetDataContext dataContext)
    {
        Model = model;
        _dataContext = dataContext;

        if (model.Binding is { TagId.Length: > 0 } binding)
        {
            dataContext.RegisterValueCallback(binding.TagId, OnTagValueChanged);
        }

        // 注：不在这里监听 Model.PropertyChanged 触发 OnPropertyChanged(string.Empty)，
        // 实测会导致设计模式下大量重复刷新引起界面卡死。属性即时预览将在后续以更
        // 精细的方式实现（按需重建 view，或运行时 Properties 字典 INotify 化）。
    }

    public WidgetInstance Model { get; }

    protected virtual void OnTagValueChanged(string rawValue) { }

    /// <summary>便捷：从 Properties 取值，无则返回 fallback。</summary>
    protected string Prop(string key, string fallback = "") =>
        Model.Properties.TryGetValue(key, out var v) ? v : fallback;
}
