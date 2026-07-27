# OpenHubCRM Local Enterprise Platform

## Overview

This project creates a local enterprise-style platform environment using Docker Compose.

The goal is to simulate a production architecture containing:

* Data services
* Platform services
* Identity management
* API gateway
* Service discovery
* Secrets management
* Observability
* .NET microservices integration

The platform is designed to mirror patterns commonly used with Kubernetes deployments.

---

# Quick Start

## Run order

Each layer is a separate Compose project (separate `docker compose -f` file), so
Compose's `depends_on` can't enforce order *across* them - the order you start
them in is what enforces it. Use the helper scripts, which run everything in
the right order:

```powershell
.\up.ps1
```

```powershell
.\down.ps1          # stops containers, keeps data
.\down.ps1 -Remove  # removes containers too, still keeps data (bind mounts)
```

If you want to run `docker compose` by hand instead, this is the order:

1. `compose.data.yaml` - creates the `enterprise-platform` network (all other
   files reference it as external), plus RabbitMQ and Elasticsearch.
2. `compose.data.postgres.yaml` - Postgres primary/replicas + haproxy +
   pgadmin. Must be up before step 6 (Keycloak/Kong need their databases).
3. `compose.data.redis.yaml`
4. `compose.data.minio.yaml`
5. `compose.observability.yaml`
6. `compose.platform.yaml` - Consul, Vault, Keycloak, Kong, Konga.

## Keeping it running across a Windows reboot

Every service has `restart: unless-stopped`, so Docker will bring containers
back up automatically whenever the Docker Engine restarts - but only if
Docker Desktop itself is running. To have the whole stack survive a full
Windows reboot with no manual steps, also enable in Docker Desktop:

```
Settings -> General -> Start Docker Desktop when you sign in to your computer
```

`unless-stopped` (rather than `always`) means containers you stop on purpose
(`.\down.ps1`) stay stopped - they won't come back until you start them again.

## First-time setup (manual, one-time)

These aren't automated by the compose files:

* Vault - `vault operator init` + `vault operator unseal` (see [Vault
  Initialization](#vault-initialization)).
* Redis - `redis-cli --cluster create` (see [Redis Cluster](#redis-cluster)).
* Postgres/Keycloak/Kong databases *are* created automatically on first boot
  (see [PostgreSQL Primary / Replica](#postgresql-primary--replica)) - no
  manual step needed there.

---

# Architecture Overview

```
                         Users
                           |
                           |
                      React Apps
                           |
                           |
                     Kong Gateway
                           |
                           |
              +------------+-------------+
              |                          |
          .NET APIs                Background Workers
              |
              |
        Consul Service Mesh
              |
              |
     +--------+---------+
     |                  |
   Vault            Keycloak


Data Layer:

 PostgreSQL HA
 Redis Cluster
 MinIO Cluster
 RabbitMQ
 Elasticsearch


Observability:

 .NET Applications
 Kong
 Consul
 Vault
 PostgreSQL
 Redis

          |
          |

 OpenTelemetry Collector

          |
    +-----+-----+------+
    |           |      |
Prometheus    Loki   Tempo

          |
          |
       Grafana
```

---

# Folder Structure

Actual layout (`infrastructure/docker/`, flat - not grouped into `Composes`/
`data`/`platform` subfolders):

```
infrastructure/docker

├── compose.data.yaml           (network + rabbitmq + elasticsearch)
├── compose.data.postgres.yaml
├── compose.data.redis.yaml
├── compose.data.minio.yaml
├── compose.observability.yaml
├── compose.platform.yaml
├── up.ps1                      (start everything, in order)
├── down.ps1                    (stop everything, reverse order)
│
├── postgres
│   ├── haproxy/haproxy.cfg
│   ├── init/01-init-primary.sh          (creates replicator role + keycloak/kong DBs)
│   ├── replica-entrypoint.sh            (postgres-2/postgres-3 clone-from-primary bootstrap)
│   ├── node1/ node2/ node3/             (bind-mounted data dirs, created at runtime)
│   └── pgadmin/                         (bind-mounted data dir, created at runtime)
│
├── redis
│   ├── node1..node6/                    (created at runtime)
│   └── redisinsight/                    (created at runtime)
│
├── minio
│   └── node1..node4/                    (created at runtime)
│
├── rabbitmq
│   └── node1..node3/                    (created at runtime)
│
├── elastic
│   └── node1..node3/                    (created at runtime)
│
├── consul
│   └── node1..node3/                    (created at runtime)
│
├── vault
│   ├── config/vault-1.hcl, vault-2.hcl, vault-3.hcl
│   └── node1/ node2/ node3/             (created at runtime)
│
└── observability
    ├── grafana/provisioning/            (checked in - datasources etc.)
    ├── grafana/data/                    (created at runtime)
    ├── loki/config.yaml
    ├── loki/data/                       (created at runtime)
    ├── otel/otel-config.yaml
    ├── prometheus/prometheus.yaml
    ├── prometheus/data/                 (created at runtime)
    ├── tempo/tempo.yaml
    └── tempo/data/                      (created at runtime)
```

The `nodeN/`, `pgadmin/`, `redisinsight/`, and observability `data/`
directories aren't checked in - they're bind-mount targets that Docker
creates the first time each stack comes up, and they're covered by the root
`.gitignore`.

Because everything lives flat under `infrastructure/docker/`, compose files
reference config with a single `./`, not `../`:

```yaml
./observability/prometheus/prometheus.yaml:/etc/prometheus/prometheus.yml
```

---

# Docker Network

All services communicate through:

```
enterprise-platform
```

Create:

```powershell
docker network create enterprise-platform
```

Containers can communicate using service names:

Example:

```
postgres-1
vault-1
keycloak
kong
otel-collector
```

instead of IP addresses.

---

# Data Layer

## PostgreSQL Primary / Replica

`postgres-1` is the primary (accepts writes). `postgres-2` and `postgres-3`
are physical streaming replicas: on first start (empty data directory) each
one clones `postgres-1` via `pg_basebackup -R`, which writes `standby.signal`
+ `primary_conninfo` automatically so they come up read-only and start
streaming WAL immediately. This is set up by
[`postgres/init/01-init-primary.sh`](postgres/init/01-init-primary.sh) (runs
once, only on `postgres-1`, only when its data directory is empty) and
[`postgres/replica-entrypoint.sh`](postgres/replica-entrypoint.sh) (runs on
`postgres-2`/`postgres-3` before the normal Postgres entrypoint).

The init script also creates the `keycloak` and `kong` databases/roles that
Keycloak and Kong expect to already exist - no manual step needed there.

There is **no automatic failover**. If `postgres-1` goes down, writes stop
until you manually promote a replica (`SELECT pg_promote();` on `postgres-2`
or `postgres-3`) and update `postgres/haproxy/haproxy.cfg` to point the write
backend at it. Real automatic failover would need something like
Patroni or repmgr, which isn't set up here.

haproxy splits traffic by port:

| Port | Routes to                                              |
| ---- | ------------------------------------------------------- |
| 5432 | `postgres-1` only (writes)                               |
| 5433 | `postgres-2`/`postgres-3` round-robin, `postgres-1` as backup (reads) |

Databases:

```
keycloak
kong
application databases
```

---

## Entity Framework Read / Write Pattern

Point the write `DbContext` at `haproxy:5432` and the read `DbContext` at
`haproxy:5433` (or `localhost:5432`/`localhost:5433` from outside Docker) to
get this split for free.

For high scale applications:

```
Application

    |
    |

Write DbContext
        |
        |
     Primary DB


Read DbContext
        |
        |
     Read Replica
```

The application chooses the context based on operation.

Example:

```csharp
Write operations:

Create
Update
Delete


Read operations:

Queries
Reports
Search
```

---

## Redis Cluster

Purpose:

* Distributed caching
* Session storage
* Fast lookups

Cluster example:

```
redis-1  Master
redis-2  Master
redis-3  Master

redis-4  Replica
redis-5  Replica
redis-6  Replica
```

Redis cluster creation - keep this on **one line**. PowerShell's backtick
line-continuation only works when the backtick is the very last character
on a line; if a multi-line version gets pasted/reflowed onto one line
(easy to do by accident), a backtick sitting mid-line right before a letter
like `r` is parsed as an escape sequence (`` `r `` = carriage return) instead
of a continuation, silently corrupting the next argument - it'll look like
a hostname is missing its first letter and fail DNS resolution, when the
real problem is a stray control character:

```powershell
docker exec redis-1 redis-cli --cluster create redis-1:6379 redis-2:6379 redis-3:6379 redis-4:6379 redis-5:6379 redis-6:6379 --cluster-replicas 1 --cluster-yes
```

`redis-cli --cluster create` also normally pauses for an interactive
`Can I set the above configuration? (type 'yes' to accept):` prompt.
`--cluster-yes` accepts that automatically, and `-it` isn't needed since
there's no other interaction - a plain `docker exec` is enough.

Verify it actually formed correctly:

```powershell
docker exec redis-1 redis-cli cluster info
docker exec redis-1 redis-cli cluster nodes
```

`cluster_state:ok` and `cluster_slots_ok:16384` mean all slots are covered.

---

## MinIO Cluster

Purpose:

S3-compatible object storage.

Used for:

* files
* documents
* images
* exports

Management:

```
MinIO Console
```

The `mc` client can be run through Docker:

```powershell
docker run --rm -it `
--network enterprise-platform `
minio/mc
```

Configure:

```bash
mc alias set local http://minio-1:9000 admin admin123
```

---

## RabbitMQ

Purpose:

Message processing.

Example:

```
API
 |
 |
RabbitMQ Queue
 |
 |
Worker Container
```

Used for:

* background jobs
* asynchronous processing
* event-driven workflows

---

## Elasticsearch

Purpose:

Search and analytics.

Includes:

* Elasticsearch cluster
* Kibana UI

Memory considerations:

Elasticsearch containers require significant memory.

Docker Desktop memory allocation must be increased if nodes crash with:

```
exit code 137
```

---

# Platform Layer

---

# Consul

Purpose:

* Service discovery
* Health checks
* Service mesh foundation

Cluster:

```
consul-1
consul-2
consul-3
```

Verify:

```powershell
docker exec -it consul-1 consul members
```

Future:

```
.NET Service

   |
 Envoy Sidecar

   |
 Consul Mesh
```

---

# Vault

Purpose:

Secrets management.

Provides:

* database passwords
* certificates
* API keys
* encryption keys

Architecture:

```
vault-1
   |
vault-2
   |
vault-3
```

Storage:

```
Raft
```

---

## Vault Initialization

All three nodes have `retry_join` configured, so vault-2 and vault-3
auto-join vault-1's raft cluster as soon as it exists - but they still each
need to be unsealed individually with the same keys.

Initialize only once, on vault-1:

```powershell
docker exec -it vault-1 vault operator init
```

Save:

* unseal keys
* root token

Unseal **all three nodes** with the same 3 keys:

```powershell
docker exec -it vault-1 vault operator unseal
docker exec -it vault-2 vault operator unseal
docker exec -it vault-3 vault operator unseal
```

Repeat each command 3 times (once per unseal key).

Verify:

```powershell
docker exec -it vault-1 vault status
```

Expected:

```
Sealed false
Storage Type raft
HA Enabled true
```

---

# Windows Docker Bind Mount Issue

When mounting individual files:

```yaml
file:/container/file
```

Docker Desktop may create a directory if the source file does not exist.

Example problem:

```
vault-1.hcl
```

was accidentally created as:

```
vault-1.hc1
```

Docker created:

```
vault-1.hcl/
```

Fix:

* Verify filenames
* Confirm file extensions
* Prefer mounting configuration directories:

Example:

```yaml
./vault/config:/vault/config
```

---

# Keycloak

Purpose:

Identity provider.

Provides:

* OpenID Connect
* OAuth2
* JWT tokens
* Roles
* Users
* Service accounts

Database:

```
PostgreSQL

database:
keycloak
```

Access:

```
http://localhost:8080
```

Development login:

```
admin
admin123
```

---

## Realm

Create:

```
openhubcrm
```

Avoid using:

```
master
```

for applications.

---

## Clients

Example:

```
crm-web

crm-api

background-worker
```

---

# Kong API Gateway

Purpose:

Application edge gateway.

Provides:

* routing
* authentication
* authorization
* plugins
* rate limiting

Flow:

```
Browser

   |

Kong

   |

.NET API
```

Database:

```
PostgreSQL

database:
kong
```

---

Ports:

| Port | Purpose     |
| ---- | ----------- |
| 8000 | Proxy       |
| 8443 | HTTPS Proxy |
| 8001 | Admin API   |

---

# Konga

Konga provides a visual learning UI for Kong.

Access:

```
http://localhost:1337
```

Connect:

```
Admin URL:

http://kong:8001
```

Note:

Konga is for learning only. Production environments typically use:

* Kong declarative configuration
* Terraform
* Kubernetes CRDs
* GitOps

---

# Observability Platform

Stack:

```
OpenTelemetry Collector
Prometheus
Grafana
Loki
Tempo
```

Architecture:

```
Applications

    |

OpenTelemetry Collector

    |
    +---- Metrics --> Prometheus
    |
    +---- Logs -----> Loki
    |
    +---- Traces ---> Tempo

              |
              |
            Grafana
```

---

# Data Retention

Prevent telemetry growth.

## Prometheus

Recommended:

```
30 days
10GB maximum
```

Example:

```
--storage.tsdb.retention.time=30d
--storage.tsdb.retention.size=10GB
```

---

## Loki

Recommended:

```
7 days
```

---

## Tempo

Recommended:

```
24 hours
```

---

## Sampling

Trace sampling reduces storage:

Example:

```
1000 traces received

10% sampling

100 traces stored
```

---

# .NET Application Observability Integration

Applications send telemetry only to:

```
OpenTelemetry Collector
```

They do not communicate directly with:

* Prometheus
* Loki
* Tempo

Flow:

```
.NET Service

      |

OTLP

      |

otel-collector:4317

      |

Grafana Stack
```

---

# Required NuGet Packages

Example ASP.NET Core service:

```powershell
dotnet add package OpenTelemetry.Extensions.Hosting

dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol

dotnet add package OpenTelemetry.Instrumentation.AspNetCore

dotnet add package OpenTelemetry.Instrumentation.Http

dotnet add package OpenTelemetry.Instrumentation.Runtime

dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore

dotnet add package OpenTelemetry.Instrumentation.StackExchangeRedis

dotnet add package OpenTelemetry.Instrumentation.SqlClient
```

---

# .NET Configuration Example

```csharp
builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint =
                    new Uri("http://otel-collector:4317");
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint =
                    new Uri("http://otel-collector:4317");
            });
    });
```

---

# Telemetry Examples

## Logs

Existing code:

```csharp
_logger.LogInformation(
    "Order {OrderId} created",
    orderId);
```

Pipeline:

```
ILogger

 |

OpenTelemetry

 |

Loki

 |

Grafana
```

---

## Metrics

Automatically collected:

* HTTP requests
* response times
* runtime metrics
* garbage collection
* memory
* CPU

---

## Traces

Example:

```
Kong

 |

order-api

 |

customer-api

 |

PostgreSQL
```

Viewed in:

```
Grafana Tempo
```

---

# Future Enhancements

Next development steps:

1. Add .NET microservices
2. Add Consul service mesh with Envoy
3. Add Kong JWT validation with Keycloak
4. Add Vault secrets injection
5. Add OpenTelemetry instrumentation to all services
6. Move Compose deployment to Kubernetes/K3s
7. Add CI/CD pipelines
8. Add GitOps deployment

---

# Final Platform State

The completed local enterprise platform:

```
Identity:
    Keycloak

Gateway:
    Kong

Discovery:
    Consul

Secrets:
    Vault

Data:
    PostgreSQL
    Redis
    MinIO
    RabbitMQ
    Elasticsearch

Observability:
    OpenTelemetry
    Prometheus
    Grafana
    Loki
    Tempo
```

This environment provides a realistic foundation for building and testing enterprise .NET microservices.
