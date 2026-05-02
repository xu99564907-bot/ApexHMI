using ApexHMI.Models;
using ApexHMI.Services;
using ApexHMI.Tests.TestHelpers;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ApexHMI.Tests.Services;

public class NamingRulesServiceTests
{
    [Fact]
    public async Task LoadOrCreateAsyncCreatesDefaultConfigWhenFileIsMissing()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "config", "naming-rules.json");
        var service = new NamingRulesService();

        var config = await service.LoadOrCreateAsync(path);

        Assert.True(File.Exists(path));
        Assert.NotNull(config.Cylinder);
        Assert.NotNull(config.Axis);
        Assert.Contains("_", config.Cylinder.SegmentSeparators);
        Assert.NotEmpty(config.Axis.PositiveKeywords);
    }

    [Fact]
    public async Task SaveAsyncNormalizesMissingAndEmptyCollectionsBeforeWriting()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "naming-rules.json");
        var service = new NamingRulesService();
        var config = new NamingRulesConfig
        {
            Cylinder = new CylinderNamingRules(),
            Axis = new AxisNamingRules()
        };

        await service.SaveAsync(path, config);
        var loaded = await service.LoadOrCreateAsync(path);

        Assert.Equal("ByRowOrder", loaded.Cylinder.MotionAssignmentMode);
        Assert.Equal("Work", loaded.Cylinder.FirstOccurrenceRole);
        Assert.Equal("Home", loaded.Cylinder.SecondOccurrenceRole);
        Assert.Equal(new[] { "A", "B", "C", "D" }, loaded.Cylinder.GroupedSuffixes);
        Assert.Equal(new[] { "_" }, loaded.Cylinder.SegmentSeparators);
    }

    [Fact]
    public async Task LoadOrCreateAsyncReplacesInvalidJsonWithFallback()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "naming-rules.json");
        File.WriteAllText(path, "null");
        var service = new NamingRulesService();

        var config = await service.LoadOrCreateAsync(path);
        var savedJson = File.ReadAllText(path);
        var savedConfig = JsonSerializer.Deserialize<NamingRulesConfig>(savedJson);

        Assert.NotNull(config.Cylinder);
        Assert.NotNull(savedConfig);
        Assert.NotEmpty(savedConfig!.Cylinder.HomeKeywords);
    }
}
