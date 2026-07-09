using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;
using OpenHub.ProductManager.Contracts;

var options = SeedOptions.Parse(args);
if (options is null)
{
    SeedOptions.PrintUsage();
    return 1;
}

await using var connection = new NpgsqlConnection(options.ConnectionString);
await connection.OpenAsync();

var (tenantId, userId) = await TenantProvisioner.ResolveAsync(connection, options);

await using var eventPublisher = options.PublishEvents
    ? await ProductChangedEventPublisher.ConnectAsync(options.RabbitMqConnectionString)
    : null;
var realmSlug = eventPublisher is not null ? await TenantProvisioner.GetSlugAsync(connection, tenantId) : null;

if (options.CsvFilePath is { } csvFilePath)
{
    await CsvImportPipeline.RunAsync(connection, tenantId, userId, csvFilePath, options.Count, eventPublisher, realmSlug);
    return 0;
}

var brandPool = await ReferenceDataProvisioner.EnsureManufacturersAndBrandsAsync(connection, tenantId);
var refData = await CatalogReferenceData.LoadAsync(connection);

var count = options.Count!.Value;
Console.WriteLine($"Seeding {count:N0} products for tenant {tenantId} (user {userId})...");
if (eventPublisher is not null)
    Console.WriteLine($"Publishing ProductChangedEvent per row to 'product.changed' (realm '{realmSlug}').");

var stopwatch = Stopwatch.StartNew();
var inserted = 0;

while (inserted < count)
{
    var chunkSize = Math.Min(options.BatchSize, count - inserted);
    var rows = await ProductBulkWriter.WriteChunkAsync(connection, tenantId, userId, brandPool, refData, chunkSize);
    await ProductCategoryBulkWriter.WriteChunkAsync(connection, tenantId, rows);
    await ProductIdentifierBulkWriter.WriteChunkAsync(connection, tenantId, rows);
    await ProductPriceBulkWriter.WriteChunkAsync(connection, tenantId, rows, refData);
    await ProductAttributeBulkWriter.WriteChunkAsync(connection, tenantId, rows, refData);
    inserted += chunkSize;

    if (eventPublisher is not null)
        await eventPublisher.PublishAsync(rows.Select(r => r.ProductId).ToArray(), tenantId, userId, realmSlug!);

    Console.WriteLine(
        $"  {inserted:N0}/{count:N0} inserted " +
        $"({inserted / stopwatch.Elapsed.TotalSeconds:N0} rows/sec)");
}

stopwatch.Stop();
Console.WriteLine(
    $"Done. Inserted {inserted:N0} products for tenant {tenantId} in {stopwatch.Elapsed.TotalSeconds:N1}s.");

return 0;

internal sealed record SeedOptions(
    string ConnectionString,
    int? Count,
    int BatchSize,
    Guid? TenantId,
    bool PublishEvents,
    string RabbitMqConnectionString,
    string? CsvFilePath)
{
    public static SeedOptions? Parse(string[] args)
    {
        string? connectionString = null;
        string? rabbitMqConnectionString = null;
        string? csvFilePath = null;
        int? count = null;
        var batchSize = 25_000;
        Guid? tenantId = null;
        var publishEvents = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--count" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out var parsedCount)) return null;
                    count = parsedCount;
                    break;
                case "--tenant-id" when i + 1 < args.Length:
                    if (!Guid.TryParse(args[++i], out var parsedTenantId)) return null;
                    tenantId = parsedTenantId;
                    break;
                case "--batch-size" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out batchSize)) return null;
                    break;
                case "--connection-string" when i + 1 < args.Length:
                    connectionString = args[++i];
                    break;
                case "--publish-events":
                    publishEvents = true;
                    break;
                case "--rabbitmq-connection-string" when i + 1 < args.Length:
                    rabbitMqConnectionString = args[++i];
                    break;
                case "--csv-file" when i + 1 < args.Length:
                    csvFilePath = args[++i];
                    break;
                case "--help":
                case "-h":
                    return null;
            }
        }

        if (csvFilePath is null)
        {
            if (count is null or <= 0 or > 500_000) return null;
        }
        else if (count is not null && count <= 0)
        {
            return null;
        }

        connectionString ??= Environment.GetEnvironmentVariable("ConnectionStrings__Catalog")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=Dev_P@ssw0rd1!;Database=Catalog";

        rabbitMqConnectionString ??= Environment.GetEnvironmentVariable("ConnectionStrings__RabbitMq")
            ?? "amqp://appuser:MyDevPassword123!@localhost:5672/";

        return new SeedOptions(connectionString, count, batchSize, tenantId, publishEvents, rabbitMqConnectionString, csvFilePath);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            OpenHub.ProductManager.SeedTool

            Bulk-inserts realistic-looking catalog.products rows (real category names,
            descriptions, weights, manufacturers/brands, condition, and availability) for
            load-testing the ElasticProcessor sync pipeline and downstream queries.

            By default this only inserts rows — pass --publish-events to also publish a
            ProductChangedEvent per row to the 'product.changed' RabbitMQ fanout exchange,
            so a running ElasticProcessor picks them up and indexes them into Elasticsearch.

            Pass --csv-file to import from a CSV instead of generating synthetic data. Expected
            columns: sku,name,description,price,price_currency,brand,manufacturer,condition,
            manufacturer_part_number,weight. --count optionally caps how many CSV rows to import.

            Usage:
              dotnet run -- --count <n> [--tenant-id <guid>] [--batch-size <n>]
                             [--connection-string <cs>] [--publish-events] [--rabbitmq-connection-string <cs>]
              dotnet run -- --csv-file <path> [--count <n>] [--tenant-id <guid>] [...same options]

            Options:
              --count <n>                        Number of products to insert. Required (1-500000) unless
                                                   --csv-file is set, where it optionally caps the import.
              --tenant-id <guid>                  Existing tenant to seed into. If omitted, a throwaway
                                                   test tenant (and user) is created and its id printed.
              --batch-size <n>                    Rows per COPY batch. Default 25000.
              --connection-string <cs>            Npgsql connection string. Defaults to the env var
                                                   ConnectionStrings__Catalog, then the local Aspire dev
                                                   default (localhost:5432/Catalog, postgres/Dev_P@ssw0rd1!).
              --publish-events                    Also publish a ProductChangedEvent per row to RabbitMQ.
              --rabbitmq-connection-string <cs>   AMQP connection string. Defaults to the env var
                                                   ConnectionStrings__RabbitMq, then the local Aspire dev
                                                   default (amqp://appuser:...@localhost:5672/).
              --csv-file <path>                    Import products from a CSV file instead of generating them.

            Examples:
              dotnet run -- --count 500000
              dotnet run -- --count 10000 --tenant-id 3fa85f64-5717-4562-b3fc-2c963f66afa6
              dotnet run -- --count 1000 --publish-events
              dotnet run -- --csv-file ../../product_catalog_500k.csv
            """);
    }
}

internal static class TenantProvisioner
{
    public static async Task<(Guid TenantId, Guid UserId)> ResolveAsync(NpgsqlConnection connection, SeedOptions options)
    {
        if (options.TenantId is { } existingTenantId)
        {
            await SetTenantContextAsync(connection, existingTenantId);
            var userId = await FindOrCreateUserAsync(connection, existingTenantId);
            return (existingTenantId, userId);
        }

        var tenantId = await CreateThrowawayTenantAsync(connection);
        await SetTenantContextAsync(connection, tenantId);
        var newUserId = await FindOrCreateUserAsync(connection, tenantId);
        Console.WriteLine($"Created throwaway tenant {tenantId} — pass --tenant-id {tenantId} to reuse it.");
        return (tenantId, newUserId);
    }

    public static async Task<string> GetSlugAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var cmd = new NpgsqlCommand("SELECT slug FROM catalog.tenants WHERE id = @tenantId", connection);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task SetTenantContextAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var cmd = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenantId, false)", connection);
        cmd.Parameters.AddWithValue("tenantId", tenantId.ToString());
        await cmd.ExecuteScalarAsync();
    }

    private static async Task<Guid> CreateThrowawayTenantAsync(NpgsqlConnection connection)
    {
        var slug = $"seed-{Guid.NewGuid():N}"[..24];
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO catalog.tenants (id, slug, name, plan_tier, is_active)
            VALUES (gen_random_uuid(), @slug, @name, 'standard', true)
            RETURNING id
            """, connection);
        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("name", $"Seed Test Tenant ({DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC)");
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> FindOrCreateUserAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using (var findCmd = new NpgsqlCommand(
            "SELECT id FROM catalog.users WHERE tenant_id = @tenantId AND is_active AND deleted_at IS NULL LIMIT 1",
            connection))
        {
            findCmd.Parameters.AddWithValue("tenantId", tenantId);
            var existing = await findCmd.ExecuteScalarAsync();
            if (existing is Guid existingId) return existingId;
        }

        await using var createCmd = new NpgsqlCommand(
            """
            INSERT INTO catalog.users (id, tenant_id, unique_id, email, first_name, last_name, is_active)
            VALUES (gen_random_uuid(), @tenantId, @uniqueId, @email, 'Seed', 'Bot', true)
            RETURNING id
            """, connection);
        var uniqueId = $"seed-bot-{Guid.NewGuid():N}";
        createCmd.Parameters.AddWithValue("tenantId", tenantId);
        createCmd.Parameters.AddWithValue("uniqueId", uniqueId);
        createCmd.Parameters.AddWithValue("email", $"{uniqueId}@seed.local");
        return (Guid)(await createCmd.ExecuteScalarAsync())!;
    }
}

internal readonly record struct BrandPoolEntry(
    Guid BrandId, Guid ManufacturerId, string ManufacturerCode, string ManufacturerName, string BrandName);

