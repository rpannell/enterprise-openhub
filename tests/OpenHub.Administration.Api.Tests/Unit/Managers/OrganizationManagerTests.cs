using NSubstitute;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Administration.Api.Managers;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Mappers;
using Shouldly;
using Xunit;

namespace OpenHub.Administration.Api.Tests.Unit.Managers;

public class OrganizationManagerTests
{
    private readonly IKeyCloakRealmAccessor _realmAccessor = Substitute.For<IKeyCloakRealmAccessor>();
    private readonly IKeyCloakOrganizationAccessor _orgAccessor = Substitute.For<IKeyCloakOrganizationAccessor>();
    private readonly OrganizationManager _sut;

    public OrganizationManagerTests()
    {
        _sut = new OrganizationManager(_realmAccessor, _orgAccessor, new OrganizationMapper());
    }

    [Fact]
    public async Task CreateOrganizationAsync_RealmDoesNotExist_ThrowsInvalidOperationException()
    {
        _realmAccessor.GetRealmBySlugAsync("client-z", Arg.Any<CancellationToken>())
            .Returns((KeyCloakRealmInfo?)null);

        var request = new CreateOrganizationDto { RealmSlug = "client-z", Name = "Z", Alias = "z" };

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.CreateOrganizationAsync(request));

        await _orgAccessor.DidNotReceive().CreateOrganizationAsync(
            Arg.Any<string>(), Arg.Any<CreateKeyCloakOrganizationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrganizationAsync_RealmExists_CreatesAndReturnsDto()
    {
        _realmAccessor.GetRealmBySlugAsync("client-c", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRealmInfo { Id = "realm-id", Realm = "client-c", Enabled = true });
        _orgAccessor.CreateOrganizationAsync("client-c", Arg.Any<CreateKeyCloakOrganizationRequest>(), Arg.Any<CancellationToken>())
            .Returns("org-1");
        _orgAccessor.GetOrganizationAsync("client-c", "org-1", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakOrganizationInfo { Id = "org-1", Name = "Client C", Alias = "client-c", Enabled = true, Domains = [] });

        var request = new CreateOrganizationDto { RealmSlug = "client-c", Name = "Client C", Alias = "client-c" };

        var result = await _sut.CreateOrganizationAsync(request);

        result.Id.ShouldBe("org-1");
        result.RealmSlug.ShouldBe("client-c");
        result.Name.ShouldBe("Client C");
    }

    [Fact]
    public async Task ListOrganizationsAsync_ExcludesMasterAndPlatformAdminRealms()
    {
        _realmAccessor.ListRealmsAsync(Arg.Any<CancellationToken>()).Returns(new List<KeyCloakRealmInfo>
        {
            new() { Id = "1", Realm = "master", Enabled = true },
            new() { Id = "2", Realm = "platform-admin", Enabled = true },
            new() { Id = "3", Realm = "client-a", Enabled = true },
        });
        _orgAccessor.ListOrganizationsAsync("client-a", Arg.Any<CancellationToken>())
            .Returns(new List<KeyCloakOrganizationInfo> { new() { Id = "org-1", Name = "Client A", Alias = "client-a", Enabled = true, Domains = [] } });

        var result = await _sut.ListOrganizationsAsync();

        result.Count.ShouldBe(1);
        result[0].RealmSlug.ShouldBe("client-a");
        await _orgAccessor.DidNotReceive().ListOrganizationsAsync("master", Arg.Any<CancellationToken>());
        await _orgAccessor.DidNotReceive().ListOrganizationsAsync("platform-admin", Arg.Any<CancellationToken>());
    }
}
