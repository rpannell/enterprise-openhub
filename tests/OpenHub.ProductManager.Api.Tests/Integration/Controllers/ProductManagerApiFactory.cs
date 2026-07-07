using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenHub.Accessor.RabbitMq;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Tests.Integration.Controllers;

/// <summary>
/// Fixed identifiers used by the fake "Test" authentication scheme below and asserted against
/// by any test that needs to know what tenant/user the fake caller belongs to.
/// </summary>
public static class TestIdentity
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public const string RealmSlug = "test-realm";
}

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> for OpenHub.ProductManager.Api that
/// swaps out the real infrastructure dependencies (Keycloak JWT auth, tenant provisioning,
/// catalog persistence) for test doubles, while leaving FluentValidation's real, DI-registered
/// validators (<see cref="IValidator{CreateBrandRequest}"/> / <see cref="IValidator{CreateManufacturerRequest}"/>)
/// intact — those are what the validation-problem tests exercise.
/// </summary>
public class ProductManagerApiFactory : WebApplicationFactory<Program>
{
    public ICatalogManager CatalogManager { get; } = Substitute.For<ICatalogManager>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        // Dummy connection strings / config so builder.Build() doesn't throw when Aspire
        // integrations (Npgsql, Redis) and the RabbitMQ publisher wire themselves up.
        // Consul/Vault are already skipped when Consul:Url / Vault:Address are unset.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:Port"] = "5672",
                ["RabbitMq:UserName"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["RabbitMq:Exchange"] = "test-exchange",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace real Keycloak JWT bearer auth with a fake scheme that always succeeds
            // and carries the claims TenantMiddleware needs (tenant_id, iss, sub, email, given_name, family_name).
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });

            // Replace tenant provisioning with a fake that always resolves to a fixed tenant/user.
            var tenantAccessor = Substitute.For<ITenantAccessor>();
            tenantAccessor
                .GetOrProvisionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new TenantDto
                {
                    Id = TestIdentity.TenantId,
                    Slug = TestIdentity.RealmSlug,
                    Name = "Test Tenant",
                    PlanTier = "standard",
                    IsActive = true
                });
            tenantAccessor
                .GetOrProvisionUserAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(TestIdentity.UserId);

            services.RemoveAll<ITenantAccessor>();
            services.AddScoped(_ => tenantAccessor);

            // Replace the catalog manager with a fake so no real database is required.
            // Note: IValidator<CreateBrandRequest> / IValidator<CreateManufacturerRequest> are
            // intentionally left as the real, DI-registered FluentValidation validators.
            services.RemoveAll<ICatalogManager>();
            services.AddScoped(_ => CatalogManager);

            // RabbitMqMessagePublisher is registered as an IHostedService that opens a real
            // broker connection in StartAsync — remove it, it's the only hosted service the
            // app registers and nothing under test publishes to RabbitMQ.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton(Substitute.For<IMessagePublisher>());
        });
    }
}

/// <summary>
/// Always-succeeding authentication handler used in place of the real Keycloak JWT bearer
/// scheme. Issues the exact claims <see cref="OpenHub.ProductManager.Api.Middleware.TenantMiddleware"/>
/// reads: tenant_id, iss, sub, email, given_name, family_name.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("tenant_id", TestIdentity.TenantId.ToString()),
            new Claim("iss", $"https://keycloak.test/realms/{TestIdentity.RealmSlug}"),
            new Claim("sub", "test-user-sub"),
            new Claim("email", "test.user@example.com"),
            new Claim("given_name", "Test"),
            new Claim("family_name", "User"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
