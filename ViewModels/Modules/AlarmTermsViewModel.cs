#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using ApexHMI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

/// <summary>中文工业术语 → 英文缩写对照表编辑 ViewModel。</summary>
public partial class AlarmTermsViewModel : ObservableObject
{
    public ObservableCollection<AlarmTermItem> Terms { get; } = new();

    [ObservableProperty] private AlarmTermItem? selectedTerm;
    [ObservableProperty] private string statusText = string.Empty;

    public AlarmTermsViewModel()
    {
        Load();
    }

    private void Load()
    {
        Terms.Clear();
        foreach (var r in SfcCodeGeneratorService.GetAlarmTerms())
            Terms.Add(new AlarmTermItem { Cn = r.Cn, En = r.En });
    }

    [RelayCommand]
    private void AddTerm()
    {
        var item = new AlarmTermItem { Cn = "新术语", En = "NewTerm" };
        Terms.Add(item);
        SelectedTerm = item;
    }

    [RelayCommand]
    private void DeleteTerm(AlarmTermItem? item)
    {
        if (item is null) return;
        Terms.Remove(item);
    }

    [RelayCommand]
    private void MoveUp(AlarmTermItem? item)
    {
        if (item is null) return;
        var idx = Terms.IndexOf(item);
        if (idx > 0) Terms.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveDown(AlarmTermItem? item)
    {
        if (item is null) return;
        var idx = Terms.IndexOf(item);
        if (idx >= 0 && idx < Terms.Count - 1) Terms.Move(idx, idx + 1);
    }

    [RelayCommand]
    private void Save()
    {
        var records = Terms
            .Where(t => !string.IsNullOrWhiteSpace(t.Cn))
            .Select(t => new SfcCodeGeneratorService.AlarmTermRecord { Cn = t.Cn.Trim(), En = t.En.Trim() });
        SfcCodeGeneratorService.SetAlarmTerms(records);
        StatusText = $"已保存 {Terms.Count} 条术语。";
    }

    [RelayCommand]
    private void Reset()
    {
        SfcCodeGeneratorService.ResetToDefaultTerms();
        Load();
        StatusText = "已重置为内置默认值。";
    }
}

public partial class AlarmTermItem : ObservableObject
{
    [ObservableProperty] private string cn = string.Empty;
    [ObservableProperty] private string en = string.Empty;
}
