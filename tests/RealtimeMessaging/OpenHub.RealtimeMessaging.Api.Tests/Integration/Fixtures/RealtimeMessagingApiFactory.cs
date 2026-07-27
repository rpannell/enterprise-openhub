using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenHub.RealtimeMessaging.Api.Managers.Interfaces;

namespace OpenHub.RealtimeMessaging.Api.Tests.Integration.Fixtures;

/// <summary>
/// Stands up the real RealtimeMessaging.Api pipeline (NotificationHub, the
/// RealtimeMessagingBroadcastPublisher authorization policy) via WebApplicationFactory, while
/// replacing everything that needs a live dependency:
///   - IMessageDispatchManager (constructor-injected into NotificationHub) → an NSubstitute mock
///     the test configures before connecting. Business-rule correctness (own-tenant vs.
///     cross-tenant) is covered separately by Unit/Managers/MessageTargetAuthorizationEngineTests
///     and Unit/Hubs/NotificationHubTests — these integration tests exist only to exercise the
///     declarative [Authorize(Policy=...)] on Broadcast, which unit-testing the Hub class
///     directly cannot cover (that's ASP.NET Core authorization middleware, not Hub-method-body
///     logic).
///   - The real JWT bearer scheme → TestAuthHandler, driven by ClaimsProvider.
///   - Every hosted service (SignalR's Redis backplane never actually resolves IHubContext in
///     these tests since the Manager is mocked, but this mirrors the defensive pattern used by
///     the other WebApplicationFactory-based test fixtures in this repo).
///   - A dummy ConnectionStrings:Redis / KeyCloak:BaseUrl, purely so DI registration in
///     Program.cs doesn't throw at startup.
/// </summary>
public sealed class RealtimeMessagingApiFactory : WebApplicationFactory<Program>
{
    public IMessageDispatchManager MessageDispatchManagerMock { get; } = Substitute.For<IMessageDispatchManager>();

    public TestAuthClaimsProvider ClaimsProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["KeyCloak:BaseUrl"] = "http://localhost:0",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Scoped(_ => MessageDispatchManagerMock));

            services.RemoveAll<IHostedService>();

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
