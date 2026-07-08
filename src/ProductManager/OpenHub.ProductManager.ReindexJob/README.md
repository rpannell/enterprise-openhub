# OpenHub Product Manager Reindex Job

A one-shot batch job that rebuilds the shared Elasticsearch product index from scratch,
tenant by tenant, and cuts each tenant over to the new index as soon as that tenant's data
is fully written. Intended to run on a schedule via a Kubernetes CronJob (or manually via
`docker run`) — it runs to completion once and exits (0 on success, non-zero if any tenant
failed), unlike every other service in this repo.

## Why this exists, and why it doesn't reuse the RabbitMQ trickle feed

`OpenHub.ProductManager.ElasticProcessor` keeps the index up to date incrementally: one
`product.changed` message at a time, each triggering one HTTP fetch and one single-document
Elasticsearch write. That's the right shape for near-real-time updates, but the wrong shape
for a full-catalog rebuild — dumping every product onto that queue would mean one HTTP call
and one non-bulk Elasticsearch write per product, and would also queue real trickle updates
behind the whole backfill on the same exchange.

This job instead talks to Elasticsearch directly via `OpenHub.Accessor.ElasticIndex`'s
bulk/versioned-index API:

1. Creates a new, timestamped index (e.g. `products-20260707020000`).
2. Calls `GET /api/v1/admin/tenants` (platform-admin only) to enumerate every active tenant.
3. Per tenant, pages through `GET /api/v1/products` and bulk-writes documents into the new
   index.
4. As soon as a tenant's data is fully written, repoints that tenant's alias
   (`products-tenant-{tenantId}`) to the new index — independently of other tenants, so one
   large or slow tenant doesn't hold up everyone else's cutover.
5. Once every tenant in the run has succeeded, deletes any index left with no remaining
   alias reference.

Because the trickle-feed consumer already writes through the tenant's alias rather than a
hardcoded index name (see `OpenHub.Accessor.ElasticIndex`'s README), it needs no changes and
no awareness of which index is "current" — it keeps working correctly before, during, and
after a tenant's cutover.

## Known limitation: mid-run product changes

Each tenant's page-through is effectively a snapshot. A product changed after this job reads
its page but before that tenant's cutover may be transiently missing from the new index until
the next scheduled run, or until an unrelated trickle update to that same product re-writes it
(which lands correctly in the new index once cutover has happened). This is accepted as a
"good enough" gap for a scheduled job, not solved here — a stronger fix (a high-water-mark
catch-up pass) is a possible future improvement, not implemented.

## Required Keycloak setup

This job needs two kinds of Keycloak tokens:

- **Platform-admin M2M token** — to call `GET /api/v1/admin/tenants`. See
  `OpenHub.ProductManager.Api`'s README, "Platform-admin authentication" section, for how to
  provision the `platform-admin` realm/client. Configure its secret at
  `KeyCloak:M2M:ClientSecrets:platform-admin`.
- **Per-tenant M2M tokens** — to page through each tenant's own `GET /api/v1/products`, using
  the same per-tenant M2M setup already documented in `OpenHub.ProductManager.Api`'s README
  ("Keycloak configuration for M2M per tenant"). Configure each tenant's secret at
  `KeyCloak:M2M:ClientSecrets:{realm-slug}`.

## Configuration reference

| Key | Description |
|---|---|
| `ReindexJob:PageSize` | Rows requested per `GET /api/v1/products` page call. Default 200. |
| `ReindexJob:BatchSize` | Documents per Elasticsearch bulk request. Default 500. |
| `ReindexJob:MaxBatchRetries` | Retries for one bulk batch before giving up on it. Default 3. |
| `ReindexJob:TenantConcurrency` | Tenants reindexed concurrently. Default 2. |
| `ReindexJob:FailureThresholdPercent` | Max share of a tenant's documents allowed to fail before that tenant's whole run fails (alias left untouched). Default 5.0. |

Schedule is **not** a config key here — this process always runs to completion once and
exits. The schedule lives entirely in the Kubernetes CronJob's `schedule:` field
(`infrastructure/k8s/reindex-job/cronjob.yaml`).

## Running locally via Aspire

The AppHost registers this project with `.WithExplicitStart()` — it won't run automatically
on every `dotnet run` of the AppHost like the always-on services do. Trigger a manual run
from the Aspire dashboard when you want to test a full reindex locally.

## Docker

```bash
docker build -f src/ProductManager/OpenHub.ProductManager.ReindexJob/Dockerfile -t openhub/product-manager-reindex-job:latest src/
docker run --rm --env-file .env openhub/product-manager-reindex-job:latest
```

The container exits when the run completes — check the exit code, not a health endpoint.

## Kubernetes

See `infrastructure/k8s/reindex-job/cronjob.yaml`. To trigger an out-of-schedule run manually:

```bash
kubectl create job --from=cronjob/product-reindex manual-reindex-$(date +%s) -n reindex-job
```
