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

var elasticsearchPassword = builder.AddParameter("elasticsearch-password", value: "Dev_P@ssw0rd1!", secret: true);

var elasticsearch =
    builder.AddElasticsearch("Elasticsearch", password: elasticsearchPassword)
           .WithDataVolume();

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
    .WithReference(keycloak)
    .WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
    // Fallback realm when the hostname has no recognizable subdomain (e.g. plain
    // localhost:5173 dev) — keycloak.ts otherwise derives the realm from the
    // hostname's first label (client-a.localhost -> "client-a", admin.localhost ->
    // "platform-admin"), so a real deployment needs no per-realm env var at all.
    .WithEnvironment("VITE_KEYCLOAK_DEFAULT_REALM", "client-a")
    .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "micro-ui-shell")
    .WithExternalHttpEndpoints();

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