internal static class ReferenceDataProvisioner
{
    // Real manufacturer/brand-line names (matching tests/data/generate-products.ps1) so
    // seeded catalogs read like real retail data instead of placeholder company names.
    private static readonly (string Name, string Code, string[] BrandLines)[] Manufacturers =
    [
        ("DeWalt", "dewalt", ["DeWalt Pro", "DeWalt XR", "DeWalt Atomic"]),
        ("Milwaukee", "milwaukee", ["Milwaukee M18", "Milwaukee M12", "Milwaukee Fuel"]),
        ("Makita", "makita", ["Makita XGT", "Makita LXT", "Makita CXT"]),
        ("Bosch", "bosch", ["Bosch Professional", "Bosch Core18V", "Bosch Blue"]),
        ("Stanley", "stanley", ["Stanley FatMax", "Stanley Classic", "Stanley Essential"]),
        ("Ridgid", "ridgid", ["Ridgid Pro", "Ridgid Octane", "Ridgid Stealth"]),
        ("Ryobi", "ryobi", ["Ryobi One+", "Ryobi PCL", "Ryobi HP"]),
        ("Craftsman", "craftsman", ["Craftsman V20", "Craftsman Professional", "Craftsman CMCE"]),
        ("Klein Tools", "klein-tools", ["Klein Pro", "Klein Journeyman", "Klein Scout"]),
        ("Fluke", "fluke", ["Fluke 80 Series", "Fluke Industrial", "Fluke Networks"]),
        ("Honeywell", "honeywell", ["Honeywell Home", "Honeywell Pro", "Honeywell Safety"]),
        ("Siemens", "siemens", ["Siemens IQ", "Siemens SENTRON", "Siemens SIMATIC"]),
        ("Schneider", "schneider", ["Schneider Square D", "Schneider Acti9", "Schneider Harmony"]),
        ("Leviton", "leviton", ["Leviton Decora", "Leviton SmartlockPro", "Leviton Renu"])
    ];

    public static async Task<BrandPoolEntry[]> EnsureManufacturersAndBrandsAsync(NpgsqlConnection connection, Guid tenantId)
    {
        var manufacturerIdsByCode = await EnsureManufacturersAsync(connection, tenantId);
        return await EnsureBrandsAsync(connection, tenantId, manufacturerIdsByCode);
    }

    private static async Task<Dictionary<string, Guid>> EnsureManufacturersAsync(NpgsqlConnection connection, Guid tenantId)
    {
        foreach (var (name, code, _) in Manufacturers)
        {
            await using var insertCmd = new NpgsqlCommand(
                """
                INSERT INTO catalog.manufacturers (id, tenant_id, name, code, is_active)
                VALUES (gen_random_uuid(), @tenantId, @name, @code, true)
                ON CONFLICT (tenant_id, code) DO NOTHING
                """, connection);
            insertCmd.Parameters.AddWithValue("tenantId", tenantId);
            insertCmd.Parameters.AddWithValue("name", name);
            insertCmd.Parameters.AddWithValue("code", code);
            await insertCmd.ExecuteNonQueryAsync();
        }

        var codes = Manufacturers.Select(m => m.Code).ToArray();
        await using var selectCmd = new NpgsqlCommand(
            "SELECT code, id FROM catalog.manufacturers WHERE tenant_id = @tenantId AND code = ANY(@codes)", connection);
        selectCmd.Parameters.AddWithValue("tenantId", tenantId);
        selectCmd.Parameters.AddWithValue("codes", codes);

        var result = new Dictionary<string, Guid>();
        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetGuid(1);
        return result;
    }

