using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ApexHMI.ViewModels;

public partial class NavigationItemViewModel : ObservableObject
{
    public string Title { get; }
    public ObservableCollection<NavigationItemViewModel> Children { get; } = new();

    /// <summary>
    /// 用户画布页面的 RouteKey；非 null 表示该项由开放平台工程页面动态注入，
    /// Navigate 时按 RouteKey 跳转到运行页 Tab 而不是按 Title 匹配固定段。
    /// </summary>
    public string? RouteKey { get; }

    /// <summary>用户画布页所属父段标题（如"手动操作"）；侧栏保持该段而不是切到设计器。</summary>
    public string? ParentTitle { get; }

    [ObservableProperty]
    private bool isExpanded = true;

    public NavigationItemViewModel(string title, params string[] children)
    {
        Title = title;
        foreach (var child in children)
        {
            Children.Add(new NavigationItemViewModel(child));
        }
    }

    public NavigationItemViewModel(string title)
    {
        Title = title;
    }

    public NavigationItemViewModel(string title, string routeKey, string? parentTitle = null)
    {
        Title = title;
        RouteKey = routeKey;
        ParentTitle = parentTitle;
    }
}
