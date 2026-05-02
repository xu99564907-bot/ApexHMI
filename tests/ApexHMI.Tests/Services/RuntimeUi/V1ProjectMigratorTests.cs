using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using FluentAssertions;
using Xunit;

namespace ApexHMI.Tests.Services.RuntimeUi;

public class V1ProjectMigratorTests
{
    [Fact]
    public void MigrateProjectConvertsProjectName()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "测试工程",
            Pages = new List<DesignerPage>()
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        result.ProjectName.Should().Be("测试工程");
        result.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void MigratePageConvertsNameToTitleAndRouteKey()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "Test",
            Pages = new List<DesignerPage>
            {
                new()
                {
                    Name = "主界面",
                    CanvasWidth = 1280,
                    CanvasHeight = 720,
                    Elements = new List<DesignerElement>()
                }
            }
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        result.Pages.Should().HaveCount(1);
        var page = result.Pages[0];
        page.Title.Should().Be("主界面");
        page.RouteKey.Should().Be("main");
        page.CanvasWidth.Should().Be(1280);
        page.CanvasHeight.Should().Be(720);
    }

    [Fact]
    public void MigrateElementMapsTypeIds()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "Test",
            Pages = new List<DesignerPage>
            {
                new()
                {
                    Name = "TestPage",
                    Elements = new List<DesignerElement>
                    {
                        new() { ElementType = "Button", Left = 10, Top = 20, Width = 100, Height = 40 },
                        new() { ElementType = "Label", Left = 30, Top = 40, Width = 200, Height = 30 },
                        new() { ElementType = "Indicator", Left = 50, Top = 60, Width = 80, Height = 30 },
                        new() { ElementType = "ValueDisplay", Left = 70, Top = 80, Width = 150, Height = 50 },
                        new() { ElementType = "Motor", Left = 90, Top = 100, Width = 180, Height = 100 },
                    }
                }
            }
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        result.Pages[0].Widgets.Should().HaveCount(5);
        result.Pages[0].Widgets[0].TypeId.Should().Be("button");
        result.Pages[0].Widgets[1].TypeId.Should().Be("text");
        result.Pages[0].Widgets[2].TypeId.Should().Be("bool-lamp");
        result.Pages[0].Widgets[3].TypeId.Should().Be("numeric-readonly");
        result.Pages[0].Widgets[4].TypeId.Should().Be("motor");
    }

    [Fact]
    public void MigrateElementPreservesPositionAndSize()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "Test",
            Pages = new List<DesignerPage>
            {
                new()
                {
                    Name = "TestPage",
                    Elements = new List<DesignerElement>
                    {
                        new()
                        {
                            ElementType = "Button",
                            Left = 42, Top = 88, Width = 120, Height = 40
                        }
                    }
                }
            }
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        var widget = result.Pages[0].Widgets[0];
        widget.X.Should().Be(42);
        widget.Y.Should().Be(88);
        widget.Width.Should().Be(120);
        widget.Height.Should().Be(40);
    }

    [Fact]
    public void MigrateElementConvertsProperties()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "Test",
            Pages = new List<DesignerPage>
            {
                new()
                {
                    Name = "TestPage",
                    Elements = new List<DesignerElement>
                    {
                        new()
                        {
                            ElementType = "Button",
                            Text = "启动",
                            Background = "#2563EB",
                            Foreground = "#FFFFFF",
                            BorderBrush = "#94A3B8",
                            FontSize = 16
                        }
                    }
                }
            }
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        var props = result.Pages[0].Widgets[0].Properties;
        props["text"].Should().Be("启动");
        props["background"].Should().Be("#2563EB");
        props["foreground"].Should().Be("#FFFFFF");
        props["borderBrush"].Should().Be("#94A3B8");
        props["fontSize"].Should().Be("16");
    }

    [Fact]
    public void MigrateElementConvertsTagBindingToBindingSpec()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "Test",
            Pages = new List<DesignerPage>
            {
                new()
                {
                    Name = "TestPage",
                    Elements = new List<DesignerElement>
                    {
                        new()
                        {
                            ElementType = "Indicator",
                            TagBinding = "MotorRun",
                            CommandBinding = string.Empty
                        }
                    }
                }
            }
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        var widget = result.Pages[0].Widgets[0];
        widget.Binding.Should().NotBeNull();
        widget.Binding!.TagId.Should().Be("MotorRun");
        widget.Binding.AccessMode.Should().Be(BindingAccessMode.Subscribe);
        widget.Binding.DataType.Should().Be("Bool");
    }

    [Fact]
    public void MigrateElementConvertsToggleBoolToReadWrite()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "Test",
            Pages = new List<DesignerPage>
            {
                new()
                {
                    Name = "TestPage",
                    Elements = new List<DesignerElement>
                    {
                        new()
                        {
                            ElementType = "Button",
                            TagBinding = "Cyl1_FwdCmd",
                            CommandBinding = "ToggleBool"
                        }
                    }
                }
            }
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        var widget = result.Pages[0].Widgets[0];
        widget.Binding.Should().NotBeNull();
        widget.Binding!.AccessMode.Should().Be(BindingAccessMode.ReadWrite);
        widget.ActionType.Should().Be("write-bool");
        widget.ActionParam.Should().Be("Cyl1_FwdCmd|True");
    }

    [Fact]
    public void MigratePageButtonConvertsToNavigateAction()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "Test",
            Pages = new List<DesignerPage>
            {
                new()
                {
                    Name = "TestPage",
                    Elements = new List<DesignerElement>
                    {
                        new()
                        {
                            ElementType = "PageButton",
                            NavigationTarget = "报警画面",
                            Text = "去报警"
                        }
                    }
                }
            }
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        var widget = result.Pages[0].Widgets[0];
        widget.TypeId.Should().Be("button");
        widget.ActionType.Should().Be("navigate");
        widget.ActionParam.Should().Be("alarm");
    }

    [Fact]
    public void MigrateProjectSetsDefaultPageRouteKey()
    {
        var v1 = new DesignerProject
        {
            ProjectName = "Test",
            Pages = new List<DesignerPage>
            {
                new() { Name = "主界面", Elements = new List<DesignerElement>() },
                new() { Name = "手动操作", Elements = new List<DesignerElement>() }
            }
        };

        var result = V1ProjectMigrator.MigrateProject(v1);

        result.DefaultPageRouteKey.Should().Be("main");
        result.Pages.Should().HaveCount(2);
        result.Pages[1].RouteKey.Should().Be("manual");
    }
}
