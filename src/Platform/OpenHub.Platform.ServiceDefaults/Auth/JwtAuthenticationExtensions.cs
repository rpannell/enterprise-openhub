using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Hosting;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Registers multi-realm Keycloak JWT authentication for any OpenHub API.
    /// Reads <c>Auth:Audience</c> from configuration (omit to skip audience validation).
    /// Signing keys are fetched and cached per Keycloak realm — no static Authority needed.
    /// In non-production environments the OIDC backchannel trusts self-signed certificates.
    /// </summary>
    public static TBuilder AddOpenHubJwtAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var audience = builder.Configuration["Auth:Audience"];
        var isProduction = builder.Environment.IsProduction();

        HttpMessageHandler httpHandler = isProduction
            ? new HttpClientHandler()
            : new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

        var keyCache = new MultiRealmKeyCache(httpHandler);
        builder.Services.AddSingleton(keyCache);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = isProduction;
                options.MapInboundClaims = false;

                if (!string.IsNullOrWhiteSpace(audience))
                    options.Audience = audience;
                else
                    options.TokenValidationParameters.ValidateAudience = false;

                options.TokenValidationParameters.ValidateIssuer = true;
            });

        builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, MultiRealmJwtPostConfigureOptions>();
        builder.Services.AddAuthorization();

        return builder;
    }
}
