using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.RabbitMq;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Accessors.Models;
using OpenHub.SalesOrg.Api.Managers;
using OpenHub.SalesOrg.Api.Managers.Engines.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Options;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Contracts;

namespace OpenHub.SalesOrg.Api.Tests.Unit.Managers;

// Scoped to the publish-on-create/update/delete behavior this change adds — not full CRUD
// coverage, since no manager tests previously existed for SalesPersonManager.
[Trait("Category", "Unit")]
public class SalesPersonManagerTests
{
    private readonly ISalesPersonAccessor _personAccessor = Substitute.For<ISalesPersonAccessor>();
    private readonly ISalesAssignmentAccessor _assignmentAccessor = Substitute.For<ISalesAssignmentAccessor>();
    private readonly ISalesRoleAccessor _roleAccessor = Substitute.For<ISalesRoleAccessor>();
    private readonly ISalesOrganizationUnitAccessor _orgUnitAccessor = Substitute.For<ISalesOrganizationUnitAccessor>();
    private readonly ISalesOrgHierarchyEngine _hierarchyEngine = Substitute.For<ISalesOrgHierarchyEngine>();
    private readonly IKeyCloakUserAccessor _keyCloakUserAccessor = Substitute.For<IKeyCloakUserAccessor>();
    private readonly IMessagePublisher _messagePublisher = Substitute.For<IMessagePublisher>();
    private readonly Guid _tenantId = Guid.NewGuid();

    private SalesPersonManager CreateManager(string? exchangeName = "salesperson.changed")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = _tenantId;
        httpContext.Items["TenantSlug"] = "client-a";
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        return new SalesPersonManager(
            _personAccessor, _assignmentAccessor, _roleAccessor, _orgUnitAccessor, _hierarchyEngine,
            _keyCloakUserAccessor, new SalesPersonMapper(), new SalesAssignmentMapper(), new SalesRoleMapper(),
            new SalesOrganizationUnitMapper(), httpContextAccessor, _messagePublisher,
            Options.Create(new SalesPersonEventOptions { ExchangeName = exchangeName }));
    }

    private static SalesPersonEntity MakeEntity(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UniqueId = Guid.NewGuid().ToString(),
        FirstName = "Jane",
        LastName = "Rep",
        Email = "jane@example.com",
        ActiveStatus = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task CreateAsync_HappyPath_PublishesSalesPersonChangedEvent()
    {
        var manager = CreateManager();
        var created = MakeEntity();
        _personAccessor.CreateAsync(Arg.Any<SalesPersonEntity>(), Arg.Any<CancellationToken>()).Returns(created);
        var request = new CreateSalesPersonRequest { UniqueId = created.UniqueId!, FirstName = "Jane", LastName = "Rep", Email = "jane@example.com" };

        await manager.CreateAsync(request, Guid.NewGuid());

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesPersonChangedEvent>(e => e.SalesPersonId == created.Id && e.TenantId == _tenantId && e.IsDeleted == false),
            "salesperson.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ExchangeNameNotConfigured_DoesNotPublish()
    {
        var manager = CreateManager(exchangeName: null);
        var created = MakeEntity();
        _personAccessor.CreateAsync(Arg.Any<SalesPersonEntity>(), Arg.Any<CancellationToken>()).Returns(created);
        var request = new CreateSalesPersonRequest { UniqueId = created.UniqueId!, FirstName = "Jane", LastName = "Rep", Email = "jane@example.com" };

        await manager.CreateAsync(request, Guid.NewGuid());

        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesPersonChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NotFound_DoesNotPublish()
    {
        _personAccessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SalesPersonEntity?)null);
        var manager = CreateManager();

        var result = await manager.UpdateAsync(Guid.NewGuid(), new UpdateSalesPersonRequest(), Guid.NewGuid());

        result.ShouldBeNull();
        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesPersonChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Found_PublishesSalesPersonChangedEvent()
    {
        var existing = MakeEntity();
        _personAccessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _personAccessor.UpdateAsync(Arg.Any<SalesPersonEntity>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<SalesPersonEntity>());
        var manager = CreateManager();

        await manager.UpdateAsync(existing.Id, new UpdateSalesPersonRequest { FirstName = "New" }, Guid.NewGuid());

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesPersonChangedEvent>(e => e.SalesPersonId == existing.Id && e.IsDeleted == false),
            "salesperson.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NotFound_DoesNotPublish()
    {
        _personAccessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SalesPersonEntity?)null);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(Guid.NewGuid());

        result.ShouldBeFalse();
        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesPersonChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Found_SoftDeletes_PublishesEvent_WithIsDeletedTrue()
    {
        var existing = MakeEntity();
        _personAccessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(existing.Id);

        result.ShouldBeTrue();
        await _personAccessor.Received(1).SoftDeleteAsync(existing.Id, Arg.Any<CancellationToken>());
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesPersonChangedEvent>(e => e.SalesPersonId == existing.Id && e.IsDeleted == true),
            "salesperson.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
