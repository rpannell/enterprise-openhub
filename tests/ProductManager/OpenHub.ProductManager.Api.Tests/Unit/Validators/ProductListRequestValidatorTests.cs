using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Tests.Unit.Validators;

public class ProductListRequestValidatorTests
{
    private readonly ProductListRequestValidator _validator = new();

    [Fact]
    public void Validate_DefaultRequest_IsValid()
    {
        var result = _validator.Validate(new ProductListRequest());

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageBelowOne_IsInvalid(int page)
    {
        var result = _validator.Validate(new ProductListRequest { Page = page });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ProductListRequest.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_PageSizeOutsideRange_IsInvalid(int pageSize)
    {
        var result = _validator.Validate(new ProductListRequest { PageSize = pageSize });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ProductListRequest.PageSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Validate_PageSizeAtBoundaries_IsValid(int pageSize)
    {
        var result = _validator.Validate(new ProductListRequest { PageSize = pageSize });

        result.IsValid.ShouldBeTrue();
    }
}
