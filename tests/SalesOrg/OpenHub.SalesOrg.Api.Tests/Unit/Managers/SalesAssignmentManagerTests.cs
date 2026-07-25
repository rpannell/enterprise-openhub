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
// coverage, since no manager tests previously existed for SalesAssignmentManager.
[Trait("Category", "Unit")]
public class SalesAssignmentManagerTests
{
    private readonly ISalesAssignmentAccessor _accessor = Substitute.For<ISalesAssignmentAccessor>();
    private readonly ISalesPersonAccessor _personAccessor = Substitute.For<ISalesPersonAccessor>();
    private readonly ISalesOrganizationUnitAccessor _orgUnitAccessor = Substitute.For<ISalesOrganizationUnitAccessor>();
    private readonly ISalesRoleAccessor _roleAccessor = Substitute.For<ISalesRoleAccessor>();
    private readonly IMessagePublisher _messagePublisher = Substitute.For<IMessagePublisher>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _salesPersonId = Guid.NewGuid();
    private readonly Guid _orgUnitId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();

    private SalesAssignmentManager CreateManager(string? exchangeName = "salesassignment.changed")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = _tenantId;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        _personAccessor.GetByIdAsync(_salesPersonId, Arg.Any<CancellationToken>())
            .Returns(new SalesPersonEntity { Id = _salesPersonId, FirstName = "Jane", LastName = "Rep", Email = "jane@example.com" });
        _orgUnitAccessor.GetByIdAsync(_orgUnitId, Arg.Any<CancellationToken>())
            .Returns(new SalesOrganizationUnitEntity { Id = _orgUnitId, Name = "Northeast", OrganizationUnitType = "region" });
        _roleAccessor.GetByIdAsync(_roleId, Arg.Any<CancellationToken>())
            .Returns(new SalesRoleEntity { Id = _roleId, Name = "Territory Manager" });

        return new SalesAssignmentManager(
            _accessor, _personAccessor, _orgUnitAccessor, _roleAccessor, new SalesAssignmentMapper(), httpContextAccessor,
            _messagePublisher, Options.Create(new SalesAssignmentEventOptions { ExchangeName = exchangeName }));
    }

    private SalesAssignmentEntity MakeEntity(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = _tenantId,
        SalesPersonId = _salesPersonId,
        OrganizationUnitId = _orgUnitId,
        RoleId = _roleId,
        EffectiveStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        PrimaryAssignmentFlag = true,
        ActiveStatus = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private CreateSalesAssignmentRequest MakeCreateRequest() => new()
    {
        SalesPersonId = _salesPersonId,
        OrganizationUnitId = _orgUnitId,
        RoleId = _roleId,
        EffectiveStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
    };

    [Fact]
    public async Task CreateAsync_HappyPath_PublishesSalesAssignmentChangedEvent()
    {
        var manager = CreateManager();
        var created = MakeEntity();
        _accessor.CreateAsync(Arg.Any<SalesAssignmentEntity>(), Arg.Any<CancellationToken>()).Returns(created);

        await manager.CreateAsync(MakeCreateRequest(), Guid.NewGuid());

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesAssignmentChangedEvent>(e =>
                e.SalesAssignmentId == created.Id && e.TenantId == _tenantId &&
                e.SalesPersonId == _salesPersonId && e.OrganizationUnitId == _orgUnitId && e.IsDeleted == false),
            "salesassignment.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ExchangeNameNotConfigured_DoesNotPublish()
    {
        var manager = CreateManager(exchangeName: "");
        var created = MakeEntity();
        _accessor.CreateAsync(Arg.Any<SalesAssignmentEntity>(), Arg.Any<CancellationToken>()).Returns(created);

        await manager.CreateAsync(MakeCreateRequest(), Guid.NewGuid());

        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesAssignmentChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NotFound_DoesNotPublish()
    {
        _accessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SalesAssignmentEntity?)null);
        var manager = CreateManager();

        var result = await manager.UpdateAsync(Guid.NewGuid(), new UpdateSalesAssignmentRequest(), Guid.NewGuid());

        result.ShouldBeNull();
        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesAssignmentChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Found_PublishesSalesAssignmentChangedEvent()
    {
        var existing = MakeEntity();
        _accessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _accessor.UpdateAsync(Arg.Any<SalesAssignmentEntity>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<SalesAssignmentEntity>());
        var manager = CreateManager();

        await manager.UpdateAsync(existing.Id, new UpdateSalesAssignmentRequest { ActiveStatus = false }, Guid.NewGuid());

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<SalesAssignmentChangedEvent>(e => e.SalesAssignmentId == existing.Id && e.IsDeleted == false),
            "salesassignment.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NotFound_DoesNotPublish()
    {
        _accessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SalesAssignmentEntity?)null);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldBeFalse();
        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<SalesAssignmentChangedEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
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
            Arg.Is<SalesAssignmentChangedEvent>(e => e.SalesAssignmentId == existing.Id && e.IsDeleted == true),
            "salesassignment.changed", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
