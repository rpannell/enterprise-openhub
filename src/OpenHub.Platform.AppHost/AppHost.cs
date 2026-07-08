using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Standard .NET hosts only auto-load user secrets when the environment is
// "Development" — the plain "C#: OpenHub.Platform.AppHost" launch profile in
// .vscode/launch.json doesn't set DOTNET_ENVIRONMENT, so secrets (e.g.
// keycloak-admin-password) would otherwise silently fail to load and prompt
// every run instead of being read back.
builder.Configuration.AddUserSecrets<Program>();

var postgresPassword = builder.AddParameter("postgres-password", value: "Dev_P@ssw0rd1!", secret: true);

var postgres =
    builder.AddPostgres("postgres", password: postgresPassword)
           .WithDataVolume();

// Product catalog database — Flyway manages its schema
var catalogDb = postgres.AddDatabase("Catalog");

var rabbitUser = builder.AddParameter("rabbitmq-username", value: "appuser");
var rabbitPass = builder.AddParameter("rabbitmq-password", value: "MyDevPassword123!", secret: true);

var rabbitmqConfigPath = Path.Combine(builder.AppHostDirectory, "RabbitMq", "rabbitmq.conf");
var rabbitmqDefinitionsPath = Path.Combine(builder.AppHostDirectory, "RabbitMq", "definitions.json");

var rabbitmq =
    builder.AddRabbitMQ("RabbitMq", userName: rabbitUser, password: rabbitPass, port: 5672)
           .WithManagementPlugin()
           .WithDataVolume()
           .WithBindMount(rabbitmqConfigPath, "/etc/rabbitmq/rabbitmq.conf", isReadOnly: true)
           .WithBindMount(rabbitmqDefinitionsPath, "/etc/rabbitmq/definitions.json", isReadOnly: true);

var redis =
    builder.AddRedis("Redis")
           .WithDataVolume();

// No "@" (or other URI-reserved characters) — unlike Postgres/RabbitMQ, which use
// key=value connection strings immune to this, Aspire's Elasticsearch hosting
// integration builds a "https://elastic:{password}@{host}:{port}" URI directly from
// this value. An unescaped "@" in the password (the same "Dev_P@ssw0rd1!" convention
// used elsewhere in this file) gets misread as the userinfo/host separator, so
// everything after it is parsed as part of the hostname and fails with
// "Invalid URI: The hostname could not be parsed."
var elasticsearchPassword = builder.AddParameter("elasticsearch-password", value: "Dev-ElasticPass1!", secret: true);

var elasticsearch =
    builder.AddElasticsearch("Elasticsearch", password: elasticsearchPassword)
           .WithDataVolume()
           // Elasticsearch's default JVM heap sizing can exceed what's actually
           // available once this many containers (including Keycloak's own JVM)
           // share one Docker Desktop VM — it gets silently OOM-killed (exit 137,
           // "died while starting up") rather than failing with a clear error.
           // Capped low enough for local dev; raise Docker Desktop's memory limit
           // instead if Elasticsearch still won't stay up.
           .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m");

var minio =
    builder.AddContainer("minio", "quay.io/minio/minio")
           .WithEndpoint(9000, 9000)
           .WithArgs("server", "/data");

var consulToken = builder.AddParameter("consul-token", value: "dev-only-token", secret: true);

var consul = builder.AddContainer("consul", "hashicorp/consul")
    .WithArgs("agent", "-dev", "-client=0.0.0.0", "-ui")
    .WithHttpEndpoint(targetPort: 8500, name: "http")
    .WithEndpoint(targetPort: 8600, name: "dns", scheme: "udp")
    .WithHttpHealthCheck("/v1/status/leader");

// One-shot: writes the non-secret dev config Consul is meant to serve — see
// Consul/seed.sh. Without this, Consul KV is always empty in dev and
// AddConsulConfiguration() silently no-ops on every run.
var consulSeedPath = Path.Combine(builder.AppHostDirectory, "Consul");
var consulSeed = builder.AddContainer("consul-seed", "hashicorp/consul")
    .WithEntrypoint("/bin/sh")
    .WithArgs("/seed/seed.sh")
    .WithBindMount(consulSeedPath, "/seed", isReadOnly: true)
    .WithEnvironment("CONSUL_HTTP_ADDR", consul.GetEndpoint("http"))
    .WaitFor(consul);

