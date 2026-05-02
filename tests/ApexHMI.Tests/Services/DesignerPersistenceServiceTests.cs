using ApexHMI.Models;
using ApexHMI.Services;
using ApexHMI.Tests.TestHelpers;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ApexHMI.Tests.Services;

public class DesignerPersistenceServiceTests
{
    [Fact]
    public async Task LayoutServiceReturnsNullForMissingPageFile()
    {
        using var tempDir = TempDir.Create();
        var service = new DesignerLayoutService();

        var page = await service.LoadPageAsync(Path.Combine(tempDir.Path, "missing", "page.json"));

        Assert.Null(page);
    }

    [Fact]
    public async Task LayoutServiceCreatesParentDirectoriesWhenSaving()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "nested", "layouts", "page.json");
        var service = new DesignerLayoutService();

        await service.SavePageAsync(path, new DesignerPage { Name = "Nested" });

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task LayoutServiceRoundTripsPageWithElements()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "layouts", "main.json");
        var service = new DesignerLayoutService();
        var page = new DesignerPage
        {
            Name = "Main",
            CanvasWidth = 1920,
            CanvasHeight = 1080,
            Elements =
            {
                new DesignerElement
                {
                    Id = "button-1",
                    Name = "StartButton",
                    ElementType = "Button",
                    Left = 10,
                    Top = 20,
                    Text = "Start",
                    TagBinding = "StartCommand"
                }
            }
        };

        await service.SavePageAsync(path, page);
        var loaded = await service.LoadPageAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal("Main", loaded!.Name);
        Assert.Equal(1920, loaded.CanvasWidth);
        Assert.Single(loaded.Elements);
        Assert.Equal("button-1", loaded.Elements[0].Id);
        Assert.Equal("Start", loaded.Elements[0].Text);
    }

    [Fact]
    public async Task ProjectServiceRoundTripsProjectWithPages()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "projects", "project.json");
        var service = new DesignerProjectService();
        var project = new DesignerProject
        {
            ProjectName = "FactoryLine",
            Pages =
            {
                new DesignerPage { Name = "Overview" },
                new DesignerPage { Name = "Manual" }
            }
        };

        await service.SaveProjectAsync(path, project);
        var loaded = await service.LoadProjectAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal("FactoryLine", loaded!.ProjectName);
        Assert.Equal(2, loaded.Pages.Count);
        Assert.Equal("Overview", loaded.Pages[0].Name);
        Assert.Equal("Manual", loaded.Pages[1].Name);
    }

    [Fact]
    public async Task ProjectServiceReturnsNullForMissingProjectFile()
    {
        using var tempDir = TempDir.Create();
        var service = new DesignerProjectService();

        var project = await service.LoadProjectAsync(Path.Combine(tempDir.Path, "missing", "project.json"));

        Assert.Null(project);
    }

    [Fact]
    public async Task ProjectServiceCreatesParentDirectoriesWhenSaving()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "nested", "projects", "project.json");
        var service = new DesignerProjectService();

        await service.SaveProjectAsync(path, new DesignerProject { ProjectName = "NestedProject" });

        Assert.True(File.Exists(path));
    }
}
