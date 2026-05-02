using ApexHMI.Tests.TestHelpers;
using ApexHMI.Services.OpcUa;
using Opc.Ua;
using System.Threading.Tasks;
using Xunit;

namespace ApexHMI.Tests.Services;

public class OpcUaResolvedNodeCacheStoreTests
{
    [Fact]
    public async Task SaveAsyncAndLoadAsyncRoundTripEndpointNodeIds()
    {
        using var tempDir = TempDir.Create();
        var appRoot = tempDir.Path;
        var endpoint = " OPC.TCP://LOCALHOST:4840 ";
        var nodeIds = new Dictionary<string, NodeId>(StringComparer.OrdinalIgnoreCase)
        {
            ["TagA"] = NodeId.Parse("ns=2;s=Application.TagA"),
            ["TagB"] = NodeId.Parse("ns=3;i=1001")
        };

        var writer = new OpcUaResolvedNodeCacheStore(appRoot);
        await writer.SaveAsync(OpcUaResolvedNodeCacheStore.NormalizeEndpointKey(endpoint), nodeIds);

        var reader = new OpcUaResolvedNodeCacheStore(appRoot);
        var loaded = await reader.LoadAsync(OpcUaResolvedNodeCacheStore.NormalizeEndpointKey(endpoint));

        Assert.Equal("opc.tcp://localhost:4840", OpcUaResolvedNodeCacheStore.NormalizeEndpointKey(endpoint));
        Assert.Equal(nodeIds.Count, loaded.Count);
        Assert.Equal(nodeIds["TagA"], loaded["TagA"]);
        Assert.Equal(nodeIds["TagB"], loaded["TagB"]);
    }

    [Fact]
    public async Task SaveAsyncKeepsEndpointCachesIsolated()
    {
        using var tempDir = TempDir.Create();
        var store = new OpcUaResolvedNodeCacheStore(tempDir.Path);
        var endpointA = OpcUaResolvedNodeCacheStore.NormalizeEndpointKey("opc.tcp://localhost:4840");
        var endpointB = OpcUaResolvedNodeCacheStore.NormalizeEndpointKey("opc.tcp://localhost:4841");

        await store.SaveAsync(endpointA, new Dictionary<string, NodeId>(StringComparer.OrdinalIgnoreCase)
        {
            ["SharedName"] = NodeId.Parse("ns=2;s=EndpointA")
        });
        await store.SaveAsync(endpointB, new Dictionary<string, NodeId>(StringComparer.OrdinalIgnoreCase)
        {
            ["SharedName"] = NodeId.Parse("ns=2;s=EndpointB")
        });

        var loadedA = await store.LoadAsync(endpointA);
        var loadedB = await store.LoadAsync(endpointB);

        Assert.Equal(NodeId.Parse("ns=2;s=EndpointA"), loadedA["SharedName"]);
        Assert.Equal(NodeId.Parse("ns=2;s=EndpointB"), loadedB["SharedName"]);
    }

    [Fact]
    public async Task LoadAsyncReturnsEmptyDictionaryForMissingEndpointOrFile()
    {
        using var tempDir = TempDir.Create();
        var store = new OpcUaResolvedNodeCacheStore(tempDir.Path);

        var missingFileResult = await store.LoadAsync("opc.tcp://localhost:4840");
        await store.SaveAsync("opc.tcp://localhost:4840", new Dictionary<string, NodeId>
        {
            ["Tag"] = NodeId.Parse("ns=2;s=Tag")
        });
        var missingEndpointResult = await store.LoadAsync("opc.tcp://localhost:4841");

        Assert.Empty(missingFileResult);
        Assert.Empty(missingEndpointResult);
    }
}
