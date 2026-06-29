# Keycloak RabbitMQ Event Listener

Publishes every Keycloak event (login, logout, register, admin actions, etc.)
to a RabbitMQ queue as JSON.

## Two ways to build this

**Option A — manual build, bind mount (simpler, more visible).**
You run `mvn package` yourself whenever you change the Java code; Aspire
just mounts the resulting jar into the container.

**Option B — Aspire builds it for you automatically (recommended).**
Aspire calls `docker build` on a multi-stage Dockerfile every time you run
the AppHost. Stage 1 runs Maven inside a throwaway build container; stage 2
layers the resulting jar onto Keycloak's own image. You never run `mvn`
yourself — editing the `.java` files and re-running the AppHost is enough.
This repo includes the `Dockerfile` for this option.

Both options assume this folder sits **next to** your AppHost project, not
inside the .NET solution — Maven and `dotnet build` are independent build
systems, so there's no reason to fold one into the other:

```
enterprise-crm/
  src/
    Enterprise.CRM.Platform.AppHost/
  keycloak-rabbitmq-listener/      <-- this folder
    pom.xml
    Dockerfile
    src/...
  Realms/
    MyApp-realm.json
```

**Before either option**, open `pom.xml` and `Dockerfile` and make sure
`keycloak.version` (in `pom.xml`) and the `FROM quay.io/keycloak/keycloak:...`
tag (in `Dockerfile`) both match whatever Keycloak version you're actually
running. Mismatches can cause the provider to fail loading silently.

## Option A — manual build + bind mount

Requires a JDK (17+) and Maven installed locally.

```bash
mvn clean package
```

This produces `target/rabbitmq-event-listener.jar`. Point a bind mount
directly at that file — that way every time you rebuild, the running
container's mount target gets the new jar on its next restart, with no
copy step:

```csharp
var keycloak = builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
    .WithRealmImport("./Realms")
    .WithBindMount(
        "./keycloak-rabbitmq-listener/target/rabbitmq-event-listener.jar",
        "/opt/keycloak/providers/rabbitmq-event-listener.jar",
        isReadOnly: true)
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_HOST", "rabbitmq")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PORT", "5672")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USERNAME", "guest")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PASSWORD", "guest")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_QUEUE", "keycloak-events")
    .WaitFor(rabbitmq);
```

## Option B — automatic build via Dockerfile (recommended)

No local Maven/JDK install needed — Docker does the build inside a
container. Use `WithDockerfile` on the *existing* `keycloak` resource
builder (not `AddDockerfile`, which would create a brand-new generic
container resource and lose the typed `KeycloakResource` helpers like
`WithRealmImport` and the `adminPassword` parameter):

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume();

var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);

var keycloak = builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
    .WithRealmImport("./Realms")
    .WithDockerfile("./keycloak-rabbitmq-listener")   // builds the Dockerfile in this repo
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_HOST", "rabbitmq")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PORT", "5672")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USERNAME", "guest")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PASSWORD", "guest")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_QUEUE", "keycloak-events")
    .WaitFor(rabbitmq);

