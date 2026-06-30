# iDesign Architecture Scaffold & Validator

Scaffold or validate iDesign-compliant code for the OpenHub platform.

## Layer Rules

| Layer | Folder | Model suffix | Rule |
|---|---|---|---|
| Controllers | `Controllers/` | — | Calls Managers only. All IDs are `Guid`. Route constraint: `{id:guid}` |
| Managers | `Managers/` | `XYZDto` | Orchestrates: calls Accessors to fetch data, passes it into Engines. **Never calls other Managers** |
| Engines | `Managers/Engines/` | `XYZDto` | **Pure logic only.** All data arrives as method parameters. **Never calls Accessors or Managers.** No constructor dependencies on infrastructure. Fully unit-testable with no mocks |
| Accessors | `Accessors/` | `XYZEntity` | All external I/O: EF Core (DB), HybridCache (Redis), other APIs |

## Model Locations (by layer)

Each layer owns its models — self-contained so the layer can be extracted to its own DLL:

| Layer | Model location | Namespace |
|---|---|---|
| Controllers | `Models/Requests/`, `Models/Responses/V1/` | `...Models.Requests`, `...Models.Responses.V1` |
| Managers + Engines | `Managers/Models/` | `...Managers.Models` |
| Accessors | `Accessors/Models/` | `...Accessors.Models` |

**Mapping:** Mapperly (`Riok.Mapperly`) only. No AutoMapper. No manual property assignment.

## API Versioning

**All controllers must be versioned starting at V1.** Use `Asp.Versioning.Http`:

```csharp
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .ReportApiVersions()
    .Build();

var group = app.MapGroup("/api/v{version:apiVersion}/products")
    .WithApiVersionSet(versionSet)
    .RequireAuthorization();
```

**Response models must be versioned** so adding V2 never breaks V1 consumers:
- `Models/Responses/V1/ProductResponse.cs` — namespace `...Models.Responses.V1`
- When adding V2: create `Models/Responses/V2/ProductResponse.cs` — never modify V1

Requests do **not** need versioning (input changes are handled via optional fields).

## Naming Conventions

- SQL identifiers: `snake_case` (handled automatically by `UseSnakeCaseNamingConvention()`)
- C# properties: `PascalCase`
- Interfaces: `IXxxManager`, `IXxxEngine`, `IXxxAccessor`

## Scaffold Mode

When the user types `/idesign scaffold <EntityName>`, generate all layers for that entity.
After scaffolding, apply `/platform` rules: OpenAPI docs, validation filters, and response codes.

### Files to create

```
Controllers/
  <Entity>Endpoints.cs           ← MapGet/MapPost/etc., calls I<Entity>Manager, uses Guid IDs

Models/                          ← Controller layer only
  Requests/
    Create<Entity>Request.cs
    Update<Entity>Request.cs
  Responses/V1/
    <Entity>Response.cs

Managers/
  Interfaces/
    I<Entity>Manager.cs
  Models/                        ← Manager + Engine layer (XxxDto)
    <Entity>Dto.cs
  Engines/
    Interfaces/
      I<Entity>Engine.cs
    <Entity>Engine.cs
  <Entity>Manager.cs

Accessors/
  Interfaces/
    I<Entity>Accessor.cs
  Models/
    <Entity>Entity.cs            ← maps to Flyway schema table
  Data/
    Configuration/
      <Entity>Configuration.cs
  <Entity>Accessor.cs

Mappers/
  <Entity>Mapper.cs              ← Mapperly [Mapper] partial class; cross-layer mappings only
```

**Note:** `CatalogDbContext.cs` is updated (add `DbSet<Entity>`), not re-created.

### Controller template

```csharp
// All IDs: Guid. No integer IDs anywhere.
// Apply /platform OpenAPI rules: WithName, WithSummary, WithDescription, Produces<T>
group.MapGet("/{id:guid}", async (Guid id, I<Entity>Manager manager, <Entity>Mapper mapper, CancellationToken ct) =>
{
    var dto = await manager.GetByIdAsync(id, ct);
    return dto is null ? Results.NotFound() : Results.Ok(mapper.ToResponse(dto));
});
```

### Manager template

```csharp
public class <Entity>Manager(
    I<Entity>Accessor accessor,
    ILookupCacheAccessor lookupCache,
    I<Entity>Engine engine,
    <Entity>Mapper mapper) : I<Entity>Manager
{
    public async Task<<Entity>Dto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // 1. Fetch raw entity (no joins)
        var entity = await accessor.GetByIdAsync(id, ct);
        if (entity is null) return null;

        // 2. Fetch lookup dictionaries from cache
        var manufacturers = await lookupCache.GetManufacturersAsync(ct);

        // 3. Pass data to Engine — Engine has no I/O of its own
        return engine.Hydrate(mapper.ToDto(entity), manufacturers);
    }
}
```

### Engine template

```csharp
public class <Entity>Engine : I<Entity>Engine
{
    // Pure function — no constructor dependencies on infrastructure
    public <Entity>Dto Hydrate(
        <Entity>Dto dto,
        IReadOnlyDictionary<Guid, ManufacturerDto> manufacturers)
    {
        if (dto.ManufacturerId.HasValue)
            dto.ManufacturerName = manufacturers.GetValueOrDefault(dto.ManufacturerId.Value)?.Name;
        return dto;
    }
}
```

### Accessor template (EF Core, no joins)

