using Opc.Ua;
using Opc.Ua.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ApexHMI.Services.OpcUa;

public static class OpcUaApplicationConfigurationFactory
{
    public static async Task<ApplicationConfiguration> BuildAsync(string? applicationRoot = null)
    {
        var appRoot = string.IsNullOrWhiteSpace(applicationRoot)
            ? ResolveApplicationRoot()
            : applicationRoot;
        var pkiRoot = Path.Combine(appRoot, "config", "pki");
        var ownStore = Path.Combine(pkiRoot, "own");
        var trustedStore = Path.Combine(pkiRoot, "trusted");
        var issuerStore = Path.Combine(pkiRoot, "issuer");
        var rejectedStore = Path.Combine(pkiRoot, "rejected");

        Directory.CreateDirectory(ownStore);
        Directory.CreateDirectory(trustedStore);
        Directory.CreateDirectory(issuerStore);
        Directory.CreateDirectory(rejectedStore);

        var configuration = new ApplicationConfiguration
        {
            ApplicationName = "ApexHMI",
            ApplicationUri = $"urn:{Utils.GetHostName()}:ApexHMI",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = ownStore,
                    SubjectName = "CN=ApexHMI, O=OpenAI, C=CN"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = trustedStore
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = issuerStore
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = rejectedStore
                },
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 10000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
            TraceConfiguration = new Opc.Ua.TraceConfiguration()
        };

        await configuration.ValidateAsync(ApplicationType.Client);
        configuration.CertificateValidator.CertificateValidation += (_, e) => { e.Accept = true; };
        return configuration;
    }

    public static string ResolveApplicationRoot()
    {
        var currentDirectory = Environment.CurrentDirectory;
        if (File.Exists(Path.Combine(currentDirectory, "ApexHMI.csproj")))
        {
            return currentDirectory;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ApexHMI.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
