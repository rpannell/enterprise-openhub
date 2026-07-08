using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.KeyCloak.Tests;

public class KeyCloakAccessorExtensionsTests
{
    private static (ServiceCollection Services, IConfiguration Configuration) CreateServices(Dictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        return (services, configuration);
    }

    [Fact]
    public void AddKeyCloakAccessor_RegistersOptionsFromConfiguration()
    {
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["KeyCloak:BaseUrl"] = "http://keycloak.test",
            ["KeyCloak:AdminUsername"] = "svc-admin",
            ["KeyCloak:AdminPassword"] = "p@ss",
        });
        services.AddKeyCloakAccessor(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<KeyCloakOptions>>().Value;

        options.BaseUrl.ShouldBe("http://keycloak.test");
        options.AdminUsername.ShouldBe("svc-admin");
        options.AdminPassword.ShouldBe("p@ss");
    }

    [Fact]
    public void AddKeyCloakAccessor_RegistersIKeyCloakRealmAccessorAsResolvable()
    {
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["KeyCloak:BaseUrl"] = "http://keycloak.test",
            ["KeyCloak:AdminPassword"] = "p@ss",
        });
        services.AddKeyCloakAccessor(configuration);
        using var provider = services.BuildServiceProvider();

        var accessor = provider.GetRequiredService<IKeyCloakRealmAccessor>();

        accessor.ShouldBeOfType<KeyCloakRealmAccessor>();
    }

    [Fact]
    public async Task AddKeyCloakAccessor_KeyCloakBaseUrlConfigured_SetsHttpClientBaseAddressFromKeyCloakSection()
    {
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["KeyCloak:BaseUrl"] = "http://from-keycloak-section.test",
            ["KeyCloak:AdminPassword"] = "p@ss",
        });
        services.AddKeyCloakAccessor(configuration);
        var fakeHandler = new FakeHttpMessageHandler()
            .When(HttpMethod.Post, "protocol/openid-connect/token", _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"t","expires_in":300}""", System.Text.Encoding.UTF8, "application/json"),
            });
        // AddHttpClient calls for the same typed client are additive, not
        // replacing — this augments the registration from AddKeyCloakAccessor
        // with a fake primary handler so we can observe the resolved BaseAddress
        // via the actual outgoing request host, without reflecting into private state.
        services.AddHttpClient<IKeyCloakRealmAccessor, KeyCloakRealmAccessor>()
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IKeyCloakRealmAccessor>();

        try { await accessor.GetRealmBySlugAsync("acme"); } catch { /* only the request host matters here */ }

        fakeHandler.Requests.ShouldNotBeEmpty();
        fakeHandler.Requests[0].RequestUri!.Host.ShouldBe("from-keycloak-section.test");
    }

    [Fact]
    public async Task AddKeyCloakAccessor_OnlyConnectionStringConfigured_FallsBackToConnectionStringsKeycloak()
    {
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["ConnectionStrings:keycloak"] = "http://from-connection-string.test",
        });
        services.AddKeyCloakAccessor(configuration);
        var fakeHandler = new FakeHttpMessageHandler()
            .When(HttpMethod.Post, "protocol/openid-connect/token", _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"t","expires_in":300}""", System.Text.Encoding.UTF8, "application/json"),
            });
        services.AddHttpClient<IKeyCloakRealmAccessor, KeyCloakRealmAccessor>()
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IKeyCloakRealmAccessor>();

        try { await accessor.GetRealmBySlugAsync("acme"); } catch { /* only the request host matters here */ }

        fakeHandler.Requests.ShouldNotBeEmpty();
        fakeHandler.Requests[0].RequestUri!.Host.ShouldBe("from-connection-string.test");
    }

    [Fact]
    public async Task AddKeyCloakAccessor_BothConfigured_PrefersKeyCloakBaseUrlOverConnectionString()
    {
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["KeyCloak:BaseUrl"] = "http://from-keycloak-section.test",
            ["KeyCloak:AdminPassword"] = "p@ss",
            ["ConnectionStrings:keycloak"] = "http://from-connection-string.test",
        });
        services.AddKeyCloakAccessor(configuration);
        var fakeHandler = new FakeHttpMessageHandler()
            .When(HttpMethod.Post, "protocol/openid-connect/token", _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"t","expires_in":300}""", System.Text.Encoding.UTF8, "application/json"),
            });
        services.AddHttpClient<IKeyCloakRealmAccessor, KeyCloakRealmAccessor>()
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IKeyCloakRealmAccessor>();

        try { await accessor.GetRealmBySlugAsync("acme"); } catch { /* only the request host matters here */ }

        fakeHandler.Requests.ShouldNotBeEmpty();
        fakeHandler.Requests[0].RequestUri!.Host.ShouldBe("from-keycloak-section.test");
    }

    [Fact]
    public void AddKeyCloakAccessor_NeitherConfigured_ThrowsInvalidOperationExceptionOnFirstResolve()
    {
        var (services, configuration) = CreateServices([]);
        services.AddKeyCloakAccessor(configuration);
        using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IKeyCloakRealmAccessor>());
    }

    [Fact]
    public void AddKeyCloakAccessor_AdminUsernameNotConfigured_DefaultsToAdmin()
    {
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["KeyCloak:BaseUrl"] = "http://keycloak.test",
            ["KeyCloak:AdminPassword"] = "p@ss",
        });
        services.AddKeyCloakAccessor(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<KeyCloakOptions>>().Value;

        options.AdminUsername.ShouldBe("admin");
    }
}
