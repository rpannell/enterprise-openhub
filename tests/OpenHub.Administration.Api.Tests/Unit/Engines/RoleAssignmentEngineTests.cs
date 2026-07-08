using OpenHub.Administration.Api.Managers.Engines;
using Shouldly;
using Xunit;

namespace OpenHub.Administration.Api.Tests.Unit.Engines;

public class RoleAssignmentEngineTests
{
    private readonly RoleAssignmentEngine _sut = new();

    [Theory]
    [InlineData("tenant-admin")]
    [InlineData("platform-admin")]
    [InlineData("TENANT-ADMIN")]
    public void Evaluate_ProtectedRole_CallerNotPlatformAdmin_Denied(string roleName)
    {
        var result = _sut.Evaluate(roleName, callerIsPlatformAdmin: false);

        result.Allowed.ShouldBeFalse();
        result.DenyReason.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("tenant-admin")]
    [InlineData("platform-admin")]
    public void Evaluate_ProtectedRole_CallerIsPlatformAdmin_Allowed(string roleName)
    {
        var result = _sut.Evaluate(roleName, callerIsPlatformAdmin: true);

        result.Allowed.ShouldBeTrue();
        result.DenyReason.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evaluate_OrdinaryRole_AlwaysAllowed(bool callerIsPlatformAdmin)
    {
        var result = _sut.Evaluate("product-manager", callerIsPlatformAdmin);

        result.Allowed.ShouldBeTrue();
    }
}
