#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using Serilog;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 符号 I/O 域：INT 变量值 → 文本列表映射。
/// entries 格式："0=停止;1=运行;2=报警"（兼容简单 JSON 数组留待 P6）。
/// </summary>
public partial class IoSymbolicWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private string _displayText = string.Empty;
    [ObservableProperty] private SymbolicEntry? _selectedEntry;

    public ObservableCollection<SymbolicEntry> Entries { get; } = new();

    public IoSymbolicWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        ParseEntries();

        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
        {
            dataContext.RegisterValueCallback(tag, OnTagValueChanged);
        }
    }

    public string Mode       => Prop("mode",       "Output");
    public string EntriesRaw
    {
        get
        {
            // P6E: 若值为 {textList:...} 引用，先展开为 inline；否则原样返回。
            var raw = Prop("entries", "");
            return ListResolver.Resolve(raw, DesignerContext.Document?.Lists);
        }
    }
    public string Background => Prop("background", "#FFFFFF");
    public string Foreground => Prop("foreground", "#0F172A");

    public bool IsInput  => Mode is "Input" or "InputOutput";
    public bool IsOutput => Mode is "Output" or "InputOutput";

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    private void ParseEntries()
    {
        Entries.Clear();
        var raw = EntriesRaw;
        if (string.IsNullOrWhiteSpace(raw)) return;
        foreach (var part in raw.Split(new[] { ';', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            var k = part.Substring(0, idx).Trim();
            var t = part.Substring(idx + 1).Trim();
            if (int.TryParse(k, out var v))
                Entries.Add(new SymbolicEntry { Value = v, Text = t });
        }
    }

    protected override void OnTagValueChanged(string rawValue)
    {
        if (!int.TryParse(rawValue, out var v))
        {
            DisplayText = rawValue;
            return;
        }
        var match = Entries.FirstOrDefault(e => e.Value == v);
        DisplayText = match?.Text ?? rawValue;
        SelectedEntry = match;
    }

    [RelayCommand]
    private void SelectEntry(SymbolicEntry? entry)
    {
        if (!IsInput || entry is null) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;
        _dataContext.ExecuteAction("write-int", $"{tag}|{entry.Value}");
    }
}

public class SymbolicEntry
{
    public int Value { get; set; }
    public string Text { get; set; } = string.Empty;
}
