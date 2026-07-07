using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenHub.Accessor.RabbitMq;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Managers.Interfaces;

namespace OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

/// <summary>
/// Stands up the real ProductManager.Api minimal-API pipeline (routing, versioning,
/// ValidationFilter, the AdminScope authorization policy, TenantMiddleware) via
/// WebApplicationFactory, while replacing everything that would otherwise need a live
/// dependency:
///   - Every Manager interface (IProductManager, ICatalogManager, ILookupManager,
///     ITenantManager) → an NSubstitute mock the test configures before calling the client.
///   - ITenantAccessor → a mock too, since TenantMiddleware calls it directly (not through a
///     Manager) to resolve the tenant/user context from JWT claims.
///   - The real JWT bearer scheme → TestAuthHandler, driven by ClaimsProvider.
///   - The RabbitMQ publisher's hosted service (which opens a real AMQP connection on host
///     start) → removed; IMessagePublisher → a mock.
///   - Dummy config values for ConnectionStrings:Catalog / Redis and KeyCloak:BaseUrl, purely so
///     DI registration in Program.cs doesn't throw at startup — nothing above ever resolves the
///     real types those back, so nothing actually connects.
/// </summary>
public sealed class ProductManagerApiFactory : WebApplicationFactory<Program>
{
    public IProductManager ProductManagerMock { get; } = Substitute.For<IProductManager>();
    public ICatalogManager CatalogManagerMock { get; } = Substitute.For<ICatalogManager>();
    public ILookupManager LookupManagerMock { get; } = Substitute.For<ILookupManager>();
    public ITenantManager TenantManagerMock { get; } = Substitute.For<ITenantManager>();
    public ITenantAccessor TenantAccessorMock { get; } = Substitute.For<ITenantAccessor>();
    public IMessagePublisher MessagePublisherMock { get; } = Substitute.For<IMessagePublisher>();

    public TestAuthClaimsProvider ClaimsProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = "Host=localhost;Database=unused_in_controller_tests;Username=x;Password=x",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["KeyCloak:BaseUrl"] = "http://localhost:0",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Scoped(_ => ProductManagerMock));
            services.Replace(ServiceDescriptor.Scoped(_ => CatalogManagerMock));
            services.Replace(ServiceDescriptor.Scoped(_ => LookupManagerMock));
            services.Replace(ServiceDescriptor.Scoped(_ => TenantManagerMock));
            services.Replace(ServiceDescriptor.Scoped(_ => TenantAccessorMock));

            // Strip the real publisher (its IHostedService opens a real AMQP connection on host
            // start) and every other hosted service — none are needed for HTTP-pipeline tests,
            // and none are safe to run without their real backing infrastructure.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<RabbitMqMessagePublisher>();
            services.Replace(ServiceDescriptor.Singleton(MessagePublisherMock));

            services.AddSingleton(ClaimsProvider);
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
