#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Controls;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P5A 表格视图：DataGrid + 多列定义。
/// <para>数据源：</para>
/// <list type="bullet">
///   <item><c>dataSource=static</c>：staticRows JSON 数组</item>
///   <item><c>dataSource=tag-array</c>：订阅 variable，按列 key 取数组元素（第一版只读，不写回）</item>
///   <item><c>dataSource=sql</c>：P10 实现，占位</item>
/// </list>
/// columns 形如 <c>[{"key":"name","title":"名称","width":120},...]</c>
/// </summary>
public class TableViewWidgetViewModel : WidgetViewModelBase
{
    public TableViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        BuildColumns();
        LoadRows();

        if (string.Equals(DataSource, "tag-array", System.StringComparison.OrdinalIgnoreCase))
        {
            var tag = Prop("variable", "");
            if (!string.IsNullOrWhiteSpace(tag))
            {
                dataContext.RegisterValueCallback(tag, OnTagValueChanged);
            }
        }
    }

    public string DataSource => Prop("dataSource", "static");
    public string ColumnsRaw => Prop("columns", "[]");
    public string StaticRowsRaw => Prop("staticRows", "[]");
    public bool AllowEdit => string.Equals(Prop("allowEdit", "false"), "true", System.StringComparison.OrdinalIgnoreCase);
    public bool ShowHeader => string.Equals(Prop("showHeader", "true"), "true", System.StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<TableColumnSpec> Columns { get; } = new();
    public ObservableCollection<TableRow> Rows { get; } = new();

    private void BuildColumns()
    {
        Columns.Clear();
        try
        {
            var arr = JsonSerializer.Deserialize<List<JsonElement>>(ColumnsRaw);
            if (arr is null) return;
            foreach (var el in arr)
            {
                var c = new TableColumnSpec
                {
                    Key = el.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "",
                    Title = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Width = el.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : 100
                };
                if (!string.IsNullOrEmpty(c.Key)) Columns.Add(c);
            }
        }
        catch
        {
            // 列定义解析失败：占位单列
            Columns.Add(new TableColumnSpec { Key = "value", Title = "值", Width = 120 });
        }
    }

    private void LoadRows()
    {
        Rows.Clear();
        var ds = DataSource;
        if (string.Equals(ds, "static", System.StringComparison.OrdinalIgnoreCase))
        {
            LoadStaticRows();
        }
        else if (string.Equals(ds, "sql", System.StringComparison.OrdinalIgnoreCase))
        {
            // P10 占位：插入提示行
            var row = new TableRow();
            row.Cells["__placeholder"] = "[SQL 数据源 P10 实现]";
            Rows.Add(row);
        }
        // tag-array 等订阅触发
    }

    private void LoadStaticRows()
    {
        try
        {
            var arr = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(StaticRowsRaw);
            if (arr is null) return;
            foreach (var dict in arr)
            {
                var row = new TableRow();
                foreach (var kv in dict)
                {
                    row.Cells[kv.Key] = kv.Value.ValueKind switch
                    {
                        JsonValueKind.String => kv.Value.GetString() ?? "",
                        JsonValueKind.Number => kv.Value.GetRawText(),
                        JsonValueKind.True => "True",
                        JsonValueKind.False => "False",
                        _ => kv.Value.GetRawText()
                    };
                }
                Rows.Add(row);
            }
        }
        catch
        {
            // 忽略解析错误
        }
    }

    /// <summary>tag-array：把订阅值当作 JSON 数组解析。</summary>
    protected override void OnTagValueChanged(string rawValue)
    {
        Rows.Clear();
        if (string.IsNullOrWhiteSpace(rawValue)) return;
        try
        {
            // 支持两种格式：JSON 数组 / 逗号分隔
            if (rawValue.TrimStart().StartsWith("["))
            {
                var arr = JsonSerializer.Deserialize<List<JsonElement>>(rawValue);
                if (arr is null) return;
                int i = 0;
                foreach (var el in arr)
                {
                    var row = new TableRow();
                    // 元素是 object → 按 key 取；元素是 scalar → 用第一列接收
                    if (el.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var col in Columns)
                        {
                            if (el.TryGetProperty(col.Key, out var v))
                                row.Cells[col.Key] = v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText();
                        }
                    }
                    else if (Columns.Count > 0)
                    {
                        row.Cells[Columns[0].Key] = el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : el.GetRawText();
                    }
                    Rows.Add(row);
                    i++;
                }
            }
            else
            {
                var parts = rawValue.Split(',');
                foreach (var p in parts)
                {
                    var row = new TableRow();
                    if (Columns.Count > 0) row.Cells[Columns[0].Key] = p.Trim();
                    Rows.Add(row);
                }
            }
        }
        catch
        {
            // 解析失败保留空表
        }
    }
}

public class TableColumnSpec
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public double Width { get; set; } = 100;
}

/// <summary>表格行：列 key → 值的字典；用 INotifyPropertyChanged 触发 DataGrid 单元格更新。</summary>
public class TableRow : INotifyPropertyChanged
{
    public Dictionary<string, string> Cells { get; } = new();

    public string GetCell(string key) => Cells.TryGetValue(key, out var v) ? v : "";

    public void SetCell(string key, string value)
    {
        Cells[key] = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
