using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using OpenHub.ProductManager.Api.Accessors.Data;

namespace OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

/// <summary>
/// Starts a single real Postgres container once for the whole integration test class (see
/// <see cref="PostgresCollectionDefinition"/>), then applies the same Flyway migration SQL
/// files this platform runs in dev/prod (<c>src/Database/ProductManager/*.sql</c>) directly via
/// Npgsql, ordered by filename (Flyway's own version-ordering convention: V1_0, V1_1, ...).
///
/// This is a lightweight stand-in for the real `flyway/flyway` container AppHost.cs uses — same
/// SQL, same schema/RLS fidelity, but without the added Docker-network wiring a genuine
/// container-to-container Flyway run would need. Since RLS-scoped accessors (the whole reason
/// this fixture exists — see the class doc on ProductAccessorRlsIntegrationTests) only depend on
/// the resulting schema/policies, not on Flyway itself having run, this is equivalent for testing
/// purposes.
///
/// Exposes two connection strings, mirroring the split AppHost.cs now uses in production:
/// <see cref="ConnectionString"/> (the container's default superuser — Postgres superusers
/// always bypass Row Level Security, so this is for test setup/seeding only, never for the
/// accessor under test) and <see cref="AppUserConnectionString"/> (the non-superuser role
/// created by V1_8__create_app_role.sql — <see cref="CreateDbContext"/> always uses this one,
/// since that's what actually exercises RLS).
/// </summary>
public sealed class PostgresCollectionFixture : IAsyncLifetime
{
    private const string AppUserPassword = "Test_AppUser_P@ss1!";

    private static readonly string MigrationsDirectory =
        Path.Combine(FindRepoRoot(), "src", "Database", "ProductManager");

    private PostgreSqlContainer? _container;

    public string ConnectionString => _container!.GetConnectionString();

    public string AppUserConnectionString =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "app_user",
            Password = AppUserPassword,
        }.ConnectionString;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16")
            .WithDatabase("catalog_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _container.StartAsync();

        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Creates a fresh <see cref="CatalogDbContext"/> connected as app_user (see class doc).</summary>
    public CatalogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(AppUserConnectionString)
            .Options;
        return new CatalogDbContext(options);
    }

    /// <summary>
    /// Creates a <see cref="CatalogDbContext"/> connected as the Postgres superuser — for test
    /// setup/assertions only. Never use this against RLS-scoped tables (catalog.users, etc.) to
    /// exercise the accessor under test; use it to read back results the accessor already wrote,
    /// bypassing RLS the same way seeding does. Querying those tables with the app_user
    /// connection outside of a transaction that set app.current_tenant_id would fail the
    /// `current_setting(...)::UUID` cast in the RLS policy (once touched, a custom GUC reads back
    /// as an empty string rather than erroring after the setting transaction ends).
    /// </summary>
    public CatalogDbContext CreateSuperuserDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new CatalogDbContext(options);
    }

    private async Task ApplyMigrationsAsync()
    {
        var files = Directory.GetFiles(MigrationsDirectory, "*.sql").OrderBy(f => f, StringComparer.Ordinal);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var file in files)
        {
            var sql = await File.ReadAllTextAsync(file);
            // This lightweight runner has no Flyway-style placeholder engine — substitute the
            // one placeholder V1_8__create_app_role.sql uses directly.
            sql = sql.Replace("${app_user_password}", AppUserPassword);
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Database", "ProductManager")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate repo root (src/Database/ProductManager) from " + AppContext.BaseDirectory);
    }
}
