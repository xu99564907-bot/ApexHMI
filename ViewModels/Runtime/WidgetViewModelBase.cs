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

        // 监听 Model 变化（含尺寸/Properties），让计算属性 Prop("...") 即时刷新
        Model.PropertyChanged += (_, __) => OnPropertyChanged(string.Empty);
    }

    public WidgetInstance Model { get; }

    protected virtual void OnTagValueChanged(string rawValue) { }

    /// <summary>便捷：从 Properties 取值，无则返回 fallback。</summary>
    protected string Prop(string key, string fallback = "") =>
        Model.Properties.TryGetValue(key, out var v) ? v : fallback;
}
