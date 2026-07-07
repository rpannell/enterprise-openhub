using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

/// <summary>
/// Replaces the real JWT bearer handler in <see cref="ProductManagerApiFactory"/> — builds the
/// request's <see cref="ClaimsPrincipal"/> straight from <see cref="TestAuthClaimsProvider"/>
/// instead of validating a real Keycloak-issued token. Lets tests exercise the real
/// TenantMiddleware / [Authorize] / AdminScope-policy pipeline without a live Keycloak.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TestAuthClaimsProvider claimsProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (claimsProvider.Claims is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(claimsProvider.Claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
