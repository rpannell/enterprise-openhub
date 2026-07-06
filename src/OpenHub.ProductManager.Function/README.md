# OpenHub Product Manager Function

Azure Function (isolated worker, deployed as a Docker container) hosting the nightly product index job. The RabbitMQ-triggered product sync consumer that used to live here has moved to `OpenHub.ProductManager.ElasticProcessor` (a plain Rebus-based Worker Service, no Azure Functions host) — see that project's README for the `product.changed` consumer.

## What it does

| Trigger | Function | Behavior |
|---|---|---|
| Timer, `%NightlyIndexSchedule%` | `NightlyIndexFunction` | Scaffold only — logs and returns. Tenant enumeration and the full Elastic re-index still need their own design pass. |

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | `dotnet --version` |
| .NET Aspire workload | latest | `dotnet workload install aspire` |
| Azure Functions Core Tools | v4 | Needed for local Functions host (`func start`), which Aspire drives automatically |
| Docker Desktop | any | Required for all containerized dependencies, and for the deployed container image |

---

## Running with Aspire (recommended)

```powershell
cd src
dotnet run --project OpenHub.Platform.AppHost
```

Aspire runs the local Functions host (`func start`) for this project during `aspire run`; the Dockerfile is a separate path used only for building the deployed container image, the same relationship `ProductManager.Api` has with its own Dockerfile.

This Function has no HTTP triggers, so there's no browsable endpoint — watch its output via the Aspire dashboard (http://localhost:18888) instead.

---

## Running standalone (without Aspire)

**Optional — omit to skip Consul config loading:**
```powershell
$env:Consul__Url = "http://localhost:8500"
```

Then:
```powershell
cd src/OpenHub.ProductManager.Function
func start
```

You'll also need `AzureWebJobsStorage` set in `local.settings.json` (gitignored — not committed) — `UseDevelopmentStorage=true` works with the Azurite storage emulator, or point it at a real storage account.

---

## Docker

Build the image (from the repo root):
```bash
docker build \
  -f src/OpenHub.ProductManager.Function/Dockerfile \
  -t openhub/product-manager-function:latest \
  src/
```

Run:
```bash
docker run \
  -e AzureWebJobsStorage="<storage-connection-string>" \
  openhub/product-manager-function:latest
```

---

## Configuration reference

| Key | Source | Description |
|---|---|---|
| `NightlyIndexSchedule` | env / Consul | NCronTab schedule for `NightlyIndexFunction`, e.g. `0 0 2 * * *` (2am daily) |
| `Consul:Url` | env | Consul agent URL. Omit to skip Consul config loading. |

---

## Architecture

```
Functions/            ← trigger entry point (TimerTrigger)
    ↓ calls
Managers/              ← orchestrate (currently a stub — see NightlyIndexManager)
```

Same iDesign layering as `ProductManager.Api` — see `.claude/commands/idesign.md` for the full pattern rules.
