using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Managers;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Tests.Unit.Managers;

public class CatalogManagerTests
{
    private static CatalogManager CreateManager(ICatalogLookupAccessor accessor, Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = tenantId;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        return new CatalogManager(accessor, new LookupMapper(), httpContextAccessor);
    }

    [Fact]
    public async Task CreateManufacturerAsync_CallsAccessorWithTenantId_ReturnsMappedDto()
    {
        var accessor = Substitute.For<ICatalogLookupAccessor>();
        var tenantId = Guid.NewGuid();
        var entity = new ManufacturerEntity { Id = Guid.NewGuid(), Name = "Acme", TenantId = tenantId, IsActive = true };
        accessor.InsertManufacturerAsync("Acme", tenantId, Arg.Any<CancellationToken>()).Returns(entity);
        var manager = CreateManager(accessor, tenantId);

        var result = await manager.CreateManufacturerAsync(new CreateManufacturerRequest { Name = "Acme" });

        result.Id.ShouldBe(entity.Id);
        result.Name.ShouldBe("Acme");
        await accessor.Received(1).InsertManufacturerAsync("Acme", tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBrandAsync_CallsAccessorWithTenantId_ReturnsMappedDto()
    {
        var accessor = Substitute.For<ICatalogLookupAccessor>();
        var tenantId = Guid.NewGuid();
        var entity = new BrandEntity { Id = Guid.NewGuid(), Name = "AcmeBrand", TenantId = tenantId, IsActive = true };
        accessor.InsertBrandAsync("AcmeBrand", tenantId, Arg.Any<CancellationToken>()).Returns(entity);
        var manager = CreateManager(accessor, tenantId);

        var result = await manager.CreateBrandAsync(new CreateBrandRequest { Name = "AcmeBrand" });

        result.Id.ShouldBe(entity.Id);
        result.Name.ShouldBe("AcmeBrand");
        await accessor.Received(1).InsertBrandAsync("AcmeBrand", tenantId, Arg.Any<CancellationToken>());
    }
}
