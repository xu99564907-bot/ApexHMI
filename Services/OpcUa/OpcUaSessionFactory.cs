using ApexHMI.Models;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ApexHMI.Services.OpcUa;

public static class OpcUaSessionFactory
{
    public static async Task<Session> CreateAsync(
        ApplicationConfiguration configuration,
        string endpointUrl,
        bool useSecurity,
        OpcUaConnectionOptions options,
        CancellationToken cancellationToken)
    {
        var selectedEndpoint = await CoreClientUtils.SelectEndpointAsync(
            configuration,
            endpointUrl,
            useSecurity,
            15000,
            null!,
            cancellationToken).ConfigureAwait(false);

        var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(configuration));
        return await CreateSessionAsync(configuration, endpoint, options, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Session> CreateDirectAsync(
        ApplicationConfiguration configuration,
        string endpointUrl,
        OpcUaConnectionOptions options,
        CancellationToken cancellationToken)
    {
        var endpointDescription = new EndpointDescription
        {
            EndpointUrl = endpointUrl,
            SecurityMode = MessageSecurityMode.None,
            SecurityPolicyUri = SecurityPolicies.None
        };

        var configuredEndpoint = new ConfiguredEndpoint(
            null,
            endpointDescription,
            EndpointConfiguration.Create(configuration));

        return await CreateSessionAsync(configuration, configuredEndpoint, options, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Session> CreateSessionAsync(
        ApplicationConfiguration configuration,
        ConfiguredEndpoint endpoint,
        OpcUaConnectionOptions options,
        CancellationToken cancellationToken)
    {
        IUserIdentity userIdentity = options.UseAnonymous
            ? new UserIdentity(new AnonymousIdentityToken())
            : new UserIdentity(options.Username, Encoding.UTF8.GetBytes(options.Password));

        var sessionFactory = new DefaultSessionFactory(null!);
        return (Session)await sessionFactory.CreateAsync(
            configuration,
            endpoint,
            false,
            false,
            "ApexHMI",
            60000,
            userIdentity,
            null,
            cancellationToken).ConfigureAwait(false);
    }
}