builder.Build().Run();
```

The first run will be slower (Docker has to pull the `maven:3.9-eclipse-temurin-17`
build image and compile), but subsequent runs reuse Docker's build cache, so
only changed files trigger a recompile.

> Note: hardcoded `guest`/`guest` is RabbitMQ's default dev credential —
> swap for `builder.AddParameter(..., secret: true)` once you move past
> local dev, and pull the actual host/port from the RabbitMQ resource's
> connection info rather than hardcoding `"rabbitmq"`/`5672` if you want it
> to stay correct if you change the resource later.

The environment variable naming pattern Keycloak uses is:

```
KC_SPI_<SPI-NAME>_<PROVIDER-ID>_<PROPERTY>
```

all uppercased with dashes replaced by underscores. The SPI name for event
listeners is `events-listener`, and `PROVIDER_ID` is what we set in the
factory class (`rabbitmq-event-listener`).

## 4. Enable the listener in your realm

In your realm JSON, add the provider's ID to `eventsListeners` and turn
events on:

```json
{
  "realm": "MyApp",
  "enabled": true,
  "eventsEnabled": true,
  "eventsListeners": ["jboss-logging", "rabbitmq-event-listener"],
  "adminEventsEnabled": true,
  "adminEventsDetailsEnabled": true,
  ...
}
```

(`jboss-logging` is Keycloak's built-in listener that just logs events to
the console — keeping it alongside yours is usually convenient for
debugging.)

## 5. Configure which events get published (optional)

By default, every user event and every admin event gets published. You can
narrow that down with two optional, comma-separated env vars — no rebuild
required, just restart the container with different values:

```csharp
var keycloak = builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
    .WithRealmImport("./Realms")
    .WithDockerfile("./keycloak-rabbitmq-listener")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_HOST", "rabbitmq")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PORT", "5672")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USERNAME", "guest")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PASSWORD", "guest")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_QUEUE", "keycloak-events")
    // only publish these user events (leave unset/empty for "all")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_EVENT_TYPES", "LOGIN,LOGOUT,LOGIN_ERROR,REGISTER")
    // only publish these admin resource types (leave unset/empty for "all")
    .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_RESOURCE_TYPES", "USER,ORGANIZATION,ORGANIZATION_MEMBERSHIP")
    .WaitFor(rabbitmq);
```

Notes on these two settings:

- **`EVENT_TYPES`** filters what comes through `onEvent(Event)` — user-facing
  events like logins. Values must match Keycloak's `EventType` enum names
  exactly (e.g. `LOGIN`, `LOGOUT`, `LOGIN_ERROR`, `REGISTER`,
  `UPDATE_PASSWORD`, `REFRESH_TOKEN`). Case-insensitive, whitespace around
  commas is trimmed.
- **`RESOURCE_TYPES`** filters what comes through `onEvent(AdminEvent, ...)`
  — admin-console/API actions. Values must match Keycloak's `ResourceType`
  enum names (e.g. `USER`, `GROUP`, `CLIENT`, `ORGANIZATION`,
  `ORGANIZATION_MEMBERSHIP`).
- **Leave either var unset or empty** to disable filtering for that
  category — everything of that kind gets published. This is the default.
- **Unrecognized names are logged and skipped**, not treated as errors —
  check the Keycloak container logs for a `Unknown EventType '...'` or
  `Unknown ResourceType '...'` warning if something you expected isn't
  showing up.
- This is an **OR within each list, AND between the two settings** — i.e.
  user events are governed solely by `EVENT_TYPES` and admin events solely
  by `RESOURCE_TYPES`; they don't interact with each other.

If you need filtering logic more complex than "is this type in my allow-list"
(e.g. excluding a specific client's events, or routing different event
types to different queues), that's a small extension to
`RabbitMqEventListenerProvider.onEvent(...)` rather than a config value —
happy to add that if you hit that need.

- Start the AppHost (`aspire run`).
- Check Keycloak's container logs for a line like:
  `RabbitMQ event listener connected to rabbitmq:5672, queue 'keycloak-events'`
- Log in as a user, then check the RabbitMQ management UI
  (`http://localhost:<rabbitmq-management-port>`) — you should see a
  message land in the `keycloak-events` queue.

## Troubleshooting

- **Provider doesn't load at all / no log line on startup**: usually a
  version mismatch between `keycloak.version` in `pom.xml` and the actual
  running server, or the bind mount path is wrong. Check Keycloak's startup
  logs for `ServiceConfigurationError` or `ClassNotFoundException`.
- **Connects but events never publish**: confirm `eventsEnabled: true` and
  that `rabbitmq-event-listener` is actually in the `eventsListeners` array
  in the imported realm.
- **`ClassNotFoundException` for RabbitMQ classes**: the shade plugin
  didn't bundle the client — confirm you're deploying
  `target/rabbitmq-event-listener.jar` (the shaded one), not an
  intermediate jar.
