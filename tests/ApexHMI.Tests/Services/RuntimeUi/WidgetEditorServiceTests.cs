using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using FluentAssertions;
using Xunit;

namespace ApexHMI.Tests.Services.RuntimeUi;

public class WidgetEditorServiceTests
{
    private readonly IWidgetEditorService _sut = new WidgetEditorService();

    private static PageDefinition CreatePage() => new() { Title = "测试页", RouteKey = "test" };

    [Fact]
    public void AddWidget_CreatesWithDefaults()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "button", 100, 200);

        widget.Should().NotBeNull();
        widget.Id.Should().NotBeNullOrEmpty();
        widget.TypeId.Should().Be("button");
        widget.X.Should().Be(100);
        widget.Y.Should().Be(200);
        widget.Width.Should().BeGreaterThan(0);
        widget.Height.Should().BeGreaterThan(0);
        widget.Properties.Should().ContainKey("text");
        page.Widgets.Should().HaveCount(1);
    }

    [Fact]
    public void AddWidget_UnknownTypeGetsDefaults()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "unknown-type", 50, 60);

        widget.Width.Should().Be(120);
        widget.Height.Should().Be(40);
    }

    [Fact]
    public void RemoveWidget_RemovesExisting()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "text", 0, 0);

        var ok = _sut.RemoveWidget(page, widget.Id);

        ok.Should().BeTrue();
        page.Widgets.Should().BeEmpty();
    }

    [Fact]
    public void RemoveWidget_ReturnsFalseForMissing()
    {
        var page = CreatePage();
        _sut.RemoveWidget(page, "nonexistent").Should().BeFalse();
    }

    [Fact]
    public void UpdateProperty_SetsAndNotifies()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "text", 0, 0);

        _sut.UpdateProperty(widget, "text", "新文本");

        widget.Properties["text"].Should().Be("新文本");
    }

    [Fact]
    public void UpdateProperty_RemovesKeyWhenValueNull()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "text", 0, 0);
        widget.Properties["custom"] = "test";

        _sut.UpdateProperty(widget, "custom", null);

        widget.Properties.Should().NotContainKey("custom");
    }

    [Fact]
    public void UpdateProperty_AddsNewKey()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "text", 0, 0);

        _sut.UpdateProperty(widget, "newKey", "newValue");

        widget.Properties["newKey"].Should().Be("newValue");
    }

    [Fact]
    public void UpdateBinding_SetsAndClears()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "bool-lamp", 0, 0);

        _sut.UpdateBinding(widget, new BindingSpec { TagId = "TestTag", DataType = "Bool" });
        widget.Binding!.TagId.Should().Be("TestTag");

        _sut.UpdateBinding(widget, null);
        widget.Binding.Should().BeNull();
    }

    [Fact]
    public void MoveWidget_ChangesPosition()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "button", 10, 20);

        _sut.MoveWidget(widget, 300, 400);

        widget.X.Should().Be(300);
        widget.Y.Should().Be(400);
    }

    [Fact]
    public void ResizeWidget_ChangesDimensions()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "button", 0, 0);

        _sut.ResizeWidget(widget, 250, 80);

        widget.Width.Should().Be(250);
        widget.Height.Should().Be(80);
    }

    [Fact]
    public void ResizeWidget_EnforcesMinimum()
    {
        var page = CreatePage();
        var widget = _sut.AddWidget(page, "button", 0, 0);

        _sut.ResizeWidget(widget, 5, 3);

        widget.Width.Should().Be(10);
        widget.Height.Should().Be(10);
    }

    [Fact]
    public void AddWidget_DefaultPropertiesPerType()
    {
        var page = CreatePage();

        var lamp = _sut.AddWidget(page, "bool-lamp", 0, 0);
        lamp.Properties.Should().ContainKey("trueColor");
        lamp.Properties.Should().ContainKey("falseColor");
        lamp.Properties["trueColor"].Should().Be("#22C55E");
    }

    [Fact]
    public void WidgetInstance_ObservablePropertyChange_TriggersEvent()
    {
        var widget = new WidgetInstance();
        var received = false;
        widget.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WidgetInstance.X))
                received = true;
        };

        widget.X = 123;

        received.Should().BeTrue();
    }
}
