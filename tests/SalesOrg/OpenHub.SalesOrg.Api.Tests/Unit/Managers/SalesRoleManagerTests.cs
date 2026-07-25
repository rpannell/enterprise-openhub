using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.RabbitMq;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Accessors.Models;
using OpenHub.SalesOrg.Api.Managers;
using OpenHub.SalesOrg.Api.Managers.Options;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Contracts;

namespace OpenHub.SalesOrg.Api.Tests.Unit.Managers;

// Scoped to the publish-on-create/update/delete behavior this change adds — not full CRUD
// coverage, since no manager tests previously existed for SalesRoleManager.
[Trait("Category", "Unit")]
public class SalesRoleManagerTests
{
    private readonly ISalesRoleAccessor _accessor = Substitute.For<ISalesRoleAccessor>();
    private readonly IMessagePublisher _messagePublisher = Substitute.For<IMessagePublisher>();
    private readonly Guid _tenantId = Guid.NewGuid();

    private SalesRoleManager CreateManager(string? exchangeName = "salesrole.changed")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = _tenantId;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        return new SalesRoleManager(
            _accessor, new SalesRoleMapper(), httpContextAccessor, _messagePublisher,
            Options.Create(new SalesRoleEventOptions { ExchangeName = exchangeName }));
    }

    private static SalesRoleEntity MakeEntity(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Territory Manager",
        ActiveStatus = true,
        HierarchyLevel = 1000,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task CreateAsync_HappyPath_PublishesSalesRoleChangedEvent()
    {
        var manager = CreateManager();
        var created = MakeEntity();
        _accessor.CreateAsync(Arg.Any<SalesRoleEntity>(), Arg.Any<CancellationToken>()).Returns(created);
        var request = new CreateSalesRoleRequest { Name = "Territory Manager" };

        await manager.CreateAsync(request);

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesRoleChangedEvent>(e => e.SalesRoleId == created.Id && e.TenantId == _tenantId && e.IsDeleted == false),
            "salesrole.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ExchangeNameNotConfigured_DoesNotPublish()
    {
        var manager = CreateManager(exchangeName: null);
        var created = MakeEntity();
        _accessor.CreateAsync(Arg.Any<SalesRoleEntity>(), Arg.Any<CancellationToken>()).Returns(created);
        var request = new CreateSalesRoleRequest { Name = "Territory Manager" };

        await manager.CreateAsync(request);

        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesRoleChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NotFound_DoesNotPublish()
    {
        _accessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SalesRoleEntity?)null);
        var manager = CreateManager();

        var result = await manager.UpdateAsync(Guid.NewGuid(), new UpdateSalesRoleRequest());

        result.ShouldBeNull();
        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesRoleChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Found_PublishesSalesRoleChangedEvent()
    {
        var existing = MakeEntity();
        _accessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _accessor.UpdateAsync(Arg.Any<SalesRoleEntity>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<SalesRoleEntity>());
        var manager = CreateManager();

        await manager.UpdateAsync(existing.Id, new UpdateSalesRoleRequest { Name = "New Name" });

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesRoleChangedEvent>(e => e.SalesRoleId == existing.Id && e.IsDeleted == false),
            "salesrole.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NotFound_DoesNotPublish()
    {
        _accessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SalesRoleEntity?)null);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(Guid.NewGuid());

        result.ShouldBeFalse();
        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesRoleChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Found_SoftDeletes_PublishesEvent_WithIsDeletedTrue()
    {
        var existing = MakeEntity();
        _accessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(existing.Id);

        result.ShouldBeTrue();
        await _accessor.Received(1).SoftDeleteAsync(existing.Id, Arg.Any<CancellationToken>());
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesRoleChangedEvent>(e => e.SalesRoleId == existing.Id && e.IsDeleted == true),
            "salesrole.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