var vaultToken = builder.AddParameter("vault-token", value: "dev-only-token", secret: true);

var vault = builder.AddContainer("vault", "hashicorp/vault")
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultToken)
    .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
    .WithHttpEndpoint(targetPort: 8200, name: "http")
    .WithHttpHealthCheck("/v1/sys/health");

var keycloakThemePath = Path.Combine(builder.AppHostDirectory, "..", "OpenHub.UI", "keycloak-theme", "openhub");

bool.TryParse(builder.Configuration["Keycloak:UseRabbitMqListener"], out var useRabbitListener);

var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var keycloak =
    builder.AddKeycloak("KeyCloak", port: 8080, adminPassword: keycloakAdminPassword)
           .WithDataVolume()
           .WithRealmImport("./Keycloak")
           .WithBindMount(keycloakThemePath, "/opt/keycloak/themes/openhub", isReadOnly: true);

if (useRabbitListener)
{
    keycloak
        .WithDockerfile("./keycloak-rabbitmq-listener")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_HOST", "RabbitMq")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PORT", "5672")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USERNAME", "appuser")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PASSWORD", "MyDevPassword123!")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_QUEUE", "keycloak-events")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_EVENT_TYPES", "LOGIN,LOGOUT,LOGIN_ERROR,REGISTER")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_RESOURCE_TYPES", "USER,ORGANIZATION,ORGANIZATION_MEMBERSHIP")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_TENANT_CHANGED_EXCHANGE", "tenant.changed")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USER_CHANGED_EXCHANGE", "user.changed")
        .WaitFor(rabbitmq);
}

// One-shot: writes the dev secrets Vault is meant to serve — see Vault/seed.sh.
// Without this, Vault KV is always empty in dev and AddVaultSecrets() silently
// no-ops on every run. Values are passed in as env vars, not hardcoded in the script.
var vaultSeedPath = Path.Combine(builder.AppHostDirectory, "Vault");
var vaultSeed = builder.AddContainer("vault-seed", "hashicorp/vault")
    .WithEntrypoint("/bin/sh")
    .WithArgs("/seed/seed.sh")
    .WithBindMount(vaultSeedPath, "/seed", isReadOnly: true)
    .WithEnvironment("VAULT_ADDR", vault.GetEndpoint("http"))
    .WithEnvironment("VAULT_TOKEN", vaultToken)
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithEnvironment("RABBITMQ_PASSWORD", rabbitPass)
    .WaitFor(vault);

// Non-superuser role ProductManager.Api actually connects as (see V1_8__create_app_role.sql) —
// Postgres superusers bypass Row Level Security entirely, so the app must NOT connect as
// "postgres" or the tenant_isolation policies on catalog.users/products/etc. are silent no-ops.
var catalogAppUserPassword = builder.AddParameter("catalog-app-user-password", value: "Dev_AppUser_P@ss1!", secret: true);

// Flyway runs schema migrations before the API starts
var flywayPath = Path.Combine(builder.AppHostDirectory, "..", "Database", "ProductManager");
var flyway = builder.AddContainer("flyway-catalog", "flyway/flyway", "latest")
    .WithBindMount(flywayPath, "/flyway/sql", isReadOnly: true)
    .WithEnvironment("FLYWAY_URL", "jdbc:postgresql://postgres:5432/Catalog")
    .WithEnvironment("FLYWAY_USER", "postgres")
    .WithEnvironment("FLYWAY_PASSWORD", postgresPassword)
    .WithEnvironment("FLYWAY_SCHEMAS", "ref,catalog")
    .WithEnvironment("FLYWAY_LOCATIONS", "filesystem:/flyway/sql")
    .WithEnvironment("FLYWAY_CONNECT_RETRIES", "10")
    .WithEnvironment("FLYWAY_CONNECT_RETRIES_INTERVAL", "3")
    .WithEnvironment("FLYWAY_PLACEHOLDERS_APP_USER_PASSWORD", catalogAppUserPassword)
    .WithArgs("repair", "migrate")
    .WaitFor(catalogDb);

