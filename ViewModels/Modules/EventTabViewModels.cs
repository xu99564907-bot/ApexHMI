#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Modules;

/// <summary>P1: 事件 Tab 中左侧的一个事件入口（如「单击」「按下」）。</summary>
public partial class EventEntryViewModel : ObservableObject
{
    public string EventId { get; init; } = "";
    public string DisplayName { get; init; } = "";

    /// <summary>已挂动作数（用于按钮上的徽章）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSteps))]
    private int _stepCount;

    /// <summary>当前是否为选中事件。</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>是否有动作（控制徽章可见性）。</summary>
    public bool HasSteps => StepCount > 0;
}

/// <summary>P1: 事件 Tab 中一个参数编辑器（一行 Label + TextBox/ComboBox）。</summary>
public partial class ArgEditorViewModel : ObservableObject
{
    private readonly ActionStep _step;

    public ArgEditorViewModel(ActionStep step, FunctionArg arg)
    {
        _step = step;
        Key = arg.Key;
        Label = arg.Label;
        ArgType = arg.Type;
        if (!_step.Args.TryGetValue(arg.Key, out var v)) v = string.Empty;
        _value = v;
    }

    public string Key { get; }
    public string Label { get; }
    public FunctionArgType ArgType { get; }

    public bool IsTagAddress     => ArgType == FunctionArgType.TagAddress;
    public bool IsText           => ArgType == FunctionArgType.Text;
    public bool IsNumber         => ArgType == FunctionArgType.Number;
    public bool IsBoolean        => ArgType == FunctionArgType.Boolean;
    public bool IsPageRouteKey   => ArgType == FunctionArgType.PageRouteKey;

    private string _value;

    /// <summary>双向绑定到 step.Args[key]。</summary>
    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value ?? string.Empty;
            _step.Args[Key] = _value;
            OnPropertyChanged();
        }
    }
}

/// <summary>P1: 事件 Tab 中一个动作步骤卡片的 VM。包装 <see cref="ActionStep"/>。</summary>
public partial class ActionStepViewModel : ObservableObject
{
    public ActionStep Model { get; }
    public SystemFunction? Function { get; }
    public string DisplayName { get; }
    public ObservableCollection<ArgEditorViewModel> ArgEditors { get; } = new();

    public ActionStepViewModel(ActionStep step)
    {
        Model = step;
        Function = SystemFunctionCatalog.GetById(step.FunctionId);
        DisplayName = Function?.DisplayName ?? step.FunctionId;
        if (Function is not null)
        {
            foreach (var a in Function.Args)
                ArgEditors.Add(new ArgEditorViewModel(step, a));
        }
        else
        {
            // 未知 FunctionId 兜底：直接列出 Args 已有项
            foreach (var kv in step.Args)
                ArgEditors.Add(new ArgEditorViewModel(step,
                    new FunctionArg(kv.Key, kv.Key, FunctionArgType.Text)));
        }
    }
}
