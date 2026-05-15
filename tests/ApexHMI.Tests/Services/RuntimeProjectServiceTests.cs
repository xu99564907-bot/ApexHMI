using System;
using System.IO;
using System.Text.Json;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services;
using FluentAssertions;
using Xunit;

namespace ApexHMI.Tests.Services;

public class RuntimeProjectServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly RuntimeProjectService _service;

    public RuntimeProjectServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ApexHMI_RPS_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new RuntimeProjectService();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void SaveAndLoadRoundTripPreservesProjectData()
    {
        var original = new ProjectDocument
        {
            SchemaVersion = 1,
            ProjectName = "RoundTripTest",
            DefaultPageRouteKey = "main",
            Pages = new System.Collections.ObjectModel.ObservableCollection<PageDefinition>
            {
                new()
                {
                    Id = "p1", Title = "Main", RouteKey = "main",
                    CanvasWidth = 800, CanvasHeight = 600
                }
            }
        };

        var filePath = Path.Combine(_testDir, "project.json");
        _service.Save(original, filePath);

        File.Exists(filePath).Should().BeTrue();

        var loaded = _service.Load(filePath);

        loaded.ProjectName.Should().Be("RoundTripTest");
        loaded.DefaultPageRouteKey.Should().Be("main");
        loaded.Pages.Should().HaveCount(1);
        loaded.Pages[0].Title.Should().Be("Main");
        loaded.Pages[0].CanvasWidth.Should().Be(800);
        loaded.Pages[0].CanvasHeight.Should().Be(600);
    }

    [Fact]
    public void SaveCreatesParentDirectoryIfNeeded()
    {
        var doc = new ProjectDocument { ProjectName = "Test" };
        var filePath = Path.Combine(_testDir, "sub", "nested", "project.json");

        _service.Save(doc, filePath);

        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void LoadSetsCurrentProperty()
    {
        var doc = new ProjectDocument { ProjectName = "SetCurrent" };
        var filePath = Path.Combine(_testDir, "setcurrent.json");
        _service.Save(doc, filePath);

        var loaded = _service.Load(filePath);

        _service.Current.Should().BeSameAs(loaded);
        _service.Current.ProjectName.Should().Be("SetCurrent");
    }

    [Fact]
    public void CurrentPropertyDefaultsToNull()
    {
        _service.Current.Should().BeNull();
    }

    [Fact]
    public void LoadOnNonExistentFileThrows()
    {
        var nonExistent = Path.Combine(_testDir, "does-not-exist.json");

        Action act = () => _service.Load(nonExistent);

        act.Should().Throw<FileNotFoundException>();
    }
}
