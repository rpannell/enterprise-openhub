using Microsoft.Extensions.Configuration;
using OpenHub.Accessor.ElasticIndex.Interfaces;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace OpenHub.Accessor.ElasticIndex;

/// <summary>
/// Minimal Vault KV v2 read/write accessor. Distinct from ServiceDefaults'
/// AddVaultSecrets (which only reads, once, into IConfiguration at startup) —
/// this is for secrets generated at runtime (e.g. Elasticsearch API keys), which
/// need to be persisted and read back across restarts. One Vault path per secret,
/// not one shared path with many keys, so concurrent writers (e.g. two tenants
/// being provisioned at once) never race on a read-modify-write of the same path.
/// </summary>
public class VaultSecretAccessor : IVaultSecretAccessor
{
    private readonly VaultClient? _client;

    public VaultSecretAccessor(IConfiguration configuration)
    {
        var vaultAddress = configuration["Vault:Address"];
        var vaultToken = configuration["Vault:Token"];

        if (string.IsNullOrWhiteSpace(vaultAddress))
            return;

        _client = new VaultClient(new VaultClientSettings(
            vaultAddress,
            new TokenAuthMethodInfo(vaultToken ?? string.Empty)));
    }

    public async Task<string?> ReadAsync(string path, string key, CancellationToken ct = default)
    {
        if (_client is null)
            return null;

        try
        {
            var secret = await _client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path, mountPoint: "secret");
            return secret?.Data?.Data is { } data && data.TryGetValue(key, out var value)
                ? value?.ToString()
                : null;
        }
        catch
        {
            // Path not found is a valid outcome — the caller (e.g. an
            // ensure-then-create accessor) treats null as "not provisioned yet".
            return null;
        }
    }

    public async Task WriteAsync(string path, string key, string value, CancellationToken ct = default)
    {
        if (_client is null)
            throw new InvalidOperationException(
                "Vault is not configured (Vault:Address missing) — cannot persist a secret.");

        await _client.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            path,
            data: new Dictionary<string, object> { [key] = value },
            mountPoint: "secret");
    }
}
