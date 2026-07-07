# OpenHub Product Manager Seed Tool

Bulk-inserts realistic-looking `catalog.products` rows directly via Postgres binary `COPY`, for
load-testing downstream processing (e.g. `OpenHub.ProductManager.ElasticProcessor`'s sync path, or
raw query/index-size testing) without going through `ProductManager.Api`.

By default this only inserts rows. Pass `--publish-events` to also publish a `ProductChangedEvent`
per row to the `product.changed` RabbitMQ fanout exchange, so a running `ElasticProcessor` picks
them up and indexes them into Elasticsearch — same as a real product create, just skipping the API.

## What "realistic" means here

Each product is drawn from a fixed catalog of ~75 real-sounding items (real names, real
descriptions, real weight ranges) spread across 15 of the leaf categories already seeded into
`ref.categories` by Flyway (Power Tools, Hand Tools, Fasteners, Computers & Tablets, Networking,
Lumber & Composites, Roofing, Flooring, etc.) — see `ProductCatalogData.Templates` in `Program.cs`
for the full list. Names get a randomized adjective prefix (`Heavy-Duty`, `Professional`, `Pro`, ...)
about half the time for extra variety at scale. Beyond the name/description, each row also gets:

- A manufacturer + brand pair from real brand-line names (DeWalt/DeWalt XR, Milwaukee/Milwaukee
  M18, Makita, Bosch, Fluke, Siemens, etc. — the same manufacturer/brand data used by
  `tests/data/generate-products.ps1`), created for the tenant on first run and reused after
- A category assignment (`catalog.product_categories`, `is_primary = true`)
- A condition (`ref.condition_types` — mostly `NEW`, some `OPEN_BOX`/`REFURBISHED`)
- An availability status (`ref.availability_statuses` — mostly `IN_STOCK`, with a realistic tail of
  `LOW_STOCK`/`OUT_OF_STOCK`/`BACKORDERED`/`PRE_ORDER`/`DISCONTINUED`)
- A weight (in `LB`) sampled from the template's realistic range, and a manufacturer part number
  like `DEWALT-48213`
- A 12-digit UPC (`catalog.product_identifiers`)
- Retail and wholesale prices in USD (`catalog.product_prices`)
- Color, material, and warranty-length attributes (`catalog.product_attributes`)

SKUs look like `{CATEGORYCODE}-{8 hex chars}` (e.g. `PWRTL-A5A2D219`) rather than an obvious `SEED-`
marker, so the catalog reads like real data rather than test fixtures.

**`description` vs `short_description`:** the ~75 templates only give ~75 distinct core sentences,
which read as obvious copy-paste at 50k+ rows if used verbatim. `short_description` gets that core
template sentence as-is (concise, category-accurate). `description` gets that same sentence *plus*
a second sentence built from the row's actual brand/color/material (e.g. "Made by DeWalt, sold under
the DeWalt XR line, in black with a steel build."), a warranty clause when `warrantyMonths > 0`, and
a condition clause when the condition isn't `NEW` — so `description` varies per row even when many
rows share the same base template.

This supersedes `tests/data/generate-products.ps1` + `products-20k.csv` (a fixed 20k-row CSV
generator with no loader) for most purposes — this tool generates *and* loads directly into
Postgres at any volume up to 500k, using the same real manufacturer/brand/category data.

## Importing from a CSV instead of generating

Pass `--csv-file <path>` to import real rows from a CSV instead of generating synthetic ones.
Expected columns: `sku,name,description,price,price_currency,brand,manufacturer,condition,
manufacturer_part_number,weight`. `--count` becomes optional in this mode and just caps how many
rows to import (omit it to import the whole file).

```powershell
dotnet run -- --csv-file ../../product_catalog_500k.csv --tenant-id 3fa85f64-5717-4562-b3fc-2c963f66afa6
```