var frontend = builder.AddViteApp("shell", "../OpenHub.UI")
    // Without this, Aspire assigns a random port each run (observed: 60465) instead
    // of honoring vite.config.ts's own server.port: 5173 — the .WithUrl() links below
    // hardcode :5173, so an unpinned port makes them all silently point at a dead
    // port ("host unreachable"/connection refused) with no obvious link back to this.
    .WithHttpEndpoint(port: 5173)
    .WithReference(keycloak)
    .WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
    // Fallback realm when the hostname has no recognizable subdomain (e.g. plain
    // localhost:5173 dev) — keycloak.ts otherwise derives the realm from the
    // hostname's first label (client-a.localhost -> "client-a", admin.localhost ->
    // "platform-admin"), so a real deployment needs no per-realm env var at all.
    .WithEnvironment("VITE_KEYCLOAK_DEFAULT_REALM", "client-a")
    .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "micro-ui-shell")
    .WithExternalHttpEndpoints()
    // Same Vite dev server (fixed at :5173 in vite.config.ts), three different
    // hostnames — keycloak.ts resolves the realm from whichever one you click.
    // The dashboard's own "Endpoint" link only ever shows the bare
    // http://localhost:5173, which always falls back to VITE_KEYCLOAK_DEFAULT_REALM
    // above (client-a) — these give one-click access to the other realms too.
    .WithUrl("http://client-a.localhost:5173", "Client A")
    .WithUrl("http://client-b.localhost:5173", "Client B")
    .WithUrl("http://admin.localhost:5173", "Platform Admin");

// A genuine Module Federation remote — built with Rspack, not Vite, and
// deliberately not wired via .WithReference()/service discovery the way
// the .NET APIs are. The shell finds it by a plain hardcoded dev URL in
// vite.config.ts (federation's `remotes` config), the same way a real
// deployment would point at wherever this remote's static assets are
// actually hosted — that URL is the whole integration surface, not
// Aspire's resource graph. See docs/module-federation.md.
// No .WithHttpEndpoint() here — Aspire's DCP wraps declared HTTP endpoints in its own
// loopback-bound proxy (127.0.0.1/::1), and that proxy hangs indefinitely in front of this
// specific rspack dev server (confirmed: the raw rspack process responds instantly when hit
// directly via its LAN-bound address, e.g. http://192.168.68.65:5175/, but every request through
// DCP's proxy on localhost:5175 times out with no response — TCP accepted, HTTP never answered).
// AddViteApp's "shell" resource uses the identical WithHttpEndpoint/proxy pattern and works fine,
// so this is specific to how DCP's proxy interacts with rspack's dev server, not a general Aspire
// or Module Federation problem. Since this remote isn't wired via .WithReference()/service
// discovery anyway (see comment above), it doesn't need an Aspire-tracked endpoint — the shell
// talks to devServer.port in rspack.config.ts directly. Aspire still starts/monitors the process
// for the dashboard; it just won't show a clickable URL for it.
var adminUiRemote = builder.AddJavaScriptApp("admin-ui-remote", "../OpenHub.Administration.UI", "dev");

var productManagerApi = builder.AddProject<Projects.OpenHub_ProductManager_Api>("product-manager-api")
    .WithHttpEndpoint(port: 5200)
    .WithHttpHealthCheck("/health")
    .WithReference(catalogDb)
    .WithReference(redis)
    .WithReference(keycloak)
    // Overrides the admin (postgres) connection string .WithReference(catalogDb) set above —
    // must run as the non-superuser app_user role for Row Level Security to actually apply.
    // See the comment on catalogAppUserPassword and V1_8__create_app_role.sql.
    .WithEnvironment("ConnectionStrings__Catalog",
        $"Host={postgres.Resource.Host};Port={postgres.Resource.Port};Username=app_user;Password={catalogAppUserPassword};Database=Catalog")
    .WithEnvironment("KeyCloak__BaseUrl", keycloak.GetEndpoint("http"))
    .WithEnvironment("KeyCloak__AdminUsername", "admin")
    .WithEnvironment("KeyCloak__AdminPassword", keycloakAdminPassword)
    .WithEnvironment("Consul__Url", consul.GetEndpoint("http"))
    .WithEnvironment("Vault__Address", vault.GetEndpoint("http"))
    .WithEnvironment("Vault__Token", vaultToken)
    .WaitForCompletion(flyway)
    .WaitFor(keycloak)
    .WaitForCompletion(consulSeed)
    .WaitForCompletion(vaultSeed);

