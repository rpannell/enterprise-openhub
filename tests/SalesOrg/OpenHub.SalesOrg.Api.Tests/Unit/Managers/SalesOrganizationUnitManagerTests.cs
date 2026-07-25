using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
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
// coverage, since no manager tests previously existed for SalesOrganizationUnitManager.
[Trait("Category", "Unit")]
public class SalesOrganizationUnitManagerTests
{
    private readonly ISalesOrganizationUnitAccessor _accessor = Substitute.For<ISalesOrganizationUnitAccessor>();
    private readonly ISalesOrgHierarchyEngine _hierarchyEngine = Substitute.For<ISalesOrgHierarchyEngine>();
    private readonly IMessagePublisher _messagePublisher = Substitute.For<IMessagePublisher>();
    private readonly Guid _tenantId = Guid.NewGuid();

    private SalesOrganizationUnitManager CreateManager(string? exchangeName = "salesorganizationunit.changed")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = _tenantId;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        return new SalesOrganizationUnitManager(
            _accessor, _hierarchyEngine, new SalesOrganizationUnitMapper(), httpContextAccessor, _messagePublisher,
            Options.Create(new SalesOrganizationUnitEventOptions { ExchangeName = exchangeName }));
    }

    private static SalesOrganizationUnitEntity MakeEntity(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Northeast",
        OrganizationUnitType = "region",
        ActiveStatus = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task CreateAsync_HappyPath_PublishesSalesOrganizationUnitChangedEvent()
    {
        var manager = CreateManager();
        var created = MakeEntity();
        _accessor.CreateAsync(Arg.Any<SalesOrganizationUnitEntity>(), Arg.Any<CancellationToken>()).Returns(created);
        var request = new CreateSalesOrganizationUnitRequest { Name = "Northeast", OrganizationUnitType = "region" };

        await manager.CreateAsync(request, Guid.NewGuid());

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesOrganizationUnitChangedEvent>(e => e.SalesOrganizationUnitId == created.Id && e.TenantId == _tenantId && e.IsDeleted == false),
            "salesorganizationunit.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ExchangeNameNotConfigured_DoesNotPublish()
    {
        var manager = CreateManager(exchangeName: "");
        var created = MakeEntity();
        _accessor.CreateAsync(Arg.Any<SalesOrganizationUnitEntity>(), Arg.Any<CancellationToken>()).Returns(created);
        var request = new CreateSalesOrganizationUnitRequest { Name = "Northeast", OrganizationUnitType = "region" };

        await manager.CreateAsync(request, Guid.NewGuid());

        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesOrganizationUnitChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NotFound_DoesNotPublish()
    {
        _accessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SalesOrganizationUnitEntity?)null);
        var manager = CreateManager();

        var result = await manager.UpdateAsync(Guid.NewGuid(), new UpdateSalesOrganizationUnitRequest(), Guid.NewGuid());

        result.ShouldBeNull();
        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesOrganizationUnitChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Found_PublishesSalesOrganizationUnitChangedEvent()
    {
        var existing = MakeEntity();
        _accessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _accessor.UpdateAsync(Arg.Any<SalesOrganizationUnitEntity>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<SalesOrganizationUnitEntity>());
        var manager = CreateManager();

        await manager.UpdateAsync(existing.Id, new UpdateSalesOrganizationUnitRequest { Name = "New Name" }, Guid.NewGuid());

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesOrganizationUnitChangedEvent>(e => e.SalesOrganizationUnitId == existing.Id && e.IsDeleted == false),
            "salesorganizationunit.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NotFound_DoesNotPublish()
    {
        _accessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SalesOrganizationUnitEntity?)null);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldBeFalse();
        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesOrganizationUnitChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Found_SoftDeletes_PublishesEvent_WithIsDeletedTrue()
    {
        var existing = MakeEntity();
        var deleteUserId = Guid.NewGuid();
        _accessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(existing.Id, deleteUserId);

        result.ShouldBeTrue();
        await _accessor.Received(1).SoftDeleteAsync(existing.Id, deleteUserId, Arg.Any<CancellationToken>());
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesOrganizationUnitChangedEvent>(e => e.SalesOrganizationUnitId == existing.Id && e.IsDeleted == true),
            "salesorganizationunit.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
