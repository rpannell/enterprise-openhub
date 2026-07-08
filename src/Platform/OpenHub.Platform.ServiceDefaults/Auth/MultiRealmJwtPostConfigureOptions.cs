using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Hosting;

internal sealed class MultiRealmJwtPostConfigureOptions(MultiRealmKeyCache cache)
    : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        options.TokenValidationParameters.IssuerSigningKeyResolver =
            (_, securityToken, _, _) => cache.GetSigningKeys(securityToken.Issuer);

        // Returning the issuer signals acceptance. Signature validation already proved
        // the token was signed by the keys fetched from that issuer's discovery endpoint.
        options.TokenValidationParameters.IssuerValidator =
            (issuer, _, _) => issuer;
    }
}
