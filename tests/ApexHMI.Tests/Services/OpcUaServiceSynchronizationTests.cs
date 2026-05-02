using ApexHMI.Services;
using System.Collections;
using System.Reflection;
using Xunit;

namespace ApexHMI.Tests.Services;

public class OpcUaServiceSynchronizationTests
{
    [Fact]
    public void ServiceDoesNotKeepObjectLockFields()
    {
        var objectLockFields = typeof(OpcUaService)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(object))
            .Select(field => field.Name)
            .ToArray();

        Assert.Empty(objectLockFields);
    }

    [Fact]
    public void SharedCollectionFieldsUseConcurrentCollections()
    {
        var mutableCollectionFields = typeof(OpcUaService)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => typeof(IEnumerable).IsAssignableFrom(field.FieldType))
            .Where(field => field.FieldType.Namespace == "System.Collections.Generic")
            .Select(field => $"{field.FieldType.Name} {field.Name}")
            .ToArray();

        Assert.Empty(mutableCollectionFields);
    }
}
