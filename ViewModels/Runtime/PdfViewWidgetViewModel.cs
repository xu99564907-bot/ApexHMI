#nullable enable
using System;
using System.IO;
using System.Windows;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P9B: PDF 视图 — 复用 WebView2 内置 PDF 阅读器。
/// 设计时显示占位 + 文件路径；运行时把 filePath 转 file:/// URI 给 WebView2。
/// </summary>
public partial class PdfViewWidgetViewModel : WidgetViewModelBase
{
    public PdfViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
    }

    public string FilePath  => Prop("filePath", "");
    public bool   FitToWidth => string.Equals(Prop("fitToWidth", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public string Background => Prop("background", "#F8FAFC");

    /// <summary>把本地文件路径转为 file:/// URI；非法则返回 null。</summary>
    public Uri? FileUri
    {
        get
        {
            var path = FilePath;
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (Uri.TryCreate(path, UriKind.Absolute, out var direct))
                {
                    return direct;
                }
                if (File.Exists(path))
                {
                    return new Uri(Path.GetFullPath(path), UriKind.Absolute);
                }
            }
            catch { /* ignore */ }
            return null;
        }
    }

    public bool IsDesignTimeView
    {
        get
        {
            var shell = _dataContext.Shell;
            if (shell is null) return true;
            return shell.GetType().Name.Contains("Designer", StringComparison.OrdinalIgnoreCase);
        }
    }

    public Visibility DesignPlaceholderVisibility => IsDesignTimeView ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RuntimeViewVisibility        => IsDesignTimeView ? Visibility.Collapsed : Visibility.Visible;
}