The tool does a first pass over the file to collect distinct manufacturer names, `(brand,
manufacturer)` pairs, condition labels, and currency codes, provisions `catalog.manufacturers` /
`catalog.brands` rows for those combinations (a brand gets one row per manufacturer it appears
under, so `product.brand_id` and `product.manufacturer_id` always agree), then streams the file a
second time in 5,000-row `COPY` batches for `catalog.products` and a matching `catalog.product_prices`
(`RETAIL`) row per product. Condition text must match a `ref.condition_types.name` (e.g. `New`,
`Open Box`, `Refurbished`, `Used`); currency codes must match `ref.currencies.code`.

This mode does **not** populate `short_description`, `catalog.product_categories`,
`catalog.product_identifiers`, or `catalog.product_attributes` — the CSV doesn't carry that data,
so nothing is fabricated to fill the gap. `weight_uom_id` defaults to `LB` and `base_uom_id` to `EA`
since the CSV doesn't specify units either.

## Usage

```powershell
cd src/OpenHub.ProductManager.SeedTool
dotnet run -- --count 500000
```

Seeds into a freshly-created throwaway tenant (and a `Seed Bot` user) by default, and prints the
generated `--tenant-id` so you can reuse it on a later run. It also ensures a small pool of 10
manufacturers and 10 brands exists for that tenant (idempotent — reused across runs):

```powershell
dotnet run -- --count 10000 --tenant-id 3fa85f64-5717-4562-b3fc-2c963f66afa6
```

To also drive the ElasticProcessor pipeline:

```powershell
dotnet run -- --count 1000 --publish-events
```

| Option | Default | Notes |
|---|---|---|
| `--count <n>` | *(required)* | Rows to insert, 1-500000. |
| `--tenant-id <guid>` | *(creates a new tenant)* | Seed into an existing tenant instead. |
| `--batch-size <n>` | 25000 | Rows per `COPY` batch — controls progress-reporting granularity. |
| `--connection-string <cs>` | `ConnectionStrings__Catalog` env var, else local Aspire dev default | Npgsql connection string. |
| `--publish-events` | off | Also publish a `ProductChangedEvent` per row to RabbitMQ. |
| `--rabbitmq-connection-string <cs>` | `ConnectionStrings__RabbitMq` env var, else local Aspire dev default | AMQP connection string, only used with `--publish-events`. |

Since seeded products don't carry an obvious marker, the easiest way to remove a batch is by the
tenant it was seeded into (the throwaway-tenant path makes this trivial — just delete that tenant's
data). Run these as a single `psql` call — running the same cleanup twice concurrently against a
large tenant can lock-contend on the same rows and stall both runs:

```sql
DELETE FROM catalog.product_attributes WHERE tenant_id = '<tenant-id>';
DELETE FROM catalog.product_prices WHERE tenant_id = '<tenant-id>';
DELETE FROM catalog.product_identifiers WHERE tenant_id = '<tenant-id>';
DELETE FROM catalog.product_categories WHERE tenant_id = '<tenant-id>';
DELETE FROM catalog.products WHERE tenant_id = '<tenant-id>';
DELETE FROM catalog.brands WHERE tenant_id = '<tenant-id>';
DELETE FROM catalog.manufacturers WHERE tenant_id = '<tenant-id>';
DELETE FROM catalog.users WHERE tenant_id = '<tenant-id>';
DELETE FROM catalog.tenants WHERE id = '<tenant-id>';
```

## Prerequisites

Postgres must already have the `catalog`/`ref` schemas migrated (Flyway, via the AppHost or run
standalone — see `Database/ProductManager/`), including the `ref.categories`/`ref.condition_types`/
`ref.availability_statuses`/`ref.units_of_measure` seed data from `V1_6__seed_ref_data.sql`. Run via
the Aspire AppHost first at least once so migrations have applied, then point this tool at the same
database. If using `--publish-events`, RabbitMQ must be reachable too (the tool declares the
`product.changed` exchange itself if missing).
