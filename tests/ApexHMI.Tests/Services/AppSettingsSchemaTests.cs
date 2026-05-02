using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ApexHMI.Models;
using Xunit;

namespace ApexHMI.Tests.Services;

public class AppSettingsSchemaTests
{
    [Fact]
    public void AppSettingsSchemaCoversCurrentAppConfigRootProperties()
    {
        var schemaPath = Path.Combine(FindRepositoryRoot(), "config", "appsettings.schema.json");

        Assert.True(File.Exists(schemaPath), $"Missing schema file: {schemaPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var root = document.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.True(root.TryGetProperty("$schema", out _));
        Assert.True(root.TryGetProperty("properties", out var schemaProperties));

        var modelProperties = typeof(AppConfig)
            .GetProperties()
            .Where(property => property.CanRead && property.CanWrite)
            .Select(property => property.Name);

        foreach (var propertyName in modelProperties)
        {
            Assert.True(
                schemaProperties.TryGetProperty(propertyName, out _),
                $"appsettings.schema.json is missing AppConfig.{propertyName}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ApexHMI.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
