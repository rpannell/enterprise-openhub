namespace OpenHub.Accessor.ElasticIndex.Interfaces;

public interface IVaultSecretAccessor
{
    Task<string?> ReadAsync(string path, string key, CancellationToken ct = default);

    Task WriteAsync(string path, string key, string value, CancellationToken ct = default);
}
