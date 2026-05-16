#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.Views.Dialogs;

/// <summary>P6B: 多语言文本资源编辑器。
/// <para>动态根据 SupportedLanguages 生成 DataGrid 列：Key + 每种语言一列。</para>
/// </summary>
public partial class TextResourceDialog : Window
{
    private readonly TextResources _texts;

    public TextResourceDialog(TextResources texts)
    {
        InitializeComponent();
        _texts = texts;
        SupportedLangsText.Text = string.Join(",", _texts.SupportedLanguages);
        RebuildLangCombo();
        DefaultLangCombo.SelectedItem = _texts.DefaultLanguage;
        DefaultLangCombo.SelectionChanged += (_, _) =>
        {
            if (DefaultLangCombo.SelectedItem is string s) _texts.DefaultLanguage = s;
        };
        RebuildGrid();
    }

    private void RebuildLangCombo()
    {
        DefaultLangCombo.ItemsSource = _texts.SupportedLanguages.ToList();
    }

    private void RebuildGrid()
    {
        EntriesGrid.Columns.Clear();
        EntriesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Key",
            Binding = new Binding("Key") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(160),
        });
        foreach (var lang in _texts.SupportedLanguages)
        {
            // 用 IndexerBinding 写到 Values[lang]
            var binding = new Binding($"Values[{lang}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            };
            EntriesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = lang,
                Binding = binding,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            });
        }
        EntriesGrid.ItemsSource = _texts.Entries;
    }

    private void OnSupportedLangsChanged(object sender, RoutedEventArgs e)
    {
        var input = SupportedLangsText.Text ?? "";
        var langs = input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList();
        if (langs.Count == 0) langs.Add("zh-CN");
        _texts.SupportedLanguages.Clear();
        foreach (var l in langs) _texts.SupportedLanguages.Add(l);
        if (!langs.Contains(_texts.DefaultLanguage)) _texts.DefaultLanguage = langs[0];
        RebuildLangCombo();
        DefaultLangCombo.SelectedItem = _texts.DefaultLanguage;
        RebuildGrid();
    }

    private void OnAddEntry(object sender, RoutedEventArgs e)
    {
        var entry = new TextEntry { Key = "newKey" };
        foreach (var lang in _texts.SupportedLanguages) entry.Values[lang] = "";
        _texts.Entries.Add(entry);
    }

    private void OnRemoveEntry(object sender, RoutedEventArgs e)
    {
        if (EntriesGrid.SelectedItem is TextEntry t) _texts.Entries.Remove(t);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DesignerContext.NotifyResourcesChanged();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
