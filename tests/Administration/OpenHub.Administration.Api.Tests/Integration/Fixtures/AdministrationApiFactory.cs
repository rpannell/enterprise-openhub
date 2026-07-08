using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using OpenHub.Administration.Api.Managers.Interfaces;

namespace OpenHub.Administration.Api.Tests.Integration.Fixtures;

/// <summary>
/// Stands up the real Administration.Api minimal-API pipeline (routing, versioning,
/// ValidationFilter, RealmRoleClaimsTransformation, PlatformAdminRole/TenantAdminRole
/// policies) via WebApplicationFactory, while replacing everything that needs a live
/// dependency:
///   - IOrganizationManager / IUserManager → NSubstitute mocks the test configures before
///     calling the client. This API has no database and no messaging, so — unlike
///     ProductManager.Api.Tests — there's nothing else to strip or fake.
///   - The real JWT bearer scheme → TestAuthHandler, driven by ClaimsProvider.
///   - A dummy KeyCloak:BaseUrl, purely so the Keycloak accessor typed-client registrations
///     in Program.cs don't throw at startup — nothing in these tests ever resolves them,
///     since the Managers they'd normally feed are mocked out.
/// </summary>
public sealed class AdministrationApiFactory : WebApplicationFactory<Program>
{
    public IOrganizationManager OrganizationManagerMock { get; } = Substitute.For<IOrganizationManager>();
    public IUserManager UserManagerMock { get; } = Substitute.For<IUserManager>();

    public TestAuthClaimsProvider ClaimsProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KeyCloak:BaseUrl"] = "http://localhost:0",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Scoped(_ => OrganizationManagerMock));
            services.Replace(ServiceDescriptor.Scoped(_ => UserManagerMock));

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
