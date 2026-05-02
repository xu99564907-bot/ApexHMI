using System.Collections.Generic;
using ApexHMI.Models;
using ApexHMI.Services.DataBinding;
using FluentAssertions;
using Xunit;

namespace ApexHMI.Tests.Services.DataBinding;

public class DataPointCatalogTests
{
    [Fact]
    public void FindTagReturnsNullForUnknownTag()
    {
        var catalog = new DataPointCatalog();

        var result = catalog.FindTag("NonExistent");

        result.Should().BeNull();
    }

    [Fact]
    public void MergeAddsTagsAndFindTagReturnsThem()
    {
        var catalog = new DataPointCatalog();
        var tags = new List<TagItem>
        {
            new() { Name = "MotorRun", NodeId = "ns=3;s=\"MotorRun\"", DataType = "Bool" },
            new() { Name = "Temperature", NodeId = "ns=3;s=\"Temp\"", DataType = "Float" }
        };

        catalog.Merge(tags);

        var found = catalog.FindTag("MotorRun");
        found.Should().NotBeNull();
        found!.NodeId.Should().Be("ns=3;s=\"MotorRun\"");
        found!.DataType.Should().Be("Bool");
    }

    [Fact]
    public void FindTagIsCaseInsensitive()
    {
        var catalog = new DataPointCatalog();
        catalog.Merge(new[] { new TagItem { Name = "MotorRun", NodeId = "ns=1" } });

        var found = catalog.FindTag("motorrun");

        found.Should().NotBeNull();
        found!.Name.Should().Be("MotorRun");
    }

    [Fact]
    public void GetAllReturnsAllMergedTags()
    {
        var catalog = new DataPointCatalog();
        var beforeCount = catalog.GetAll().Count();

        catalog.Merge(new[]
        {
            new TagItem { Name = "TestTag1", NodeId = "ns=1" },
            new TagItem { Name = "TestTag2", NodeId = "ns=2" }
        });

        catalog.GetAll().Should().HaveCount(beforeCount + 2);
    }

    [Fact]
    public void MergeOverwritesExistingTagWithSameName()
    {
        var catalog = new DataPointCatalog();
        catalog.Merge(new[] { new TagItem { Name = "MotorRun", NodeId = "ns=old", DataType = "Bool" } });
        catalog.Merge(new[] { new TagItem { Name = "MotorRun", NodeId = "ns=new", DataType = "Int" } });

        var found = catalog.FindTag("MotorRun");
        found!.NodeId.Should().Be("ns=new");
        found!.DataType.Should().Be("Int");
    }

    [Fact]
    public void FindTagReturnsNullForNullOrEmpty()
    {
        var catalog = new DataPointCatalog();

        catalog.FindTag(null!).Should().BeNull();
        catalog.FindTag(string.Empty).Should().BeNull();
        catalog.FindTag("  ").Should().BeNull();
    }
}
