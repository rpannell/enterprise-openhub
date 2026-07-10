using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.KeyCloak;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Accessor.RabbitMq;
using OpenHub.CRM.Api.Accessors.Interfaces;
using OpenHub.CRM.Api.Accessors.Models;
using OpenHub.CRM.Api.Managers;
using OpenHub.CRM.Api.Mappers;
using OpenHub.CRM.Api.Models.Requests;
using OpenHub.CRM.Contracts;

namespace OpenHub.CRM.Api.Tests.Unit.Managers;

public class ClientManagerTests
{
    private readonly IClientAccessor _clientAccessor = Substitute.For<IClientAccessor>();
    private readonly ISalesOrgApiAccessor _salesOrgApiAccessor = Substitute.For<ISalesOrgApiAccessor>();
    private readonly IKeyCloakOrganizationAccessor _kcOrgAccessor = Substitute.For<IKeyCloakOrganizationAccessor>();
    private readonly IKeyCloakRealmAccessor _kcRealmAccessor = Substitute.For<IKeyCloakRealmAccessor>();
    private readonly IMessagePublisher _messagePublisher = Substitute.For<IMessagePublisher>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string RealmSlug = "client-a";

    private ClientManager CreateManager()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = _tenantId;
        httpContext.Items["TenantSlug"] = RealmSlug;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        _clientAccessor.GetAddressesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _clientAccessor.GetContactsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        return new ClientManager(
            _clientAccessor, _salesOrgApiAccessor, _kcOrgAccessor, _kcRealmAccessor,
            new ClientMapper(), httpContextAccessor, _messagePublisher);
    }

    private static SalesPersonApiResponse MakeSalesPerson(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FirstName = "Jane",
        LastName = "Rep",
    };

    private static ClientEntity MakeClientEntity(Guid? id = null, Guid? salesPersonId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        SalesPersonId = salesPersonId ?? Guid.NewGuid(),
        Name = "Acme Co",
        Status = "prospect",
        AccountTier = "standard",
        CreateUserId = Guid.NewGuid(),
        ModifyUserId = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static CreateClientRequest MakeCreateRequest(Guid salesPersonId) => new()
    {
        Name = "Acme Co",
        SalesPersonId = salesPersonId,
        Status = "prospect",
        AccountTier = "standard",
        Addresses = [],
        Contacts = [],
    };

    [Fact]
    public async Task CreateAsync_SalesPersonNotFound_ThrowsKeyNotFoundException()
    {
        var manager = CreateManager();
        var salesPersonId = Guid.NewGuid();
        _salesOrgApiAccessor.GetSalesPersonByIdAsync(salesPersonId, Arg.Any<CancellationToken>()).Returns((SalesPersonApiResponse?)null);

        await Should.ThrowAsync<KeyNotFoundException>(
            () => manager.CreateAsync(MakeCreateRequest(salesPersonId), Guid.NewGuid()));

        await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ClientChangedEvent>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_RealmDoesNotExist_Throws()
    {
        var manager = CreateManager();
        var salesPerson = MakeSalesPerson();
        _salesOrgApiAccessor.GetSalesPersonByIdAsync(salesPerson.Id, Arg.Any<CancellationToken>()).Returns(salesPerson);
        _kcRealmAccessor.GetRealmBySlugAsync(RealmSlug, Arg.Any<CancellationToken>()).Returns((KeyCloakRealmInfo?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => manager.CreateAsync(MakeCreateRequest(salesPerson.Id), Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_HappyPath_CreatesKeycloakOrganization_AndPublishesClientChangedEvent()
    {
        var manager = CreateManager();
        var salesPerson = MakeSalesPerson();
        var createdEntity = MakeClientEntity(salesPersonId: salesPerson.Id);
        var userId = Guid.NewGuid();

        _salesOrgApiAccessor.GetSalesPersonByIdAsync(salesPerson.Id, Arg.Any<CancellationToken>()).Returns(salesPerson);
        _kcRealmAccessor.GetRealmBySlugAsync(RealmSlug, Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRealmInfo { Id = "realm-1", Realm = RealmSlug, Enabled = true });
        _kcOrgAccessor.CreateOrganizationAsync(RealmSlug, Arg.Any<CreateKeyCloakOrganizationRequest>(), Arg.Any<CancellationToken>())
            .Returns("kc-org-123");
        _clientAccessor.CreateAsync(Arg.Any<ClientEntity>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var e = ci.Arg<ClientEntity>();
                e.Id = createdEntity.Id;
                return e;
            });
        _clientAccessor.GetByIdAsync(createdEntity.Id, Arg.Any<CancellationToken>()).Returns(createdEntity);

        var result = await manager.CreateAsync(MakeCreateRequest(salesPerson.Id), userId);

        result.Id.ShouldBe(createdEntity.Id);
        await _kcOrgAccessor.Received(1).CreateOrganizationAsync(
            RealmSlug, Arg.Is<CreateKeyCloakOrganizationRequest>(r => r.Name == "Acme Co"), Arg.Any<CancellationToken>());
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<ClientChangedEvent>(e =>
                e.ClientId == createdEntity.Id &&
                e.TenantId == _tenantId &&
                e.UserId == userId &&
                e.RealmSlug == RealmSlug &&
                e.IsDeleted == false),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_KeycloakConflictOnOwnAlias_RecoversByLookingUpExistingOrganization()
    {
        var manager = CreateManager();
        var salesPerson = MakeSalesPerson();
        var createdEntity = MakeClientEntity(salesPersonId: salesPerson.Id);
        string? capturedAlias = null;

        _salesOrgApiAccessor.GetSalesPersonByIdAsync(salesPerson.Id, Arg.Any<CancellationToken>()).Returns(salesPerson);
        _kcRealmAccessor.GetRealmBySlugAsync(RealmSlug, Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRealmInfo { Id = "realm-1", Realm = RealmSlug, Enabled = true });

        // Simulates the platform's standard HTTP resilience handler retrying our POST after the
        // original (successful) response was lost — Keycloak now says the alias we generated
        // already exists, because our own earlier attempt actually created it.
        _kcOrgAccessor.CreateOrganizationAsync(RealmSlug, Arg.Any<CreateKeyCloakOrganizationRequest>(), Arg.Any<CancellationToken>())
            .Returns<string>(ci =>
            {
                capturedAlias = ci.Arg<CreateKeyCloakOrganizationRequest>().Alias;
                throw new KeyCloakAdminConflictException("conflict");
            });
        _kcOrgAccessor.ListOrganizationsAsync(RealmSlug, Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<KeyCloakOrganizationInfo>)
                [new KeyCloakOrganizationInfo { Id = "kc-org-existing", Name = "Acme Co", Alias = capturedAlias!, Enabled = true }]);

        ClientEntity? capturedEntity = null;
        _clientAccessor.CreateAsync(Arg.Do<ClientEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var e = ci.Arg<ClientEntity>();
                e.Id = createdEntity.Id;
                return e;
            });
        _clientAccessor.GetByIdAsync(createdEntity.Id, Arg.Any<CancellationToken>()).Returns(createdEntity);

        var result = await manager.CreateAsync(MakeCreateRequest(salesPerson.Id), Guid.NewGuid());

        result.Id.ShouldBe(createdEntity.Id);
        capturedEntity!.KeycloakOrganizationId.ShouldBe("kc-org-existing");
        await _kcOrgAccessor.Received(1).ListOrganizationsAsync(RealmSlug, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_KeycloakConflictOnOrphanedOrganizationName_RecoversByNameMatch()
    {
        // Keycloak organizations enforce a unique name as well as a unique alias. An orphaned
        // organization — created successfully in some earlier attempt whose client row never
        // got written — blocks every future attempt to (re-)create a client with that name,
        // even though the alias we generate is fresh and never collides.
        var manager = CreateManager();
        var salesPerson = MakeSalesPerson();
        var createdEntity = MakeClientEntity(salesPersonId: salesPerson.Id);

        _salesOrgApiAccessor.GetSalesPersonByIdAsync(salesPerson.Id, Arg.Any<CancellationToken>()).Returns(salesPerson);
        _kcRealmAccessor.GetRealmBySlugAsync(RealmSlug, Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRealmInfo { Id = "realm-1", Realm = RealmSlug, Enabled = true });

        _kcOrgAccessor.CreateOrganizationAsync(RealmSlug, Arg.Any<CreateKeyCloakOrganizationRequest>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new KeyCloakAdminConflictException("conflict"));
        _kcOrgAccessor.ListOrganizationsAsync(RealmSlug, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<KeyCloakOrganizationInfo>)
                [new KeyCloakOrganizationInfo { Id = "kc-org-orphan", Name = "Acme Co", Alias = "acme-co-deadbeef", Enabled = true }]);

        ClientEntity? capturedEntity = null;
        _clientAccessor.CreateAsync(Arg.Do<ClientEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var e = ci.Arg<ClientEntity>();
                e.Id = createdEntity.Id;
                return e;
            });
        _clientAccessor.GetByIdAsync(createdEntity.Id, Arg.Any<CancellationToken>()).Returns(createdEntity);

        var result = await manager.CreateAsync(MakeCreateRequest(salesPerson.Id), Guid.NewGuid());

        result.Id.ShouldBe(createdEntity.Id);
        capturedEntity!.KeycloakOrganizationId.ShouldBe("kc-org-orphan");
    }

    [Fact]
    public async Task CreateAsync_KeycloakConflictMatchesNothing_RethrowsOriginalConflict()
    {
        var manager = CreateManager();
        var salesPerson = MakeSalesPerson();

        _salesOrgApiAccessor.GetSalesPersonByIdAsync(salesPerson.Id, Arg.Any<CancellationToken>()).Returns(salesPerson);
        _kcRealmAccessor.GetRealmBySlugAsync(RealmSlug, Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRealmInfo { Id = "realm-1", Realm = RealmSlug, Enabled = true });

        _kcOrgAccessor.CreateOrganizationAsync(RealmSlug, Arg.Any<CreateKeyCloakOrganizationRequest>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new KeyCloakAdminConflictException("conflict"));
        _kcOrgAccessor.ListOrganizationsAsync(RealmSlug, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<KeyCloakOrganizationInfo>)
                [new KeyCloakOrganizationInfo { Id = "kc-org-unrelated", Name = "Some Other Company", Alias = "some-other-company-abc", Enabled = true }]);

        await Should.ThrowAsync<KeyCloakAdminConflictException>(
            () => manager.CreateAsync(MakeCreateRequest(salesPerson.Id), Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAsync_ClientNotFound_ReturnsNull_DoesNotPublish()
    {
        _clientAccessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ClientEntity?)null);
        var manager = CreateManager();

        var result = await manager.UpdateAsync(Guid.NewGuid(), new UpdateClientRequest(), Guid.NewGuid());

        result.ShouldBeNull();
        await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ClientChangedEvent>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NewSalesPersonDoesNotExist_ThrowsKeyNotFoundException()
    {
        var existing = MakeClientEntity();
        var newSalesPersonId = Guid.NewGuid();
        _clientAccessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _salesOrgApiAccessor.GetSalesPersonByIdAsync(newSalesPersonId, Arg.Any<CancellationToken>()).Returns((SalesPersonApiResponse?)null);
        var manager = CreateManager();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => manager.UpdateAsync(existing.Id, new UpdateClientRequest { SalesPersonId = newSalesPersonId }, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAsync_HappyPath_PublishesClientChangedEvent_NotDeleted()
    {
        var existing = MakeClientEntity();
        var userId = Guid.NewGuid();
        _clientAccessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _clientAccessor.UpdateAsync(Arg.Any<ClientEntity>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ClientEntity>());
        var manager = CreateManager();

        var result = await manager.UpdateAsync(existing.Id, new UpdateClientRequest { Name = "New Name" }, userId);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("New Name");
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<ClientChangedEvent>(e => e.ClientId == existing.Id && e.IsDeleted == false && e.UserId == userId),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ClientNotFound_ReturnsFalse_DoesNotPublish()
    {
        _clientAccessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ClientEntity?)null);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldBeFalse();
        await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ClientChangedEvent>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ClientFound_SoftDeletes_PublishesEvent_WithIsDeletedTrue()
    {
        var existing = MakeClientEntity();
        var userId = Guid.NewGuid();
        _clientAccessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(existing.Id, userId);

        result.ShouldBeTrue();
        await _clientAccessor.Received(1).SoftDeleteAsync(existing.Id, userId, Arg.Any<CancellationToken>());
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<ClientChangedEvent>(e => e.ClientId == existing.Id && e.IsDeleted == true && e.UserId == userId),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
