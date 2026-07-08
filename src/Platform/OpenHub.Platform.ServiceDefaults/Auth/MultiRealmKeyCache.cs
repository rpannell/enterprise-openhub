using System.Collections.Concurrent;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

internal sealed class MultiRealmKeyCache(HttpMessageHandler httpHandler)
{
    private readonly ConcurrentDictionary<string, Lazy<ConfigurationManager<OpenIdConnectConfiguration>>> _managers
        = new(StringComparer.OrdinalIgnoreCase);

    internal IEnumerable<SecurityKey> GetSigningKeys(string issuerUrl)
    {
        var lazy = _managers.GetOrAdd(
            issuerUrl,
            url => new Lazy<ConfigurationManager<OpenIdConnectConfiguration>>(
                () => CreateManager(url),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value
            .GetConfigurationAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .SigningKeys;
    }

    private ConfigurationManager<OpenIdConnectConfiguration> CreateManager(string issuerUrl)
    {
        var metadataAddress = $"{issuerUrl.TrimEnd('/')}/.well-known/openid-configuration";

        var retriever = new HttpDocumentRetriever(
            new HttpClient(httpHandler, disposeHandler: false))
        {
            RequireHttps = false
        };

        return new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            retriever);
    }
}
