using System.Security.Claims;

namespace OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

/// <summary>
/// Mutable holder for the claims <see cref="TestAuthHandler"/> issues on the next request —
/// set this before each <see cref="System.Net.Http.HttpClient"/> call to control what
/// "authenticated user" the request pipeline (TenantMiddleware, [Authorize], AdminScope policy)
/// sees. Null means unauthenticated.
/// </summary>
public sealed class TestAuthClaimsProvider
{
    public IReadOnlyCollection<Claim>? Claims { get; set; }
}
