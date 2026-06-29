using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenHub.ProductManager.Api.Accessors;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Engines;
using OpenHub.ProductManager.Api.Engines.Interfaces;
using OpenHub.ProductManager.Api.Managers;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Controllers;
using OpenHub.ProductManager.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Database ─────────────────────────────────────────────────────────────────
builder.AddNpgsqlDbContext<CatalogDbContext>("catalog");

// ── Caching (HybridCache: L1 in-process + L2 Redis) ─────────────────────────
builder.AddRedisDistributedCache("redis");
builder.Services.AddHybridCache();

// ── Authentication — multi-realm Keycloak ────────────────────────────────────
// Each client has its own realm. The iss claim identifies which realm issued the token.
// We resolve the authority dynamically so any registered realm is accepted.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Token is null) return Task.CompletedTask;

                try
                {
                    var handler = new JsonWebTokenHandler();
                    var jwt = handler.ReadJsonWebToken(ctx.Token);
                    var iss = jwt.Issuer;
                    if (!string.IsNullOrEmpty(iss))
                    {
                        ctx.Options.Authority = iss;
                        ctx.Options.TokenValidationParameters.ValidIssuer = iss;
                    }
                }
                catch
                {
                    // Malformed token — let the normal pipeline reject it
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── iDesign layer registrations ───────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// Accessors
builder.Services.AddScoped<ITenantAccessor, TenantAccessor>();
builder.Services.AddScoped<ILookupCacheAccessor, LookupCacheAccessor>();
builder.Services.AddScoped<IProductAccessor, ProductAccessor>();
builder.Services.AddScoped<IProductPriceAccessor, ProductPriceAccessor>();

// Engines — transient because they are stateless pure-logic classes
builder.Services.AddTransient<IProductEngine, ProductEngine>();
builder.Services.AddTransient<IProductPricingEngine, ProductPricingEngine>();

// Managers
builder.Services.AddScoped<IProductManager, ProductManager>();

// Mappers (Mapperly source-generated, no DI overhead)
builder.Services.AddSingleton<ProductMapper>();
builder.Services.AddSingleton<TenantMapper>();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

// Resolves tenant UUID from JWT iss claim and stores it in HttpContext.Items["TenantId"]
app.UseMiddleware<TenantMiddleware>();

app.MapDefaultEndpoints();

// ── Routes ────────────────────────────────────────────────────────────────────
app.MapProductEndpoints();

app.Run();