// Stateless facade over Keycloak's Admin REST API — creates organizations and users.
// No database/cache/messaging reference: Keycloak is the only infrastructure dependency.
var administrationApi = builder.AddProject<Projects.OpenHub_Administration_Api>("administration-api")
    .WithHttpEndpoint(port: 5300)
    .WithHttpHealthCheck("/health")
    .WithReference(keycloak)
    .WithEnvironment("KeyCloak__BaseUrl", keycloak.GetEndpoint("http"))
    .WithEnvironment("KeyCloak__AdminUsername", "admin")
    .WithEnvironment("KeyCloak__AdminPassword", keycloakAdminPassword)
    .WithEnvironment("Consul__Url", consul.GetEndpoint("http"))
    .WithEnvironment("Vault__Address", vault.GetEndpoint("http"))
    .WithEnvironment("Vault__Token", vaultToken)
    .WaitFor(keycloak)
    .WaitForCompletion(consulSeed)
    .WaitForCompletion(vaultSeed);

var productManagerElasticProcessor = builder.AddProject<Projects.OpenHub_ProductManager_ElasticProcessor>("product-manager-elastic-processor")
    .WithReference(productManagerApi)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WithReference(elasticsearch)
    .WithEnvironment("KeyCloak__BaseUrl", keycloak.GetEndpoint("http"))
    .WithEnvironment("Consul__Url", consul.GetEndpoint("http"))
    .WithEnvironment("Vault__Address", vault.GetEndpoint("http"))
    .WithEnvironment("Vault__Token", vaultToken)
    .WaitFor(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(productManagerApi)
    .WaitFor(elasticsearch)
    .WaitForCompletion(consulSeed)
    .WaitForCompletion(vaultSeed);

var productManagerIdentityEventProcessor = builder.AddProject<Projects.OpenHub_ProductManager_IdentityEventProcessor>("product-manager-identity-event-processor")
    .WithReference(productManagerApi)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WithEnvironment("KeyCloak__BaseUrl", keycloak.GetEndpoint("http"))
    .WithEnvironment("Consul__Url", consul.GetEndpoint("http"))
    .WithEnvironment("Vault__Address", vault.GetEndpoint("http"))
    .WithEnvironment("Vault__Token", vaultToken)
    .WaitFor(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(productManagerApi)
    .WaitForCompletion(consulSeed)
    .WaitForCompletion(vaultSeed);

// One-shot full-catalog reindex — not a long-running service, so it isn't started
// automatically like the resources above. Trigger a manual run from the Aspire
// dashboard; in production this same image runs on a schedule via a Kubernetes
// CronJob (infrastructure/k8s/reindex-job/cronjob.yaml).
var productManagerReindexJob = builder.AddProject<Projects.OpenHub_ProductManager_ReindexJob>("product-manager-reindex-job")
    .WithReference(productManagerApi)
    .WithReference(redis)
    .WithReference(elasticsearch)
    .WithEnvironment("KeyCloak__BaseUrl", keycloak.GetEndpoint("http"))
    .WithEnvironment("Consul__Url", consul.GetEndpoint("http"))
    .WithEnvironment("Vault__Address", vault.GetEndpoint("http"))
    .WithEnvironment("Vault__Token", vaultToken)
    .WithExplicitStart()
    .WaitFor(keycloak)
    .WaitFor(productManagerApi)
    .WaitFor(elasticsearch)
    .WaitForCompletion(consulSeed)
    .WaitForCompletion(vaultSeed);

builder.Build().Run();
