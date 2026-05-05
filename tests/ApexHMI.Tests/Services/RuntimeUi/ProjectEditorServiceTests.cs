using System;
using System.Linq;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using FluentAssertions;
using Xunit;

namespace ApexHMI.Tests.Services.RuntimeUi;

public class ProjectEditorServiceTests
{
    private readonly IProjectEditorService _sut = new ProjectEditorService();

    private static ProjectDocument CreateDoc() => new()
    {
        ProjectName = "测试工程",
        Pages =
        {
            new PageDefinition { Title = "首页", RouteKey = "home" },
            new PageDefinition { Title = "手动", RouteKey = "manual" },
        },
    };

    [Fact]
    public void AddPage_CreatesPageWithAutoIdAndRouteKey()
    {
        var doc = CreateDoc();
        var page = _sut.AddPage(doc, "报警");

        page.Should().NotBeNull();
        page.Id.Should().NotBeNullOrEmpty();
        page.Title.Should().Be("报警");
        page.RouteKey.Should().Be("报警");
        doc.Pages.Should().HaveCount(3);
    }

    [Fact]
    public void AddPage_UsesExplicitRouteKey()
    {
        var doc = CreateDoc();
        var page = _sut.AddPage(doc, "报警页面", "alarm");

        page.RouteKey.Should().Be("alarm");
    }

    [Fact]
    public void RemovePage_RemovesWhenNotReferenced()
    {
        var doc = CreateDoc();
        var pageId = doc.Pages[0].Id;

        var ok = _sut.RemovePage(doc, pageId, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        doc.Pages.Should().HaveCount(1);
    }

    [Fact]
    public void RemovePage_BlocksWhenReferenced()
    {
        var doc = CreateDoc();
        // 第二个页面有 navigate 跳转到第一个页面的 routeKey
        doc.Pages[1].Widgets.Add(new WidgetInstance
        {
            TypeId = "button",
            ActionType = "navigate",
            ActionParam = "home",
        });

        var pageId = doc.Pages[0].Id;
        var ok = _sut.RemovePage(doc, pageId, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("引用");
        doc.Pages.Should().HaveCount(2);
    }

    [Fact]
    public void RemovePage_ReturnsFalseForMissingPage()
    {
        var doc = CreateDoc();
        var ok = _sut.RemovePage(doc, "nonexistent", out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void DuplicatePage_ClonesAllProperties()
    {
        var doc = CreateDoc();
        var source = doc.Pages[0];
        source.RequiredRole = "Engineer";
        source.CanvasWidth = 1024;
        source.CanvasHeight = 768;
        source.Widgets.Add(new WidgetInstance { TypeId = "text", X = 10, Y = 20 });

        var clone = _sut.DuplicatePage(doc, source.Id);

        clone.Should().NotBeNull();
        clone!.Title.Should().Be("首页 (副本)");
        clone.RequiredRole.Should().Be("Engineer");
        clone.CanvasWidth.Should().Be(1024);
        clone.CanvasHeight.Should().Be(768);
        clone.Widgets.Should().HaveCount(1);
        clone.Id.Should().NotBe(source.Id);
        doc.Pages.Should().HaveCount(3);
    }

    [Fact]
    public void DuplicatePage_ReturnsNullForMissingPage()
    {
        var doc = CreateDoc();
        _sut.DuplicatePage(doc, "nonexistent").Should().BeNull();
    }

    [Fact]
    public void ReorderPages_ReordersAndKeepsUnlisted()
    {
        var doc = CreateDoc();
        doc.Pages.Add(new PageDefinition { Title = "参数", RouteKey = "params" });
        var id0 = doc.Pages[0].Id;
        var id1 = doc.Pages[1].Id;
        var id2 = doc.Pages[2].Id;

        _sut.ReorderPages(doc, new[] { id2, id0 });

        doc.Pages[0].Id.Should().Be(id2);
        doc.Pages[1].Id.Should().Be(id0);
        doc.Pages[2].Id.Should().Be(id1); // 不在列表中的追加末尾
    }

    [Fact]
    public void RenamePage_SucceedsWithValidTitle()
    {
        var doc = CreateDoc();
        var ok = _sut.RenamePage(doc, doc.Pages[0].Id, "新首页", out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        doc.Pages[0].Title.Should().Be("新首页");
    }

    [Fact]
    public void RenamePage_FailsWithEmptyTitle()
    {
        var doc = CreateDoc();
        var ok = _sut.RenamePage(doc, doc.Pages[0].Id, "   ", out var error);

        ok.Should().BeFalse();
        error.Should().Contain("不能为空");
    }

    [Fact]
    public void FindPagesReferencing_FindsNavigateActions()
    {
        var doc = CreateDoc();
        doc.Pages[1].Widgets.Add(new WidgetInstance
        {
            TypeId = "button",
            ActionType = "navigate",
            ActionParam = "home",
        });

        var refs = _sut.FindPagesReferencing(doc, doc.Pages[0].Id);

        refs.Should().HaveCount(1);
        refs[0].Should().Be("手动");
    }

    [Fact]
    public void FindPagesReferencing_ReturnsEmptyWhenNoRefs()
    {
        var doc = CreateDoc();
        var refs = _sut.FindPagesReferencing(doc, doc.Pages[0].Id);
        refs.Should().BeEmpty();
    }
}
