using System.Security.Claims;

namespace OpenHub.RealtimeMessaging.Api.Tests.Integration.Fixtures;

/// <summary>
/// Mutable holder for the claims <see cref="TestAuthHandler"/> issues on the next request —
/// set this before each <see cref="System.Net.Http.HttpClient"/> call to control what
/// "authenticated caller" the request pipeline ([Authorize], RealtimeMessagingPublisher policy)
/// sees. Null means unauthenticated.
/// </summary>
public sealed class TestAuthClaimsProvider
{
    public IReadOnlyCollection<Claim>? Claims { get; set; }
}