```csharp
public class <Entity>Accessor(CatalogDbContext db, IHttpContextAccessor http) : I<Entity>Accessor
{
    private async Task SetTenantAsync()
    {
        var tenantId = http.HttpContext!.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant not resolved");
        await db.Database.ExecuteSqlRawAsync(
            "SET LOCAL app.current_tenant_id = {0}", tenantId.ToString());
    }

    public async Task<<Entity>Entity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await SetTenantAsync();
        return await db.<Entities>.FirstOrDefaultAsync(e => e.Id == id, ct);
    }
}
```

### Mapperly template

```csharp
[Mapper]
public partial class <Entity>Mapper
{
    public partial <Entity>Dto ToDto(<Entity>Entity entity);
    public partial <Entity>Response ToResponse(<Entity>Dto dto);
    public partial <Entity>Dto ToDto(Create<Entity>Request request);
}
```

### Entity template (matches Flyway snake_case schema)

```csharp
// EF Core maps PascalCase → snake_case automatically via UseSnakeCaseNamingConvention()
public class <Entity>Entity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

### EF Configuration template

```csharp
public class <Entity>Configuration : IEntityTypeConfiguration<<Entity>Entity>
{
    public void Configure(EntityTypeBuilder<<Entity>Entity> builder)
    {
        builder.ToTable("<table>", "catalog");
        builder.HasKey(e => e.Id);
        builder.HasQueryFilter(e => e.DeletedAt == null);
    }
}
```

## Validate Mode

When the user types `/idesign validate`, scan for violations:

1. **Engine injecting Accessor or Manager** — check Engine constructor parameters for `IXxxAccessor` or `IXxxManager`
2. **Manager calling another Manager** — check Manager method bodies for calls to `IXxxManager` dependencies
3. **AutoMapper usage** — grep for `IMapper`, `MapperConfiguration`, `CreateMap`
4. **Integer IDs in API** — check Controllers for `int id` route params
5. **Manual mapping** — check for `new XxxDto { Prop = src.Prop }` patterns outside Mappers/
6. **EF Core joins in Accessors** — check for `.Include()` or `.Join()` calls on product/catalog queries
7. **Migrations code** — check for `Database.Migrate()` or `EnsureCreated()` calls

Report each violation with file path and line number.

## Lookup Cache Pattern

Platform-wide `ref.*` table data (shared across all tenants) is served from `ILookupCacheAccessor` via HybridCache:
- L1: in-process memory, 5-minute TTL
- L2: Redis, 1-hour TTL
- Never query `ref.*` tables directly in domain queries — always use the cache dictionaries
- To invalidate: `await cache.RemoveAsync("ref:<table>")`

Available lookup methods (dictionaries, keyed by `Guid` id — used for hydrating other entities):
- `GetConditionTypeNamesAsync()` → `IReadOnlyDictionary<Guid, string>`
- `GetAvailabilityStatusNamesAsync()` → `IReadOnlyDictionary<Guid, string>`
- `GetPriceTypeNamesAsync()` → `IReadOnlyDictionary<Guid, string>`
- `GetCurrencyCodesAsync()` → `IReadOnlyDictionary<Guid, string>`
- `GetUomCodesAsync()` → `IReadOnlyDictionary<Guid, string>`

List-shaped variants for lookup/picklist endpoints (e.g. `GET /api/v1/lookups`):
- `GetConditionTypesAsync()`, `GetAvailabilityStatusesAsync()`, `GetPriceTypesAsync()` → `IReadOnlyList<LookupItemRow>`
- `GetCurrenciesAsync()` → `IReadOnlyList<CurrencyLookupRow>`
- `GetUnitsOfMeasureAsync()` → `IReadOnlyList<UomLookupRow>`

### Tenant-specific lookups

Data that each tenant manages independently (e.g. `catalog.manufacturers`, `catalog.brands`) does **not** belong in `ILookupCacheAccessor` — it lives in `catalog.*` tables with RLS, not `ref.*`. Use a dedicated accessor instead, e.g. `ICatalogLookupAccessor`:
- `GetManufacturersAsync()` → `IReadOnlyList<ManufacturerEntity>`
- `GetBrandsAsync()` → `IReadOnlyList<BrandEntity>`

Same HybridCache pattern, but cache keys are tenant-scoped: `catalog:<table>:tenant:{tenantId}` (vs. the global `ref:<table>` / `ref:<table>:list` keys). Tenant id is read from `HttpContext.Items["TenantId"]` via `IHttpContextAccessor`, and the same `SET LOCAL app.current_tenant_id` RLS pattern applies.

## DI Registration Pattern

Each layer registers its own services via a static `Add<Layer>()` extension method, kept in a `Setup/` subfolder so it doesn't clutter the layer's implementation files:

```
Accessors/Setup/AccessorServiceExtensions.cs   → AddAccessors()
Managers/Setup/ManagerServiceExtensions.cs     → AddManagers()
Managers/Engines/Setup/EngineServiceExtensions.cs → AddEngines()
Mappers/Setup/MapperServiceExtensions.cs       → AddMappers()
```

Namespace matches folder (e.g. `...Accessors.Setup`). `Program.cs` only calls these four methods — it never registers a layer's services directly:

```csharp
builder.Services.AddAccessors();
builder.Services.AddEngines();
builder.Services.AddManagers();
builder.Services.AddMappers();
```

**Not a service-factory pattern** — these are plain `IServiceCollection` extension methods using standard `AddScoped`/`AddTransient`/`AddSingleton` calls, the same idiom as `AddControllers()` or `AddAuthorization()`. This keeps each layer self-contained (consistent with the "Model Locations" section above) so a layer's DI wiring travels with it if it's ever extracted to its own DLL.

When scaffolding a new entity, add its registrations to the relevant `Add<Layer>()` method instead of touching `Program.cs`.