    private static async Task<BrandPoolEntry[]> EnsureBrandsAsync(
        NpgsqlConnection connection, Guid tenantId, Dictionary<string, Guid> manufacturerIdsByCode)
    {
        var brandCodes = new List<string>();

        foreach (var (_, mfrCode, brandLines) in Manufacturers)
        {
            var manufacturerId = manufacturerIdsByCode[mfrCode];
            foreach (var brandName in brandLines)
            {
                var code = brandName.ToLowerInvariant().Replace(' ', '-').Replace("+", "plus");
                brandCodes.Add(code);

                await using var insertCmd = new NpgsqlCommand(
                    """
                    INSERT INTO catalog.brands (id, tenant_id, manufacturer_id, name, code, is_active)
                    VALUES (gen_random_uuid(), @tenantId, @manufacturerId, @name, @code, true)
                    ON CONFLICT (tenant_id, code) DO NOTHING
                    """, connection);
                insertCmd.Parameters.AddWithValue("tenantId", tenantId);
                insertCmd.Parameters.AddWithValue("manufacturerId", manufacturerId);
                insertCmd.Parameters.AddWithValue("name", brandName);
                insertCmd.Parameters.AddWithValue("code", code);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        await using var selectCmd = new NpgsqlCommand(
            """
            SELECT b.id, b.manufacturer_id, m.code, m.name, b.name
            FROM catalog.brands b
            JOIN catalog.manufacturers m ON m.id = b.manufacturer_id
            WHERE b.tenant_id = @tenantId AND b.code = ANY(@codes)
            """, connection);
        selectCmd.Parameters.AddWithValue("tenantId", tenantId);
        selectCmd.Parameters.AddWithValue("codes", brandCodes.ToArray());

        var entries = new List<BrandPoolEntry>();
        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            entries.Add(new BrandPoolEntry(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        return [.. entries];
    }
}

internal readonly record struct WeightedPicker<T>(T[] Values, double[] CumulativeWeights)
{
    public static WeightedPicker<T> Create((T Value, double Weight)[] options)
    {
        var values = new T[options.Length];
        var cumulative = new double[options.Length];
        var running = 0.0;
        for (var i = 0; i < options.Length; i++)
        {
            running += options[i].Weight;
            values[i] = options[i].Value;
            cumulative[i] = running;
        }
        return new WeightedPicker<T>(values, cumulative);
    }

    public T Pick()
    {
        var roll = Random.Shared.NextDouble() * CumulativeWeights[^1];
        for (var i = 0; i < CumulativeWeights.Length; i++)
            if (roll <= CumulativeWeights[i]) return Values[i];
        return Values[^1];
    }
}

internal readonly record struct ConditionOption(Guid Id, string Label);

internal sealed record CatalogReferenceData(
    Dictionary<string, Guid> CategoryIdBySlug,
    WeightedPicker<ConditionOption> ConditionTypePicker,
    WeightedPicker<Guid> AvailabilityStatusPicker,
    Guid EachUomId,
    Guid PoundUomId,
    Guid ColorAttributeId,
    Guid MaterialAttributeId,
    Guid WarrantyMonthsAttributeId,
    Guid RetailPriceTypeId,
    Guid WholesalePriceTypeId,
    Guid UsdCurrencyId)
{
    public static async Task<CatalogReferenceData> LoadAsync(NpgsqlConnection connection)
    {
        var categorySlugs = ProductCatalogData.Templates.Select(t => t.CategorySlug).Distinct().ToArray();
        var categoryIdBySlug = new Dictionary<string, Guid>();
        await using (var cmd = new NpgsqlCommand("SELECT slug, id FROM ref.categories WHERE slug = ANY(@slugs)", connection))
        {
            cmd.Parameters.AddWithValue("slugs", categorySlugs);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                categoryIdBySlug[reader.GetString(0)] = reader.GetGuid(1);
        }

        var conditionTypes = await LoadCodeIdAndNameAsync(connection, "ref.condition_types", ["NEW", "OPEN_BOX", "REFURBISHED"]);
        var conditionPicker = WeightedPicker<ConditionOption>.Create(
        [
            (new ConditionOption(conditionTypes["NEW"].Id, conditionTypes["NEW"].Name), 0.90),
            (new ConditionOption(conditionTypes["OPEN_BOX"].Id, conditionTypes["OPEN_BOX"].Name), 0.05),
            (new ConditionOption(conditionTypes["REFURBISHED"].Id, conditionTypes["REFURBISHED"].Name), 0.05)
        ]);

        var availabilityIds = await LoadCodeIdsAsync(connection, "ref.availability_statuses",
            ["IN_STOCK", "LOW_STOCK", "OUT_OF_STOCK", "BACKORDERED", "PRE_ORDER", "DISCONTINUED"]);
        var availabilityPicker = WeightedPicker<Guid>.Create(
        [
            (availabilityIds["IN_STOCK"], 0.65),
            (availabilityIds["LOW_STOCK"], 0.15),
            (availabilityIds["OUT_OF_STOCK"], 0.05),
            (availabilityIds["BACKORDERED"], 0.05),
            (availabilityIds["PRE_ORDER"], 0.03),
            (availabilityIds["DISCONTINUED"], 0.07)
        ]);

        var uomIds = await LoadCodeIdsAsync(connection, "ref.units_of_measure", ["EA", "LB"]);
        var attributeIds = await LoadCodeIdsAsync(connection, "ref.attribute_definitions", ["COLOR", "MATERIAL", "WARRANTY_MONTHS"]);
        var priceTypeIds = await LoadCodeIdsAsync(connection, "ref.price_types", ["RETAIL", "WHOLESALE"]);
        var currencyIds = await LoadCodeIdsAsync(connection, "ref.currencies", ["USD"]);

        return new CatalogReferenceData(
            categoryIdBySlug, conditionPicker, availabilityPicker, uomIds["EA"], uomIds["LB"],
            attributeIds["COLOR"], attributeIds["MATERIAL"], attributeIds["WARRANTY_MONTHS"],
            priceTypeIds["RETAIL"], priceTypeIds["WHOLESALE"], currencyIds["USD"]);
    }

    private static async Task<Dictionary<string, Guid>> LoadCodeIdsAsync(NpgsqlConnection connection, string table, string[] codes)
    {
        await using var cmd = new NpgsqlCommand($"SELECT code, id FROM {table} WHERE code = ANY(@codes)", connection);
        cmd.Parameters.AddWithValue("codes", codes);
        var result = new Dictionary<string, Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetGuid(1);
        return result;
    }

    private static async Task<Dictionary<string, (Guid Id, string Name)>> LoadCodeIdAndNameAsync(
        NpgsqlConnection connection, string table, string[] codes)
    {
        await using var cmd = new NpgsqlCommand($"SELECT code, id, name FROM {table} WHERE code = ANY(@codes)", connection);
        cmd.Parameters.AddWithValue("codes", codes);
        var result = new Dictionary<string, (Guid, string)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = (reader.GetGuid(1), reader.GetString(2));
        return result;
    }
}

internal sealed record ProductTemplate(
    string CategorySlug, string CategoryCode, string NamePattern, string[] Variants,
    string Description, decimal MinWeightLb, decimal MaxWeightLb)
{
    // NamePattern has at most one "{0}" placeholder. Templates with no natural spec axis
    // (Variants has a single entry) format to a fixed string, same as before.
    public string FormatName() => string.Format(NamePattern, Variants[Random.Shared.Next(Variants.Length)]);
}

internal static class ProductCatalogData
{
    public static readonly ProductTemplate[] Templates =
    [
        new("computers-tablets", "COMPTB", "15.6 in. Laptop, Core i5, 16GB RAM, {0} SSD", ["256GB", "512GB", "1TB"], "Thin-and-light laptop with a full-HD display, all-day battery life, and a backlit keyboard. Great for work and everyday browsing.", 3, 5),
        new("computers-tablets", "COMPTB", "10.9 in. Tablet, {0}, Wi-Fi", ["32GB", "64GB", "128GB", "256GB"], "Slim tablet with a high-resolution touchscreen, all-day battery, and support for a stylus and keyboard case (sold separately).", 0.8m, 1.2m),
        new("computers-tablets", "COMPTB", "{0} in. 4K Monitor, USB-C", ["24", "27", "32", "34"], "4K UHD monitor with USB-C connectivity for single-cable video, data, and charging to a compatible laptop.", 10, 14),
        new("computers-tablets", "COMPTB", "Mechanical Keyboard, RGB Backlit, {0}", ["Red Switches", "Brown Switches", "Blue Switches"], "Full-size mechanical keyboard with tactile switches, per-key RGB lighting, and a detachable USB-C cable.", 1.5m, 2.5m),
        new("computers-tablets", "COMPTB", "Wireless Mouse, Ergonomic, {0}", ["2.4GHz", "Bluetooth", "Dual-Mode"], "Rechargeable wireless mouse with an ergonomic grip and quiet click buttons for all-day comfort.", 0.2m, 0.4m),
        new("computers-tablets", "COMPTB", "Desktop Tower PC, Ryzen 5, 16GB RAM, {0} SSD", ["512GB", "1TB", "2TB"], "Compact desktop tower with a multi-core processor for everyday computing and light multitasking.", 12, 18),
        new("computers-tablets", "COMPTB", "Portable External SSD, {0}", ["500GB", "1TB", "2TB"], "Pocket-sized external solid-state drive with USB-C connectivity for fast backups and file transfers.", 0.1m, 0.2m),
        new("computers-tablets", "COMPTB", "USB-C Docking Station, {0}-Port", ["8", "11", "14"], "Single-cable docking station that adds display, Ethernet, and USB ports to a compatible laptop.", 0.5m, 0.8m),
        new("computers-tablets", "COMPTB", "Digital Drawing Tablet, {0} in.", ["10", "13", "16"], "Pen-input drawing tablet with pressure sensitivity for digital art and design work.", 0.5m, 1.5m),
        new("computers-tablets", "COMPTB", "{0} in. All-in-One Desktop PC", ["21.5", "24", "27"], "All-in-one desktop with the display, processor, and speakers built into a single unit.", 10, 16),
        new("computers-tablets", "COMPTB", "2-in-1 Convertible Laptop, {0} in., 16GB RAM", ["13", "14", "15.6"], "Convertible laptop with a 360-degree hinge for use as a laptop or touchscreen tablet.", 2.5m, 4),
        new("computers-tablets", "COMPTB", "{0} in. Chromebook, 4GB RAM, 64GB Storage", ["11.6", "14"], "Lightweight Chromebook built for web browsing, streaming, and cloud-based schoolwork.", 2, 3.5m),
        new("computers-tablets", "COMPTB", "{0} in. Gaming Laptop, Discrete Graphics, 32GB RAM", ["15.6", "17.3"], "High-performance gaming laptop with a high-refresh display and dedicated graphics.", 5, 7),

        new("mobile-phones", "MOBILE", "6.1 in. Smartphone, {0}, Unlocked", ["64GB", "128GB", "256GB", "512GB"], "Unlocked smartphone with a 6.1-inch OLED display, dual rear cameras, and all-day battery life.", 0.3m, 0.5m),
        new("mobile-phones", "MOBILE", "Phone Case, Shockproof, {0}", ["iPhone 15", "iPhone 14", "Galaxy S24", "Pixel 8"], "Shockproof protective case with raised edges to guard the screen and camera from drops and scratches.", 0.1m, 0.2m),
        new("mobile-phones", "MOBILE", "Tempered Glass Screen Protector ({0}-Pack)", ["2", "3", "4"], "9H hardness tempered glass screen protector with an oleophobic coating to resist fingerprints.", 0.05m, 0.1m),
        new("mobile-phones", "MOBILE", "Wireless Charging Pad, {0}", ["10W", "15W", "20W"], "Fast wireless charging pad compatible with Qi-enabled phones and earbud cases.", 0.3m, 0.5m),
        new("mobile-phones", "MOBILE", "USB-C to USB-C Cable, {0} ft", ["3", "6", "10"], "Braided USB-C to USB-C cable rated for fast charging and USB 2.0 data transfer speeds.", 0.1m, 0.2m),
        new("mobile-phones", "MOBILE", "{0}mm Smartwatch, GPS", ["41", "45", "49"], "GPS smartwatch with fitness tracking, heart-rate monitoring, and smartphone notifications.", 0.1m, 0.2m),
        new("mobile-phones", "MOBILE", "Magnetic Car Phone Mount, {0}", ["Vent Mount", "Dash Mount", "Windshield Mount"], "Magnetic car mount that holds a phone securely for hands-free navigation and calls.", 0.2m, 0.4m),
        new("mobile-phones", "MOBILE", "Phone Grip and Stand, {0}", ["Black", "Clear", "Marble"], "Collapsible phone grip that doubles as a kickstand for hands-free viewing.", 0.05m, 0.1m),
        new("mobile-phones", "MOBILE", "Phone Tripod Stand, {0} in.", ["12", "24", "50"], "Adjustable tripod with a phone clamp for photos, video calls, and content creation.", 0.4m, 0.8m),
        new("mobile-phones", "MOBILE", "Selfie Ring Light with Phone Clip, {0} in.", ["6", "8", "10"], "LED ring light with adjustable brightness and a clamp for phone-based video and photography.", 0.5m, 1),

        new("networking", "NETWRK", "{0} Wi-Fi 6 Router", ["AX1800", "AX3000", "AX5400", "AX6000"], "Dual-band Wi-Fi 6 router with MU-MIMO and beamforming for faster speeds across more connected devices.", 1, 1.5m),
        new("networking", "NETWRK", "{0}-Port Gigabit Ethernet Switch", ["8", "16", "24", "48"], "Unmanaged Gigabit switch for expanding a wired network in an office or server closet.", 4, 6),
        new("networking", "NETWRK", "Cat6 Ethernet Cable, {0} ft", ["10", "25", "50", "100"], "Snagless Cat6 patch cable rated for Gigabit speeds, suitable for indoor structured cabling runs.", 1, 1.5m),
        new("networking", "NETWRK", "Mesh Wi-Fi System ({0}-Pack)", ["1", "2", "3"], "Whole-home mesh Wi-Fi system for seamless coverage across large homes.", 2, 3),
        new("networking", "NETWRK", "PoE Network Injector, {0}", ["802.3af", "802.3at", "802.3bt"], "Power-over-Ethernet injector adds PoE to a single Ethernet run for cameras and access points.", 0.5m, 0.8m),
        new("networking", "NETWRK", "{0}-Bay Network Attached Storage Enclosure", ["2", "4", "8"], "Network-attached storage enclosure for centralized file storage, backups, and media streaming.", 3, 8),
        new("networking", "NETWRK", "Powerline Network Adapter Kit, {0}", ["500 Mbps", "1200 Mbps", "2000 Mbps"], "Powerline adapter kit that extends a wired network connection over existing electrical wiring.", 0.5m, 0.8m),
        new("networking", "NETWRK", "Wi-Fi Range Extender, {0}", ["AC750", "AC1200", "AX1500"], "Plug-in range extender that boosts Wi-Fi coverage into dead zones.", 0.3m, 0.5m),
        new("networking", "NETWRK", "{0}-Port Patch Panel, Cat6", ["24", "48"], "Rack-mount patch panel for terminating and organizing structured Cat6 cabling runs.", 3, 5),
        new("networking", "NETWRK", "USB Wi-Fi Adapter, {0}", ["AC600", "AC1200", "AX1800"], "USB Wi-Fi adapter that adds wireless connectivity to a desktop PC.", 0.1m, 0.2m),

        new("audio-video", "AUDVID", "Wireless Over-Ear Headphones, {0}", ["Noise Cancelling", "Bass Boost", "Studio Monitor"], "Wireless over-ear headphones with up to 30 hours of battery life and plush memory-foam ear cushions.", 0.6m, 0.9m),
        new("audio-video", "AUDVID", "Bluetooth Soundbar with Subwoofer, {0}", ["2.1 Channel", "3.1 Channel", "5.1 Channel"], "Soundbar with a wireless subwoofer for richer bass and clearer dialogue than built-in TV speakers.", 8, 12),
        new("audio-video", "AUDVID", "4K HDMI Cable, {0} ft", ["3", "6", "10", "15"], "High-speed HDMI cable supporting 4K at 60Hz and HDR for streaming boxes, consoles, and Blu-ray players.", 0.2m, 0.3m),
        new("audio-video", "AUDVID", "True Wireless Earbuds, {0}", ["Noise Cancelling", "Sport", "Standard"], "True wireless earbuds with a compact charging case and up to 24 hours of combined battery life.", 0.1m, 0.2m),
        new("audio-video", "AUDVID", "Studio Monitor Speakers (Pair), {0} in.", ["4", "5", "6.5", "8"], "Powered studio monitor speakers with a flat frequency response for accurate audio mixing and playback.", 10, 15),
        new("audio-video", "AUDVID", "{0} in. Full-Motion TV Wall Mount", ["32-55", "55-75", "65-100"], "Full-motion wall mount that lets a TV tilt, swivel, and extend from the wall.", 8, 15),
        new("audio-video", "AUDVID", "4K Streaming Media Player, {0}", ["HDR", "Dolby Vision", "HDR10+"], "Streaming media player that adds smart-TV apps and 4K playback to any HDMI display.", 0.2m, 0.3m),
        new("audio-video", "AUDVID", "USB Condenser Microphone, {0}", ["Cardioid", "Cardioid + Stand", "Studio Bundle"], "USB condenser microphone for podcasting, streaming, and voiceover recording.", 1, 2),
        new("audio-video", "AUDVID", "{0} Lumen Portable Projector", ["3200", "5000", "7500"], "Portable projector for movies, presentations, and outdoor viewing.", 3, 6),
        new("audio-video", "AUDVID", "Bluetooth Turntable, {0}", ["Belt-Drive", "Direct-Drive"], "Bluetooth-enabled turntable for playing vinyl records through wireless speakers.", 6, 9),

        new("electronics-accessories", "ELACCS", "USB-C Fast Charger, {0}", ["30W", "45W", "65W", "100W"], "Compact GaN charger with a foldable plug, fast enough to charge a laptop or phone.", 0.3m, 0.5m),
        new("electronics-accessories", "ELACCS", "Laptop Sleeve, {0} in.", ["13", "14", "15", "16"], "Padded neoprene laptop sleeve with a zippered closure.", 0.4m, 0.6m),
        new("electronics-accessories", "ELACCS", "Power Strip, {0}-Outlet with Surge Protection", ["4", "6", "8", "12"], "Power strip with surge protection and a 6-foot cord for desks, entertainment centers, and workshops.", 1, 1.5m),
        new("electronics-accessories", "ELACCS", "Portable Power Bank, {0}mAh", ["10000", "20000", "26800"], "High-capacity power bank with dual USB ports for charging phones and tablets on the go.", 0.8m, 1.1m),
        new("electronics-accessories", "ELACCS", "Webcam, {0} with Microphone", ["720p", "1080p", "4K"], "USB webcam with a built-in microphone and privacy shutter for video calls and streaming.", 0.2m, 0.3m),
        new("electronics-accessories", "ELACCS", "Cable Management Box, {0} in.", ["13", "17"], "Cable management box that hides and organizes power strips and excess cords.", 0.5m, 0.8m),
        new("electronics-accessories", "ELACCS", "{0}-Outlet Surge Protector Tower", ["8", "10", "12"], "Tower-style surge protector with multiple outlets and USB charging ports.", 1, 1.5m),
        new("electronics-accessories", "ELACCS", "Adjustable Laptop Stand, {0}", ["Aluminum", "Foldable", "Ergonomic"], "Adjustable laptop stand that raises the screen to a more ergonomic viewing height.", 1, 2),
        new("electronics-accessories", "ELACCS", "Single Monitor Desk Mount Arm, up to {0} in.", ["27", "32", "43"], "Gas-spring monitor arm that clamps to a desk for adjustable height and viewing angle.", 3, 5),

        new("power-tools", "PWRTL", "{0}V MAX Cordless Drill/Driver Kit", ["12", "18", "20"], "Compact brushless drill with a 2-speed transmission and LED work light. Includes battery, charger, and case.", 3, 4.5m),
        new("power-tools", "PWRTL", "{0} in. Circular Saw", ["6-1/2", "7-1/4", "8-1/4"], "Circular saw with a laser guide for straight, accurate cuts through lumber and plywood.", 8, 10),
        new("power-tools", "PWRTL", "{0}-Gauge Brad Nailer", ["16", "18", "23"], "Pneumatic brad nailer for trim, molding, and cabinetry work.", 2, 3),
        new("power-tools", "PWRTL", "{0} in. Impact Wrench", ["1/4", "3/8", "1/2", "3/4"], "High-torque impact wrench for automotive and heavy-equipment work.", 4, 6),
        new("power-tools", "PWRTL", "{0} in. Angle Grinder", ["4-1/2", "5", "7"], "Angle grinder for cutting, grinding, and polishing metal and masonry surfaces.", 3, 4),
        new("power-tools", "PWRTL", "{0}-Amp Reciprocating Saw", ["10", "12"], "Reciprocating saw for demolition, remodeling, and pruning work.", 6, 8),
        new("power-tools", "PWRTL", "{0}V MAX Oscillating Multi-Tool", ["12", "20"], "Oscillating multi-tool for cutting, sanding, and scraping in tight spaces.", 2, 3),
        new("power-tools", "PWRTL", "{0} in. Belt Sander", ["3x18", "3x21", "4x24"], "Belt sander for fast material removal on wood and metal surfaces.", 6, 9),
        new("power-tools", "PWRTL", "{0}-Amp Variable Speed Jigsaw", ["5", "6.5"], "Variable-speed jigsaw for curved and straight cuts in wood, metal, and plastic.", 4, 6),
        new("power-tools", "PWRTL", "{0} in. Portable Table Saw", ["8-1/4", "10"], "Portable table saw with a folding stand for jobsite ripping and crosscutting.", 40, 55),
        new("power-tools", "PWRTL", "{0}-Gal Portable Air Compressor", ["6", "20", "30"], "Portable air compressor for powering nailers, staplers, and inflation tasks.", 25, 60),
        new("power-tools", "PWRTL", "Corded Rotary Tool Kit, {0}", ["Standard", "Deluxe"], "Variable-speed rotary tool kit with accessories for cutting, grinding, and polishing.", 1, 2),

        new("hand-tools", "HNDTL", "{0} oz Fiberglass Claw Hammer", ["16", "20", "22"], "Fiberglass-handled claw hammer with a shock-absorbing grip and a magnetic nail starter.", 1, 1.5m),
        new("hand-tools", "HNDTL", "{0} in. Adjustable Wrench", ["6", "8", "10", "12"], "Chrome-vanadium adjustable wrench with a smooth-adjusting worm gear for a precise fit on fasteners.", 0.5m, 0.8m),
        new("hand-tools", "HNDTL", "{0}-in-1 Screwdriver Set", ["6", "12", "20"], "Multi-bit screwdriver with interchangeable Phillips and flathead tips stored in the handle.", 0.4m, 0.6m),
        new("hand-tools", "HNDTL", "{0} ft. Tape Measure", ["16", "25", "35"], "Tape measure with a wide, standout blade and a belt clip for one-person layout and measuring.", 0.5m, 0.7m),
        new("hand-tools", "HNDTL", "{0} in. Slip Joint Pliers", ["6", "8", "10"], "Slip-joint pliers with a wide jaw capacity for gripping pipes, nuts, and fasteners.", 0.6m, 0.9m),
        new("hand-tools", "HNDTL", "{0}-Piece Mechanics Tool Set", ["150", "192", "230"], "Mechanics tool set with sockets, wrenches, and drive tools in a storage case.", 15, 25),
        new("hand-tools", "HNDTL", "Retractable Utility Knife, {0}", ["Standard", "Auto-Load"], "Retractable utility knife with a quick-change blade mechanism.", 0.2m, 0.3m),
        new("hand-tools", "HNDTL", "{0} in. Flat Pry Bar", ["12", "18", "36"], "Flat pry bar for demolition, prying, and nail-pulling tasks.", 1, 3),
        new("hand-tools", "HNDTL", "{0} in. Bolt Cutters", ["14", "24", "30"], "Bolt cutters for cutting chain, wire, and bolts up to hardened-steel grade.", 2, 6),
        new("hand-tools", "HNDTL", "{0}-Piece File Set", ["5", "8"], "Hand file set for shaping, smoothing, and deburring metal and wood.", 0.5m, 1),

        new("fasteners", "FSTNR", "{0} in. Deck Screws (1 lb Box)", ["2", "2-1/2", "3"], "Coated exterior deck screws with a self-drilling tip for fastening deck boards to joists.", 1, 1.2m),
        new("fasteners", "FSTNR", "{0}-20 Hex Bolts (25-Pack)", ["1/4", "5/16", "3/8"], "Zinc-plated hex bolts for general-purpose fastening in wood, metal, and machinery.", 0.5m, 0.8m),
        new("fasteners", "FSTNR", "Wood Screws, #8 x {0} in. (100-Pack)", ["1", "1-1/2", "2", "2-1/2"], "Coarse-thread wood screws with a bugle head for cabinetry, framing, and general woodworking.", 0.6m, 0.9m),
        new("fasteners", "FSTNR", "Toggle Bolts, {0} in. (10-Pack)", ["1/8", "3/16", "1/4"], "Spring-wing toggle bolts for hanging shelves and fixtures on hollow drywall.", 0.3m, 0.5m),
        new("fasteners", "FSTNR", "Concrete Anchors, {0} in. (20-Pack)", ["1/4", "3/8", "1/2"], "Wedge anchors for fastening ledger boards, railings, and fixtures directly into concrete or masonry.", 0.8m, 1.1m),
        new("fasteners", "FSTNR", "{0}D Common Nails (5 lb Box)", ["8", "10", "16"], "Common nails for general framing and construction work.", 4, 6),
        new("fasteners", "FSTNR", "Machine Screws, {0}-32 (50-Pack)", ["#8", "#10", "1/4"], "Machine screws for fastening into tapped holes or with a nut.", 0.3m, 0.5m),
        new("fasteners", "FSTNR", "{0} in. Flat Washers (50-Pack)", ["1/4", "5/16", "3/8"], "Flat washers for distributing load under a bolt head or nut.", 0.2m, 0.4m),
        new("fasteners", "FSTNR", "{0} in. Cable Zip Ties (100-Pack)", ["8", "11", "14"], "Nylon zip ties for bundling cables and securing lightweight items.", 0.3m, 0.5m),
        new("fasteners", "FSTNR", "Plastic Wall Anchors, {0} in. (20-Pack)", ["1/4", "3/8"], "Plastic wall anchors for hanging light fixtures into drywall.", 0.2m, 0.3m),

        new("measuring-layout", "MEASR", "{0} in. Aluminum Level", ["24", "48", "72"], "I-beam aluminum level with three vials for plumb, level, and 45-degree readings.", 2, 3),
        new("measuring-layout", "MEASR", "{0} in. Combination Square", ["6", "12"], "Combination square with a sliding, lockable head for marking 90- and 45-degree angles.", 0.7m, 1),
        new("measuring-layout", "MEASR", "Laser Distance Measurer, {0} ft Range", ["100", "165", "265"], "Handheld laser measure with stored-measurement memory.", 0.2m, 0.4m),
        new("measuring-layout", "MEASR", "Chalk Line Reel with {0} Chalk", ["Blue", "Red"], "Chalk line reel for snapping straight reference lines across framing, flooring, and drywall.", 0.4m, 0.6m),
        new("measuring-layout", "MEASR", "Digital Angle Finder, {0}", ["Standard", "Extended Reach"], "Digital protractor with a magnetic base for measuring and setting precise angles.", 0.3m, 0.5m),
        new("measuring-layout", "MEASR", "Electronic Stud Finder, {0}", ["Standard", "Deep Scan"], "Electronic stud finder that detects studs, joists, and live wiring behind walls.", 0.3m, 0.5m),
        new("measuring-layout", "MEASR", "{0} in. Speed Square", ["7", "12"], "Speed square for marking rafter cuts and checking angles on the jobsite.", 0.3m, 0.6m),
        new("measuring-layout", "MEASR", "{0} in. Steel Framing Square", ["16x24", "24x16"], "Steel framing square for laying out stairs, rafters, and right angles.", 1, 2),
        new("measuring-layout", "MEASR", "Pinless Moisture Meter, {0}", ["Wood", "Wood/Drywall"], "Pinless moisture meter for checking wood and building materials for excess moisture.", 0.3m, 0.5m),

        new("storage-organization", "STORG", "{0}-Gal Storage Tote with Lid", ["18", "27", "45"], "Heavy-duty storage tote with a snap-on lid, stackable design for a garage, attic, or workshop.", 3, 4),
        new("storage-organization", "STORG", "Wall-Mount Tool Organizer Panel, {0} ft", ["2", "4"], "Slotted wall panel with hooks and bins for organizing hand tools within easy reach.", 5, 7),
        new("storage-organization", "STORG", "{0}-Drawer Parts Cabinet", ["3", "5", "10"], "Small-parts storage cabinet for organizing fasteners, fittings, and hardware.", 4, 6),
        new("storage-organization", "STORG", "Stackable Bin Storage Rack ({0}-Bin)", ["6", "12", "24"], "Rack with stackable bins for sorting small hardware by size and type.", 6, 8),
        new("storage-organization", "STORG", "Rolling Tool Bag with Wheels, {0} in.", ["14", "18", "22"], "Wheeled tool bag with reinforced sides and multiple pockets for transporting tools to the jobsite.", 3, 5),
        new("storage-organization", "STORG", "{0} ft Pegboard Panel with Hooks", ["2", "4"], "Pegboard panel with an assorted hook kit for organizing tools within reach.", 4, 7),
        new("storage-organization", "STORG", "{0}-Compartment Small Parts Organizer", ["24", "39", "60"], "Compartmented storage case for sorting screws, fasteners, and small hardware.", 1, 2),
        new("storage-organization", "STORG", "{0}-Shelf Wire Storage Rack", ["4", "5"], "Adjustable wire shelving unit for garage, pantry, or workshop storage.", 15, 25),
        new("storage-organization", "STORG", "{0} in. Rolling Tool Chest", ["26", "41", "52"], "Rolling tool chest with multiple drawers for organizing a full tool collection.", 40, 90),
        new("storage-organization", "STORG", "Bucket Tool Organizer, {0} Pockets", ["24", "52"], "Fabric organizer that fits over a standard bucket to hold hand tools and small parts.", 1, 2),

        new("lumber-composites", "LUMBR", "2x4x{0} ft Kiln-Dried Lumber", ["8", "10", "12"], "Kiln-dried spruce-pine-fir stud for framing walls, partitions, and general construction.", 8, 10),
        new("lumber-composites", "LUMBR", "{0} in. Plywood Sheet, 4x8 ft", ["1/2", "5/8", "3/4"], "Sanded plywood sheet suitable for subflooring, shelving, and general carpentry projects.", 60, 70),
        new("lumber-composites", "LUMBR", "Composite Decking Board, {0} ft", ["8", "12", "16"], "Capped composite decking board that resists fading, staining, and scratching without the upkeep of wood.", 25, 30),
        new("lumber-composites", "LUMBR", "2x6x{0} ft Pressure-Treated Lumber", ["8", "10", "12"], "Pressure-treated lumber rated for ground contact, suitable for deck framing and outdoor structures.", 18, 22),
        new("lumber-composites", "LUMBR", "{0} in. OSB Sheathing, 4x8 ft", ["7/16", "1/2", "5/8"], "Oriented strand board sheathing for wall and roof sheathing applications.", 45, 55),
        new("lumber-composites", "LUMBR", "{0} in. MDF Board, 4x8 ft", ["1/2", "3/4"], "Smooth, uniform MDF board for cabinetry, shelving, and painted trim work.", 55, 65),
        new("lumber-composites", "LUMBR", "1x6x{0} ft Cedar Board", ["6", "8"], "Cedar board for fencing, siding, and outdoor projects with natural rot resistance.", 5, 8),
        new("lumber-composites", "LUMBR", "{0} in. Hardwood Plywood, 4x8 ft", ["1/4", "1/2"], "Hardwood-faced plywood for furniture, cabinetry, and fine woodworking projects.", 30, 40),
        new("lumber-composites", "LUMBR", "{0} ft Wood Fence Picket", ["6", "8"], "Dog-eared wood fence picket for building or repairing a privacy fence.", 3, 5),
        new("lumber-composites", "LUMBR", "{0} ft Landscape Timber", ["4", "8"], "Pressure-treated landscape timber for garden borders and retaining walls.", 15, 25),

        new("concrete-masonry", "CONCR", "{0} lb Concrete Mix Bag", ["50", "60", "80"], "All-purpose concrete mix for footings, slabs, and post setting — just add water.", 50, 80),
        new("concrete-masonry", "CONCR", "Concrete Block, {0} in.", ["8x8x16", "4x8x16", "6x8x16"], "Standard concrete masonry unit for foundation walls, retaining walls, and general construction.", 30, 35),
        new("concrete-masonry", "CONCR", "Mortar Mix, {0} lb Bag", ["60", "80"], "Type S mortar mix for laying brick, block, and stone in above-grade masonry work.", 60, 80),
        new("concrete-masonry", "CONCR", "Rebar, {0} in. x 10 ft", ["3/8", "1/2", "5/8"], "Deformed steel rebar for reinforcing concrete slabs, footings, and columns.", 5, 7),
        new("concrete-masonry", "CONCR", "Concrete Crack Sealant, {0} oz Tube", ["10", "28"], "Self-leveling sealant for repairing cracks in driveways, sidewalks, and patios.", 0.8m, 1),
        new("concrete-masonry", "CONCR", "{0} in. Concrete Paver", ["12x12", "16x16", "24x24"], "Concrete paver for patios, walkways, and outdoor hardscaping projects.", 15, 40),
        new("concrete-masonry", "CONCR", "{0} lb Stucco Patch", ["20", "40"], "Pre-mixed stucco patch for repairing cracks and holes in exterior stucco walls.", 20, 40),
        new("concrete-masonry", "CONCR", "{0} Gal Concrete Sealer", ["1", "5"], "Penetrating concrete sealer that protects driveways and patios from moisture and stains.", 8, 40),

        new("insulation", "INSUL", "{0} Fiberglass Batt Insulation", ["R-13", "R-19", "R-30"], "Kraft-faced fiberglass batt insulation sized for standard wall or attic cavities.", 15, 20),
        new("insulation", "INSUL", "Rigid Foam Board Insulation, {0} in.", ["1", "2", "3"], "Extruded polystyrene foam board for basement walls, roofs, and under-slab applications.", 3, 5),
        new("insulation", "INSUL", "Spray Foam Insulation Kit, {0} Board Ft", ["200", "600"], "Two-component spray foam kit for sealing gaps, cracks, and small cavities around a home.", 20, 25),
        new("insulation", "INSUL", "Reflective Foil Insulation Roll, {0} ft", ["25", "50"], "Radiant barrier insulation roll that reflects heat for use in attics and crawl spaces.", 8, 10),
        new("insulation", "INSUL", "Weatherstripping Tape, {0} ft Roll", ["10", "17", "25"], "Self-adhesive foam weatherstripping for sealing gaps around doors and windows.", 0.3m, 0.5m),
        new("insulation", "INSUL", "{0} ft Duct Insulation Wrap", ["10", "25"], "Reflective duct wrap that reduces heat loss on HVAC ductwork.", 3, 6),
        new("insulation", "INSUL", "{0} in. Foam Pipe Insulation, 6 ft", ["1/2", "3/4", "1"], "Slit foam pipe insulation that prevents freezing and condensation on plumbing lines.", 0.5m, 1),
        new("insulation", "INSUL", "{0} ft Polyethylene Vapor Barrier", ["10x100", "20x100"], "Polyethylene sheeting used as a vapor barrier under slabs and behind insulation.", 10, 25),
        new("insulation", "INSUL", "Acoustic Foam Panels, {0}-Pack", ["12", "24"], "Acoustic foam panels that reduce echo and improve sound quality in a room.", 2, 5),

        new("roofing", "ROOF", "Architectural Shingles, {0} Bundle", ["Weathered Wood", "Charcoal", "Driftwood"], "Laminated architectural asphalt shingles with a dimensional look and Class A fire rating.", 60, 70),
        new("roofing", "ROOF", "Roll Roofing Felt, {0} lb", ["15", "30"], "Asphalt-saturated felt underlayment for use beneath shingles and other roof coverings.", 40, 50),
        new("roofing", "ROOF", "Ridge Vent, {0} ft Section", ["4", "10"], "Rigid ridge vent for passive attic ventilation along the roof ridge line.", 2, 3),
        new("roofing", "ROOF", "Roofing Nails, {0} in. (5 lb Box)", ["1", "1-1/4", "1-1/2"], "Hot-dip galvanized roofing nails for fastening shingles and underlayment.", 5, 5),
        new("roofing", "ROOF", "Drip Edge Flashing, {0} ft", ["10", "10"], "Aluminum drip edge for directing water away from the fascia and roof deck.", 1.5m, 2.5m),
        new("roofing", "ROOF", "{0} oz Roof and Flashing Cement", ["10.3", "29"], "Trowel-grade roof cement for sealing flashing, vents, and minor roof repairs.", 2, 5),
        new("roofing", "ROOF", "{0} ft Gutter Guard", ["4", "5"], "Mesh gutter guard that keeps leaves and debris out of gutters.", 1, 2),
        new("roofing", "ROOF", "{0} in. Fixed Skylight", ["22x22", "22x46"], "Fixed skylight that adds natural daylight to an attic or upper-floor room.", 20, 35),
        new("roofing", "ROOF", "{0} in. Roofing Screws (250-Pack)", ["1-1/2", "2", "3"], "Self-drilling roofing screws with a weather-sealing washer.", 5, 8),
        new("roofing", "ROOF", "{0} in. Chimney Flashing Kit", ["Small", "Large"], "Flashing kit for weatherproofing the joint between a chimney and the roof.", 3, 6),

        new("flooring", "FLOOR", "Laminate Flooring Plank, {0}mm (Carton)", ["7", "8", "12"], "Wood-look laminate flooring with an AC4-rated wear layer for residential and light commercial use.", 25, 30),
        new("flooring", "FLOOR", "Vinyl Plank Flooring, Waterproof, {0}mm (Carton)", ["4", "6", "8"], "Rigid-core waterproof vinyl plank flooring suitable for kitchens, baths, and basements.", 30, 35),
        new("flooring", "FLOOR", "Engineered Hardwood Flooring, {0} (Carton)", ["Oak", "Maple", "Hickory"], "Engineered hardwood flooring with a real hardwood wear layer over a stable plywood core.", 35, 40),
        new("flooring", "FLOOR", "Floor Underlayment Roll, {0}mm", ["2", "3"], "Foam underlayment roll that reduces noise and adds cushioning beneath laminate or vinyl flooring.", 5, 8),
        new("flooring", "FLOOR", "Baseboard Trim, {0} ft", ["8", "12", "16"], "Paint-grade MDF baseboard trim for finishing the transition between wall and floor.", 3, 5),
        new("flooring", "FLOOR", "{0} in. Ceramic Floor Tile", ["12x12", "18x18", "24x24"], "Glazed ceramic floor tile for kitchens, baths, and entryways.", 20, 40),
        new("flooring", "FLOOR", "{0} in. Carpet Tile (Carton)", ["18x18", "24x24"], "Peel-and-stick carpet tile for basements, offices, and playrooms.", 15, 25),
        new("flooring", "FLOOR", "{0} ft Floor Transition Strip", ["3", "6"], "Transition strip for finishing the edge between two different flooring types.", 1, 2),
        new("flooring", "FLOOR", "{0} ft Interlocking Rubber Floor Mat", ["2x2", "3x3"], "Interlocking rubber tile for garage, gym, or workshop flooring.", 5, 10),
        new("flooring", "FLOOR", "{0}mm Cork Underlayment Roll", ["3", "6"], "Cork underlayment roll that adds sound dampening beneath hard flooring.", 3, 6),

        new("plumbing", "PLMB", "{0} Single-Handle Kitchen Faucet", ["Chrome", "Matte Black", "Brushed Nickel"], "Single-handle kitchen faucet with a pull-down sprayer for everyday use.", 4, 7),
        new("plumbing", "PLMB", "{0} Gal Electric Water Heater", ["40", "50", "65"], "Electric water heater for residential hot-water supply.", 90, 140),
        new("plumbing", "PLMB", "PVC Pipe, {0} in. x 10 ft, Schedule 40", ["1/2", "3/4", "1", "2"], "Schedule 40 PVC pipe for drain, waste, and vent plumbing lines.", 2, 8),
        new("plumbing", "PLMB", "{0} Ball Valve, 1/2 in.", ["Brass", "PVC"], "Quarter-turn ball valve for shutting off water supply lines.", 0.3m, 0.6m),
        new("plumbing", "PLMB", "Dual-Flush Toilet, {0}", ["Round Bowl", "Elongated Bowl"], "Water-efficient dual-flush toilet for standard residential installation.", 80, 100),
        new("plumbing", "PLMB", "{0} Rain Shower Head", ["Chrome", "Matte Black", "Brushed Nickel"], "Rain-style shower head with a wide spray pattern.", 1, 2),
        new("plumbing", "PLMB", "{0} ft Drain Auger", ["25", "50"], "Hand-crank drain auger for clearing clogs in sinks and drains.", 3, 6),
        new("plumbing", "PLMB", "Garbage Disposal, {0} HP", ["1/2", "3/4", "1"], "Under-sink garbage disposal for food waste grinding.", 8, 14),

        new("electrical", "ELEC", "{0}-Amp Single-Pole Circuit Breaker", ["15", "20", "30"], "Single-pole circuit breaker for residential load center panels.", 0.3m, 0.5m),
        new("electrical", "ELEC", "{0} ft 12-Gauge Romex Wire", ["25", "50", "100"], "Non-metallic sheathed electrical wire for residential branch circuits.", 5, 20),
        new("electrical", "ELEC", "Decora Wall Switch, {0}", ["Single-Pole", "3-Way", "Dimmer"], "Decora-style wall switch for controlling lights and fixtures.", 0.1m, 0.3m),
        new("electrical", "ELEC", "{0}-Outlet Extension Cord, 25 ft", ["1", "3"], "Heavy-duty extension cord for indoor and outdoor use.", 1, 2),
        new("electrical", "ELEC", "GFCI Outlet, {0}", ["15A", "20A"], "Ground-fault circuit interrupter outlet for wet-location safety.", 0.2m, 0.3m),
        new("electrical", "ELEC", "{0} in. Electrical Conduit, 10 ft", ["1/2", "3/4", "1"], "Rigid electrical conduit for protecting wiring runs.", 2, 5),
        new("electrical", "ELEC", "Wire Nuts, {0} (100-Pack)", ["Red", "Yellow", "Orange"], "Twist-on wire connectors for joining electrical conductors.", 0.2m, 0.4m),
        new("electrical", "ELEC", "{0}-Amp Load Center, 20-Space", ["100", "150", "200"], "Main breaker load center for a residential electrical panel.", 8, 14),

        new("lighting", "LIGHT", "LED Bulb, {0}W Equivalent (4-Pack)", ["60", "75", "100"], "Energy-efficient LED bulb that replaces an equivalent incandescent bulb.", 0.3m, 0.5m),
        new("lighting", "LIGHT", "{0} in. Flush Mount Ceiling Light", ["11", "13", "16"], "Flush-mount ceiling light fixture for bedrooms, hallways, and closets.", 2, 4),
        new("lighting", "LIGHT", "Outdoor Solar Path Lights, {0}-Pack", ["4", "6", "8"], "Solar-powered path lights that charge by day and light up walkways at night.", 1, 2),
        new("lighting", "LIGHT", "{0} ft LED String Lights", ["24", "48"], "Weatherproof LED string lights for patios, decks, and outdoor entertaining.", 1, 3),
        new("lighting", "LIGHT", "Motion-Sensor Security Floodlight, {0}", ["Single-Head", "Dual-Head"], "Motion-activated LED floodlight for driveways and entryways.", 1, 2),
        new("lighting", "LIGHT", "{0} in. Ceiling Fan with Light Kit", ["44", "52", "60"], "Ceiling fan with an integrated light kit and reversible motor.", 10, 18),
        new("lighting", "LIGHT", "Under-Cabinet LED Light Bar, {0} in.", ["12", "18", "24"], "Slim LED light bar for illuminating kitchen countertops.", 0.5m, 1),
        new("lighting", "LIGHT", "Rechargeable LED Work Light, {0} Lumens", ["1000", "2000", "3000"], "Rechargeable work light for jobsites, garages, and emergencies.", 1, 2),

        new("office-supplies", "OFFSUP", "Ergonomic Mesh Office Chair, {0}", ["Black", "Gray"], "Ergonomic office chair with adjustable lumbar support and armrests.", 25, 35),
        new("office-supplies", "OFFSUP", "{0} in. Adjustable Sit-Stand Desk", ["48", "55", "60"], "Electric sit-stand desk with programmable height presets.", 60, 90),
        new("office-supplies", "OFFSUP", "Heavy-Duty Stapler, {0}", ["Standard", "Long-Reach"], "Heavy-duty stapler for high-volume office use.", 1, 2),
        new("office-supplies", "OFFSUP", "Copy Paper, {0} (10-Ream Case)", ["Letter", "Legal"], "Multipurpose copy paper for everyday printing and copying.", 35, 45),
        new("office-supplies", "OFFSUP", "{0}-Drawer Filing Cabinet", ["2", "3", "4"], "Locking filing cabinet for letter- and legal-size documents.", 30, 60),
        new("office-supplies", "OFFSUP", "Dry Erase Whiteboard, {0} ft", ["3x4", "4x6"], "Wall-mounted dry erase whiteboard for offices and meeting rooms.", 8, 15),
        new("office-supplies", "OFFSUP", "Sticky Notes, {0} (12-Pack)", ["3x3 in.", "4x6 in."], "Adhesive-backed sticky notes for reminders and annotations.", 0.5m, 1),
        new("office-supplies", "OFFSUP", "Paper Shredder, {0}-Sheet Capacity", ["8", "12", "16"], "Cross-cut paper shredder for secure document disposal.", 10, 18),

        new("safety-security", "SAFSEC", "{0}-lb ABC Fire Extinguisher", ["2.5", "5", "10"], "Multi-purpose ABC fire extinguisher for home and office use.", 3, 12),
        new("safety-security", "SAFSEC", "Combination Smoke and CO Detector, {0}", ["Battery", "Hardwired"], "Combination smoke and carbon monoxide detector with a loud alarm.", 0.5m, 1),
        new("safety-security", "SAFSEC", "{0}-Gun Fireproof Safe", ["Small", "Medium", "Large"], "Fireproof safe with a digital keypad for firearms and valuables.", 25, 80),
        new("safety-security", "SAFSEC", "Wireless Security Camera, {0}", ["1080p", "2K", "4K"], "Wireless security camera with night vision and motion alerts.", 0.5m, 1),
        new("safety-security", "SAFSEC", "Smart Deadbolt Lock, {0}", ["Keypad", "Fingerprint", "App-Enabled"], "Smart deadbolt lock for keyless entry and remote access control.", 1, 2),
        new("safety-security", "SAFSEC", "Safety Glasses, {0} (Clear Lens)", ["Standard", "Anti-Fog"], "ANSI-rated safety glasses for eye protection on the jobsite.", 0.1m, 0.2m),
        new("safety-security", "SAFSEC", "First Aid Kit, {0}-Piece", ["100", "200", "300"], "Comprehensive first aid kit for home, office, or vehicle.", 1, 3),
        new("safety-security", "SAFSEC", "High-Visibility Safety Vest, {0}", ["Class 2", "Class 3"], "ANSI-rated high-visibility safety vest for roadwork and warehouse use.", 0.3m, 0.5m)
    ];
}

internal static class NameVariety
{
    // Matches the adjective/color/warranty word banks in tests/data/generate-products.ps1.
    public static readonly string[] Adjectives =
        ["Heavy-Duty", "Professional", "Pro", "Premium", "Compact", "Portable", "Commercial",
         "Industrial", "High-Speed", "Precision", "Advanced", "Ergonomic"];

    public static readonly string[] Materials =
        ["Steel", "Aluminum", "Carbon Fiber", "Polycarbonate", "Stainless Steel", "Titanium",
         "Brass", "Nylon", "ABS Plastic", "Cast Iron", "Fiberglass", "Rubber"];

    public static readonly string[] Colors =
        ["Black", "Red", "Blue", "Yellow", "Gray", "Orange", "Green", "Silver", "White", "Titanium"];

    public static readonly int[] WarrantyMonths = [0, 0, 12, 12, 24, 36, 60];

    public static string VaryName(string baseName)
    {
        // ~50% chance of an adjective prefix keeps names varied without every
        // product looking artificially modified.
        return Random.Shared.Next(2) == 0 ? baseName : $"{Adjectives[Random.Shared.Next(Adjectives.Length)]} {baseName}";
    }

    public static string RandomUpc()
    {
        Span<char> digits = stackalloc char[12];
        for (var i = 0; i < digits.Length; i++)
            digits[i] = (char)('0' + Random.Shared.Next(10));
        return new string(digits);
    }

    // Builds a per-row description from the template's core sentence plus brand/color/material/
    // warranty/condition details, so rows sharing a template don't read as identical copy-paste text.
    public static string BuildDescription(
        string coreDescription, BrandPoolEntry brand, string color, string material, int warrantyMonths, string conditionLabel)
    {
        var sentences = new List<string>
        {
            coreDescription,
            $"Made by {brand.ManufacturerName}, sold under the {brand.BrandName} line, in {color.ToLowerInvariant()} with a {material.ToLowerInvariant()} build."
        };

        if (warrantyMonths > 0)
            sentences.Add($"Backed by a {warrantyMonths}-month manufacturer warranty.");

        if (!conditionLabel.Equals("New", StringComparison.OrdinalIgnoreCase))
            sentences.Add($"This unit is offered in {conditionLabel.ToLowerInvariant()} condition.");

        return string.Join(" ", sentences);
    }
}

internal readonly record struct GeneratedProduct(
    Guid ProductId, Guid CategoryId, string Upc, decimal RetailPrice, decimal WholesalePrice,
    string Color, string Material, int WarrantyMonths);

internal static class ProductBulkWriter
{
    public static async Task<GeneratedProduct[]> WriteChunkAsync(
        NpgsqlConnection connection, Guid tenantId, Guid userId, BrandPoolEntry[] brandPool,
        CatalogReferenceData refData, int chunkSize)
    {
        var rows = new GeneratedProduct[chunkSize];

        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY catalog.products (
                id, tenant_id, sku, name, description, short_description, manufacturer_id, brand_id,
                condition_type_id, availability_status_id, base_uom_id, manufacturer_part_number,
                is_active, is_published, weight, weight_uom_id,
                create_user_id, modify_user_id, sys_period, created_at, updated_at
            ) FROM STDIN (FORMAT BINARY)
            """);

        var now = DateTimeOffset.UtcNow;
        var openPeriod = new NpgsqlRange<DateTimeOffset>(now, true, false, default, false, true);
        var templates = ProductCatalogData.Templates;

        for (var i = 0; i < chunkSize; i++)
        {
            var template = templates[Random.Shared.Next(templates.Length)];
            var brand = brandPool[Random.Shared.Next(brandPool.Length)];
            var categoryId = refData.CategoryIdBySlug[template.CategorySlug];
            var productId = Guid.NewGuid();
            var name = NameVariety.VaryName(template.FormatName());
            var condition = refData.ConditionTypePicker.Pick();

            var retailPrice = Math.Round((decimal)Random.Shared.NextDouble() * 999.99m + 0.99m, 2);
            var wholesalePrice = Math.Round(retailPrice * 0.65m, 2);
            var color = NameVariety.Colors[Random.Shared.Next(NameVariety.Colors.Length)];
            var material = NameVariety.Materials[Random.Shared.Next(NameVariety.Materials.Length)];
            var warrantyMonths = NameVariety.WarrantyMonths[Random.Shared.Next(NameVariety.WarrantyMonths.Length)];

            rows[i] = new GeneratedProduct(
                productId, categoryId, NameVariety.RandomUpc(), retailPrice, wholesalePrice, color, material, warrantyMonths);

            // 16 hex chars (64 bits), not 8 — several templates share a CategoryCode (e.g. 13
            // templates all use "COMPTB"), so a popular code alone can draw 50k+ rows out of a
            // 500k run. At 8 hex chars (32 bits) that's a >50% chance of a birthday collision on
            // uq_products_tenant_sku; 16 hex chars makes it negligible at this scale.
            var sku = $"{template.CategoryCode}-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";
            var hyphenIndex = brand.ManufacturerCode.IndexOf('-');
            var mpnPrefix = (hyphenIndex < 0 ? brand.ManufacturerCode : brand.ManufacturerCode[..hyphenIndex]).ToUpperInvariant();
            var mpn = $"{mpnPrefix}-{Random.Shared.Next(10_000, 99_999)}";
            var weight = Math.Round(
                template.MinWeightLb + (decimal)Random.Shared.NextDouble() * (template.MaxWeightLb - template.MinWeightLb), 2);

            var description = NameVariety.BuildDescription(template.Description, brand, color, material, warrantyMonths, condition.Label);

            await writer.StartRowAsync();
            await writer.WriteAsync(productId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(sku, NpgsqlDbType.Text);
            await writer.WriteAsync(name, NpgsqlDbType.Text);
            await writer.WriteAsync(description, NpgsqlDbType.Text);
            await writer.WriteAsync(template.Description, NpgsqlDbType.Text);
            await writer.WriteAsync(brand.ManufacturerId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(brand.BrandId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(condition.Id, NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.AvailabilityStatusPicker.Pick(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.EachUomId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(mpn, NpgsqlDbType.Text);
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);
            await writer.WriteAsync(weight, NpgsqlDbType.Numeric);
            await writer.WriteAsync(refData.PoundUomId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(userId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(userId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(openPeriod, NpgsqlDbType.Range | NpgsqlDbType.TimestampTz);
            await writer.WriteAsync(now, NpgsqlDbType.TimestampTz);
            await writer.WriteAsync(now, NpgsqlDbType.TimestampTz);
        }

        await writer.CompleteAsync();
        return rows;
    }
}

internal static class ProductCategoryBulkWriter
{
    public static async Task WriteChunkAsync(NpgsqlConnection connection, Guid tenantId, GeneratedProduct[] rows)
    {
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY catalog.product_categories (id, tenant_id, product_id, category_id, is_primary)
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var row in rows)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.ProductId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.CategoryId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);
        }

        await writer.CompleteAsync();
    }
}

internal static class ProductIdentifierBulkWriter
{
    public static async Task WriteChunkAsync(NpgsqlConnection connection, Guid tenantId, GeneratedProduct[] rows)
    {
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY catalog.product_identifiers (id, tenant_id, product_id, identifier_type, identifier_value, is_primary)
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var row in rows)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.ProductId, NpgsqlDbType.Uuid);
            await writer.WriteAsync("UPC", NpgsqlDbType.Text);
            await writer.WriteAsync(row.Upc, NpgsqlDbType.Text);
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);
        }

        await writer.CompleteAsync();
    }
}

internal static class ProductPriceBulkWriter
{
    public static async Task WriteChunkAsync(NpgsqlConnection connection, Guid tenantId, GeneratedProduct[] rows, CatalogReferenceData refData)
    {
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY catalog.product_prices (id, tenant_id, product_id, price_type_id, currency_id, amount, is_active)
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var row in rows)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.ProductId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.RetailPriceTypeId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.UsdCurrencyId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.RetailPrice, NpgsqlDbType.Numeric);
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);

            await writer.StartRowAsync();
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.ProductId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.WholesalePriceTypeId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.UsdCurrencyId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.WholesalePrice, NpgsqlDbType.Numeric);
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);
        }

        await writer.CompleteAsync();
    }
}

internal static class ProductAttributeBulkWriter
{
    public static async Task WriteChunkAsync(NpgsqlConnection connection, Guid tenantId, GeneratedProduct[] rows, CatalogReferenceData refData)
    {
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY catalog.product_attributes
                (id, tenant_id, product_id, attribute_definition_id, value_text, value_integer)
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var row in rows)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.ProductId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.ColorAttributeId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.Color, NpgsqlDbType.Text);
            await writer.WriteAsync(DBNull.Value, NpgsqlDbType.Bigint);

            await writer.StartRowAsync();
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.ProductId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.MaterialAttributeId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.Material, NpgsqlDbType.Text);
            await writer.WriteAsync(DBNull.Value, NpgsqlDbType.Bigint);

            await writer.StartRowAsync();
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.ProductId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(refData.WarrantyMonthsAttributeId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(DBNull.Value, NpgsqlDbType.Text);
            await writer.WriteAsync((long)row.WarrantyMonths, NpgsqlDbType.Bigint);
        }

        await writer.CompleteAsync();
    }
}

internal sealed class ProductChangedEventPublisher : IAsyncDisposable
{
    private const string ExchangeName = "product.changed";

    private readonly RabbitMQ.Client.IConnection _connection;
    private readonly RabbitMQ.Client.IChannel _channel;

    private ProductChangedEventPublisher(RabbitMQ.Client.IConnection connection, RabbitMQ.Client.IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<ProductChangedEventPublisher> ConnectAsync(string connectionString)
    {
        var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(connectionString) };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(ExchangeName, RabbitMQ.Client.ExchangeType.Fanout, durable: true, autoDelete: false);
        return new ProductChangedEventPublisher(connection, channel);
    }

    public async Task PublishAsync(IReadOnlyList<Guid> productIds, Guid tenantId, Guid userId, string realmSlug)
    {
        var properties = new RabbitMQ.Client.BasicProperties { ContentType = "application/json", Persistent = true };

        foreach (var productId in productIds)
        {
            var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new ProductChangedEvent
            {
                ProductId = productId,
                TenantId = tenantId,
                UserId = userId,
                RealmSlug = realmSlug,
                IsDeleted = false
            });

            await _channel.BasicPublishAsync(ExchangeName, routingKey: string.Empty, mandatory: false, properties, body);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal readonly record struct CsvProductRow(
    string Sku, string Name, string Description, decimal Price, string PriceCurrency,
    string Brand, string Manufacturer, string Condition, string ManufacturerPartNumber, decimal? Weight)
{
    // File has no quoted fields/embedded commas (verified on the 500k-row import file), so a
    // plain split is safe and far faster than a general-purpose CSV parser at this volume.
    public static CsvProductRow Parse(string line)
    {
        var f = line.Split(',');
        return new CsvProductRow(
            f[0], f[1], f[2],
            decimal.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture),
            f[4], f[5], f[6], f[7], f[8],
            string.IsNullOrEmpty(f[9]) ? null : decimal.Parse(f[9], System.Globalization.CultureInfo.InvariantCulture));
    }
}

internal sealed record CsvReferenceData(
    Dictionary<string, Guid> ManufacturerIdByName,
    Dictionary<(string Brand, string Manufacturer), Guid> BrandIdByPair,
    Dictionary<string, Guid> ConditionIdByName,
    Dictionary<string, Guid> CurrencyIdByCode,
    Guid EachUomId,
    Guid PoundUomId,
    Guid RetailPriceTypeId)
{
    public static async Task<CsvReferenceData> ProvisionAsync(NpgsqlConnection connection, Guid tenantId, string csvFilePath)
    {
        var manufacturerNames = new HashSet<string>();
        var brandPairs = new HashSet<(string, string)>();
        var conditionNames = new HashSet<string>();
        var currencyCodes = new HashSet<string>();

        var isFirstLine = true;
        foreach (var line in File.ReadLines(csvFilePath))
        {
            if (isFirstLine) { isFirstLine = false; continue; }
            var row = CsvProductRow.Parse(line);
            manufacturerNames.Add(row.Manufacturer);
            brandPairs.Add((row.Brand, row.Manufacturer));
            conditionNames.Add(row.Condition);
            currencyCodes.Add(row.PriceCurrency);
        }

        var manufacturerIds = await EnsureManufacturersAsync(connection, tenantId, manufacturerNames);
        var brandIds = await EnsureBrandsAsync(connection, tenantId, brandPairs, manufacturerIds);

        var conditionIds = await LookupByNameAsync(connection, "ref.condition_types", conditionNames);
        var currencyIds = await LookupByCodeAsync(connection, "ref.currencies", currencyCodes);
        var uomIds = await LookupByCodeAsync(connection, "ref.units_of_measure", ["EA", "LB"]);
        var priceTypeIds = await LookupByCodeAsync(connection, "ref.price_types", ["RETAIL"]);

        return new CsvReferenceData(
            manufacturerIds, brandIds, conditionIds, currencyIds, uomIds["EA"], uomIds["LB"], priceTypeIds["RETAIL"]);
    }

    private static async Task<Dictionary<string, Guid>> EnsureManufacturersAsync(
        NpgsqlConnection connection, Guid tenantId, HashSet<string> names)
    {
        foreach (var name in names)
        {
            await using var insertCmd = new NpgsqlCommand(
                """
                INSERT INTO catalog.manufacturers (id, tenant_id, name, code, is_active)
                VALUES (gen_random_uuid(), @tenantId, @name, @code, true)
                ON CONFLICT (tenant_id, code) DO NOTHING
                """, connection);
            insertCmd.Parameters.AddWithValue("tenantId", tenantId);
            insertCmd.Parameters.AddWithValue("name", name);
            insertCmd.Parameters.AddWithValue("code", Slugify(name));
            await insertCmd.ExecuteNonQueryAsync();
        }

        var codes = names.Select(Slugify).ToArray();
        await using var selectCmd = new NpgsqlCommand(
            "SELECT name, id FROM catalog.manufacturers WHERE tenant_id = @tenantId AND code = ANY(@codes)", connection);
        selectCmd.Parameters.AddWithValue("tenantId", tenantId);
        selectCmd.Parameters.AddWithValue("codes", codes);

        var result = new Dictionary<string, Guid>();
        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetGuid(1);
        return result;
    }

    private static async Task<Dictionary<(string, string), Guid>> EnsureBrandsAsync(
        NpgsqlConnection connection, Guid tenantId, HashSet<(string Brand, string Manufacturer)> pairs,
        Dictionary<string, Guid> manufacturerIdByName)
    {
        var codeToPair = new Dictionary<string, (string, string)>();

        foreach (var (brand, manufacturer) in pairs)
        {
            var code = $"{Slugify(brand)}-{Slugify(manufacturer)}";
            codeToPair[code] = (brand, manufacturer);

            await using var insertCmd = new NpgsqlCommand(
                """
                INSERT INTO catalog.brands (id, tenant_id, manufacturer_id, name, code, is_active)
                VALUES (gen_random_uuid(), @tenantId, @manufacturerId, @name, @code, true)
                ON CONFLICT (tenant_id, code) DO NOTHING
                """, connection);
            insertCmd.Parameters.AddWithValue("tenantId", tenantId);
            insertCmd.Parameters.AddWithValue("manufacturerId", manufacturerIdByName[manufacturer]);
            insertCmd.Parameters.AddWithValue("name", brand);
            insertCmd.Parameters.AddWithValue("code", code);
            await insertCmd.ExecuteNonQueryAsync();
        }

        await using var selectCmd = new NpgsqlCommand(
            "SELECT code, id FROM catalog.brands WHERE tenant_id = @tenantId AND code = ANY(@codes)", connection);
        selectCmd.Parameters.AddWithValue("tenantId", tenantId);
        selectCmd.Parameters.AddWithValue("codes", codeToPair.Keys.ToArray());

        var result = new Dictionary<(string, string), Guid>();
        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[codeToPair[reader.GetString(0)]] = reader.GetGuid(1);
        return result;
    }

    private static async Task<Dictionary<string, Guid>> LookupByNameAsync(
        NpgsqlConnection connection, string table, HashSet<string> names)
    {
        await using var cmd = new NpgsqlCommand($"SELECT name, id FROM {table} WHERE name = ANY(@names)", connection);
        cmd.Parameters.AddWithValue("names", names.ToArray());
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetGuid(1);
        return result;
    }

    private static async Task<Dictionary<string, Guid>> LookupByCodeAsync(
        NpgsqlConnection connection, string table, IEnumerable<string> codes)
    {
        await using var cmd = new NpgsqlCommand($"SELECT code, id FROM {table} WHERE code = ANY(@codes)", connection);
        cmd.Parameters.AddWithValue("codes", codes.ToArray());
        var result = new Dictionary<string, Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetGuid(1);
        return result;
    }

    private static string Slugify(string value) =>
        value.ToLowerInvariant().Replace(" ", "-").Replace(",", "");
}

internal static class CsvImportPipeline
{
    public static async Task RunAsync(
        NpgsqlConnection connection, Guid tenantId, Guid userId, string csvFilePath, int? maxCount,
        ProductChangedEventPublisher? eventPublisher, string? realmSlug)
    {
        if (!File.Exists(csvFilePath))
        {
            Console.WriteLine($"CSV file not found: {csvFilePath}");
            return;
        }

        Console.WriteLine($"Scanning {csvFilePath} for manufacturers/brands/conditions/currencies...");
        var refData = await CsvReferenceData.ProvisionAsync(connection, tenantId, csvFilePath);
        Console.WriteLine(
            $"Provisioned {refData.ManufacturerIdByName.Count} manufacturers and {refData.BrandIdByPair.Count} brands for tenant {tenantId}.");

        const int batchSize = 5_000;
        var buffer = new List<CsvProductRow>(batchSize);
        var inserted = 0;
        var stopwatch = Stopwatch.StartNew();

        async Task FlushAsync()
        {
            if (buffer.Count == 0) return;
            var productIds = await CsvProductBulkWriter.WriteChunkAsync(connection, tenantId, userId, buffer, refData);
            inserted += buffer.Count;
            buffer.Clear();

            if (eventPublisher is not null)
                await eventPublisher.PublishAsync(productIds, tenantId, userId, realmSlug!);

            Console.WriteLine($"  {inserted:N0} inserted ({inserted / stopwatch.Elapsed.TotalSeconds:N0} rows/sec)");
        }

        var isFirstLine = true;
        foreach (var line in File.ReadLines(csvFilePath))
        {
            if (isFirstLine) { isFirstLine = false; continue; }
            if (maxCount is { } cap && inserted + buffer.Count >= cap) break;

            buffer.Add(CsvProductRow.Parse(line));
            if (buffer.Count >= batchSize) await FlushAsync();
        }

        await FlushAsync();
        stopwatch.Stop();
        Console.WriteLine($"Done. Imported {inserted:N0} products from CSV for tenant {tenantId} in {stopwatch.Elapsed.TotalSeconds:N1}s.");
    }
}

internal static class CsvProductBulkWriter
{
    public static async Task<Guid[]> WriteChunkAsync(
        NpgsqlConnection connection, Guid tenantId, Guid userId, List<CsvProductRow> rows, CsvReferenceData refData)
    {
        var productIds = new Guid[rows.Count];
        var now = DateTimeOffset.UtcNow;
        var openPeriod = new NpgsqlRange<DateTimeOffset>(now, true, false, default, false, true);

        await using (var writer = await connection.BeginBinaryImportAsync(
            """
            COPY catalog.products (
                id, tenant_id, sku, name, description, manufacturer_id, brand_id,
                condition_type_id, base_uom_id, manufacturer_part_number,
                is_active, is_published, weight, weight_uom_id,
                create_user_id, modify_user_id, sys_period, created_at, updated_at
            ) FROM STDIN (FORMAT BINARY)
            """))
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var productId = Guid.NewGuid();
                productIds[i] = productId;

                await writer.StartRowAsync();
                await writer.WriteAsync(productId, NpgsqlDbType.Uuid);
                await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
                await writer.WriteAsync(row.Sku, NpgsqlDbType.Text);
                await writer.WriteAsync(row.Name, NpgsqlDbType.Text);
                await writer.WriteAsync(row.Description, NpgsqlDbType.Text);
                await writer.WriteAsync(refData.ManufacturerIdByName[row.Manufacturer], NpgsqlDbType.Uuid);
                await writer.WriteAsync(refData.BrandIdByPair[(row.Brand, row.Manufacturer)], NpgsqlDbType.Uuid);
                await writer.WriteAsync(refData.ConditionIdByName[row.Condition], NpgsqlDbType.Uuid);
                await writer.WriteAsync(refData.EachUomId, NpgsqlDbType.Uuid);
                await writer.WriteAsync(row.ManufacturerPartNumber, NpgsqlDbType.Text);
                await writer.WriteAsync(true, NpgsqlDbType.Boolean);
                await writer.WriteAsync(true, NpgsqlDbType.Boolean);
                if (row.Weight is { } weight) await writer.WriteAsync(weight, NpgsqlDbType.Numeric);
                else await writer.WriteNullAsync();
                await writer.WriteAsync(refData.PoundUomId, NpgsqlDbType.Uuid);
                await writer.WriteAsync(userId, NpgsqlDbType.Uuid);
                await writer.WriteAsync(userId, NpgsqlDbType.Uuid);
                await writer.WriteAsync(openPeriod, NpgsqlDbType.Range | NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(now, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(now, NpgsqlDbType.TimestampTz);
            }

            await writer.CompleteAsync();
        }

        await using (var writer = await connection.BeginBinaryImportAsync(
            """
            COPY catalog.product_prices (id, tenant_id, product_id, price_type_id, currency_id, amount, is_active)
            FROM STDIN (FORMAT BINARY)
            """))
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                await writer.StartRowAsync();
                await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
                await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
                await writer.WriteAsync(productIds[i], NpgsqlDbType.Uuid);
                await writer.WriteAsync(refData.RetailPriceTypeId, NpgsqlDbType.Uuid);
                await writer.WriteAsync(refData.CurrencyIdByCode[row.PriceCurrency], NpgsqlDbType.Uuid);
                await writer.WriteAsync(row.Price, NpgsqlDbType.Numeric);
                await writer.WriteAsync(true, NpgsqlDbType.Boolean);
            }

            await writer.CompleteAsync();
        }

        return productIds;
    }
}
