using ApexHMI.Models;
using ApexHMI.Services.OpcUa;
using Opc.Ua;
using Opc.Ua.Client;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApexHMI.Tests.Services;

public class OpcUaSessionFactoryTests
{
    [Theory]
    [InlineData("CreateAsync", typeof(ApplicationConfiguration), typeof(string), typeof(bool), typeof(OpcUaConnectionOptions), typeof(CancellationToken))]
    [InlineData("CreateDirectAsync", typeof(ApplicationConfiguration), typeof(string), typeof(OpcUaConnectionOptions), typeof(CancellationToken))]
    public void FactoryExposesSessionCreationEntrypoints(string methodName, params Type[] parameterTypes)
    {
        var method = typeof(OpcUaSessionFactory).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<Session>), method!.ReturnType);
    }
}
