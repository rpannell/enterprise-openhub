using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Managers;
using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Tests.Unit.Managers;

public class TenantManagerTests
{
    [Fact]
    public async Task ListActiveTenantsAsync_DelegatesToAccessor_ReturnsItsResultUnchanged()
    {
        var accessor = Substitute.For<ITenantAccessor>();
        var tenants = new List<TenantDto> { new() { Id = Guid.NewGuid(), Slug = "client-a", Name = "Client A", PlanTier = "standard", IsActive = true } };
        accessor.ListActiveAsync(Arg.Any<CancellationToken>()).Returns(tenants);
        var manager = new TenantManager(accessor);

        var result = await manager.ListActiveTenantsAsync();

        result.ShouldBeSameAs(tenants);
    }

    [Fact]
    public async Task SyncTenantAsync_DelegatesToAccessor_WithDtoFieldsUnpacked()
    {
        var accessor = Substitute.For<ITenantAccessor>();
        var manager = new TenantManager(accessor);
        var dto = new TenantSyncDto { TenantId = Guid.NewGuid(), RealmSlug = "client-a", Name = "Client A", IsDeleted = false };

        await manager.SyncTenantAsync(dto);

        await accessor.Received(1).SyncTenantAsync(dto.TenantId, dto.RealmSlug, dto.Name, dto.IsDeleted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserAsync_DelegatesToAccessor_WithDtoFieldsUnpacked()
    {
        var accessor = Substitute.For<ITenantAccessor>();
        var manager = new TenantManager(accessor);
        var dto = new UserSyncDto
        {
            TenantId = Guid.NewGuid(), UniqueId = "kc-user-1", Email = "a@example.com",
            FirstName = "Ada", LastName = "Lovelace", IsDeleted = false,
        };

        await manager.SyncUserAsync(dto);

        await accessor.Received(1).SyncUserAsync(
            dto.TenantId, dto.UniqueId, dto.Email, dto.FirstName, dto.LastName, dto.IsDeleted, Arg.Any<CancellationToken>());
    }
}
