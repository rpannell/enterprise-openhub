# iDesign Architecture Scaffold & Validator

Scaffold or validate iDesign-compliant code for the OpenHub platform.

## Layer Rules

| Layer | Folder | Model suffix | Rule |
|---|---|---|---|
| Controllers | `Controllers/` | `XYZ` | Calls Managers only. All IDs are `Guid`. Route constraint: `{id:guid}` |
| Managers | `Managers/` | `XYZDto` | Orchestrates: calls Accessors to fetch data, passes it into Engines. **Never calls other Managers** |
| Engines | `Engines/` | `XYZDto` | **Pure logic only.** All data arrives as method parameters. **Never calls Accessors or Managers.** No constructor dependencies on infrastructure. Fully unit-testable with no mocks |
| Accessors | `Accessors/` | `XYZEntity` | All external I/O: EF Core (DB), HybridCache (Redis), other APIs |

**Mapping:** Mapperly (`Riok.Mapperly`) only. No AutoMapper. No manual property assignment.

**Naming conventions:**
- SQL identifiers: `snake_case` (handled automatically by `UseSnakeCaseNamingConvention()`)
- C# properties: `PascalCase`
- Interfaces: `IXxxManager`, `IXxxEngine`, `IXxxAccessor`

## Scaffold Mode

When the user types `/idesign scaffold <EntityName>`, generate all layers for that entity:

### Files to create

```
Controllers/
  <Entity>Controller.cs          ← MapGet/MapPost/etc., calls I<Entity>Manager, uses Guid IDs

Managers/
  Interfaces/
    I<Entity>Manager.cs
  <Entity>Manager.cs             ← calls Accessors, passes data to Engines

Engines/
  Interfaces/
    I<Entity>Engine.cs
  <Entity>Engine.cs              ← pure methods, all inputs as parameters

Accessors/
  Interfaces/
    I<Entity>Accessor.cs
  <Entity>Accessor.cs            ← EF Core only, no ref joins
  Data/Configuration/
    <Entity>Configuration.cs     ← IEntityTypeConfiguration<T>

Models/
  Requests/
    Create<Entity>Request.cs
    Update<Entity>Request.cs
  Responses/
    <Entity>Response.cs
  Dtos/
    <Entity>Dto.cs
  Entities/
    <Entity>Entity.cs            ← maps to Flyway schema table

Mappers/
  <Entity>Mapper.cs              ← Mapperly [Mapper] partial class
```

### Controller template

```csharp
// All IDs: Guid. No integer IDs anywhere.
app.MapGet("/products/{id:guid}", async (Guid id, IProductManager mgr) =>
{
    var dto = await mgr.GetByIdAsync(id);
    return dto is null ? Results.NotFound() : Results.Ok(mapper.ToResponse(dto));
});
```

### Manager template

```csharp
public class ProductManager(
    IProductAccessor productAccessor,
    ILookupCacheAccessor lookupCache,
    IProductEngine productEngine,
    ProductMapper mapper) : IProductManager
{
    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        // 1. Fetch raw entity (no joins)
        var entity = await productAccessor.GetByIdAsync(id);
        if (entity is null) return null;

        // 2. Fetch lookup dictionaries from cache
        var manufacturers = await lookupCache.GetManufacturersAsync();
        var brands = await lookupCache.GetBrandsAsync();

        // 3. Pass data to Engine — Engine has no I/O of its own
        return productEngine.Hydrate(mapper.ToDto(entity), manufacturers, brands);
    }
}
```

### Engine template

```csharp
public class ProductEngine : IProductEngine
{
    // Pure function — no constructor dependencies on infrastructure
    public ProductDto Hydrate(
        ProductDto dto,
        IReadOnlyDictionary<Guid, ManufacturerDto> manufacturers,
        IReadOnlyDictionary<Guid, BrandDto> brands)
    {
        if (dto.ManufacturerId.HasValue)
            dto.ManufacturerName = manufacturers.GetValueOrDefault(dto.ManufacturerId.Value)?.Name;
        if (dto.BrandId.HasValue)
            dto.BrandName = brands.GetValueOrDefault(dto.BrandId.Value)?.Name;
        return dto;
    }
}
```

### Accessor template (EF Core, no joins)

```csharp
public class ProductAccessor(CatalogDbContext db, IHttpContextAccessor http) : IProductAccessor
{
    private async Task SetTenantAsync()
    {
        var tenantId = http.HttpContext!.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant not resolved");
        await db.Database.ExecuteSqlRawAsync(
            "SET LOCAL app.current_tenant_id = {0}", tenantId.ToString());
    }

    public async Task<ProductEntity?> GetByIdAsync(Guid id)
    {
        await SetTenantAsync();
        return await db.Products.FirstOrDefaultAsync(p => p.Id == id);
    }
}
```

### Mapperly template

```csharp
[Mapper]
public partial class ProductMapper
{
    public partial ProductDto ToDto(ProductEntity entity);
    public partial ProductEntity ToEntity(ProductDto dto);
    public partial ProductResponse ToResponse(ProductDto dto);
    public partial ProductDto ToDto(CreateProductRequest request);
}
```

### Entity template (matches Flyway snake_case schema)

```csharp
// EF Core maps PascalCase → snake_case automatically via UseSnakeCaseNamingConvention()
public class ProductEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid? ManufacturerId { get; set; }
    public Guid? BrandId { get; set; }
    // ... other columns matching catalog.products schema
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

### Configuration template

```csharp
public class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("products", "catalog");
        builder.HasKey(p => p.Id);
        builder.HasQueryFilter(p => p.DeletedAt == null);
        // NpgsqlRange for sys_period:
        builder.Property(p => p.SysPeriod).HasColumnType("tstzrange");
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

All `ref.*` table data is served from `ILookupCacheAccessor` via HybridCache:
- L1: in-process memory, 5-minute TTL
- L2: Redis, 1-hour TTL
- Never query `ref.*` tables directly in product queries — always use the cache dictionaries
- To invalidate: `await cache.RemoveAsync("ref:<table>")`

Available lookup methods:
- `GetManufacturersAsync()` → `IReadOnlyDictionary<Guid, ManufacturerDto>`
- `GetBrandsAsync()` → `IReadOnlyDictionary<Guid, BrandDto>`
- `GetCategoriesAsync()` → `IReadOnlyDictionary<Guid, CategoryDto>`
- `GetConditionTypesAsync()` → `IReadOnlyDictionary<Guid, ConditionTypeDto>`
- `GetPriceTypesAsync()` → `IReadOnlyDictionary<Guid, PriceTypeDto>`
- `GetAvailabilityStatusesAsync()` → `IReadOnlyDictionary<Guid, AvailabilityStatusDto>`
- `GetUnitsOfMeasureAsync()` → `IReadOnlyDictionary<Guid, UnitOfMeasureDto>`
- `GetCurrenciesAsync()` → `IReadOnlyDictionary<Guid, CurrencyDto>`
- `GetAttributeDefinitionsAsync()` → `IReadOnlyDictionary<Guid, AttributeDefinitionDto>`
