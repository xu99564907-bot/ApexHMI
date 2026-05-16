#nullable enable
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 图形 I/O 域：变量值 → 图片路径列表映射。
/// entries 格式："0=C:\path\off.png;1=C:\path\on.png"。
/// </summary>
public partial class IoGraphicWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private string _currentImage = string.Empty;

    private readonly List<GraphicEntry> _entries = new();

    public IoGraphicWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        ParseEntries();
        // 默认显示第一条（设计模式可见）
        if (_entries.Count > 0) CurrentImage = _entries[0].Image;

        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
        {
            dataContext.RegisterValueCallback(tag, OnTagValueChanged);
        }
    }

    public string Mode       => Prop("mode",       "Output");

    /// <summary>M4.4: 是否参与 Tab 焦点链。Input/InputOutput 模式下可作输入目标。</summary>
    public bool IsInput => Mode is "Input" or "InputOutput";
    public bool IsTabStop => IsInput;
    public string EntriesRaw
    {
        get
        {
            // P6E: 支持 {graphicList:name} 引用展开。
            var raw = Prop("entries", "");
            return ListResolver.Resolve(raw, DesignerContext.Document?.Lists);
        }
    }
    public string Stretch    => Prop("stretch",    "Uniform");

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    private void ParseEntries()
    {
        _entries.Clear();
        var raw = EntriesRaw;
        if (string.IsNullOrWhiteSpace(raw)) return;
        foreach (var part in raw.Split(new[] { ';', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            var k = part.Substring(0, idx).Trim();
            var img = part.Substring(idx + 1).Trim();
            if (int.TryParse(k, out var v))
                _entries.Add(new GraphicEntry { Value = v, Image = img });
        }
    }

    protected override void OnTagValueChanged(string rawValue)
    {
        int v;
        if (!int.TryParse(rawValue, out v))
        {
            // 兼容 Bool
            if (rawValue is "True" or "true" or "1") v = 1;
            else if (rawValue is "False" or "false" or "0") v = 0;
            else return;
        }
        var match = _entries.FirstOrDefault(e => e.Value == v);
        if (match is not null) CurrentImage = match.Image;
    }

    private sealed class GraphicEntry
    {
        public int Value { get; set; }
        public string Image { get; set; } = string.Empty;
    }
}
