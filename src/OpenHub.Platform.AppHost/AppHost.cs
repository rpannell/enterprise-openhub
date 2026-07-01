var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", value: "Dev_P@ssw0rd1!", secret: true);

var postgres =
    builder.AddPostgres("postgres", password: postgresPassword)
           .WithDataVolume();

// Product catalog database — Flyway manages its schema
var catalogDb = postgres.AddDatabase("catalog");

var rabbitUser = builder.AddParameter("rabbitmq-username", value: "appuser");
var rabbitPass = builder.AddParameter("rabbitmq-password", value: "MyDevPassword123!", secret: true);

var rabbitmqConfigPath = Path.Combine(builder.AppHostDirectory, "RabbitMq", "rabbitmq.conf");
var rabbitmqDefinitionsPath = Path.Combine(builder.AppHostDirectory, "RabbitMq", "definitions.json");

var rabbitmq =
    builder.AddRabbitMQ("rabbit", userName: rabbitUser, password: rabbitPass)
           .WithManagementPlugin()
           .WithDataVolume()
           .WithBindMount(rabbitmqConfigPath, "/etc/rabbitmq/rabbitmq.conf", isReadOnly: true)
           .WithBindMount(rabbitmqDefinitionsPath, "/etc/rabbitmq/definitions.json", isReadOnly: true);

var redis =
    builder.AddRedis("redis")
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
    builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
           .WithDataVolume()
           .WithRealmImport("./Keycloak")
           .WithBindMount(keycloakThemePath, "/opt/keycloak/themes/openhub", isReadOnly: true);

if (useRabbitListener)
{
    keycloak
        .WithDockerfile("./keycloak-rabbitmq-listener")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_HOST", "rabbit")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PORT", "5672")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USERNAME", "appuser")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PASSWORD", "MyDevPassword123!")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_QUEUE", "keycloak-events")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_EVENT_TYPES", "LOGIN,LOGOUT,LOGIN_ERROR,REGISTER")
        .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_RESOURCE_TYPES", "USER,ORGANIZATION,ORGANIZATION_MEMBERSHIP")
        .WaitFor(rabbitmq);
}

// Flyway runs schema migrations before the API starts
var flywayPath = Path.Combine(builder.AppHostDirectory, "..", "Database", "ProductManager");
var flyway = builder.AddContainer("flyway-catalog", "flyway/flyway", "latest")
    .WithBindMount(flywayPath, "/flyway/sql", isReadOnly: true)
    .WithEnvironment("FLYWAY_URL", "jdbc:postgresql://postgres:5432/catalog")
    .WithEnvironment("FLYWAY_USER", "postgres")
    .WithEnvironment("FLYWAY_PASSWORD", postgresPassword)
    .WithEnvironment("FLYWAY_SCHEMAS", "ref,catalog")
    .WithEnvironment("FLYWAY_LOCATIONS", "filesystem:/flyway/sql")
    .WithEnvironment("FLYWAY_CONNECT_RETRIES", "10")
    .WithEnvironment("FLYWAY_CONNECT_RETRIES_INTERVAL", "3")
    .WithArgs("repair", "migrate")
    .WaitFor(catalogDb);

var frontend = builder.AddViteApp("shell", "../OpenHub.UI")
    .WithReference(keycloak)
    .WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
    .WithEnvironment("VITE_KEYCLOAK_REALM", "client-a")
    .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "micro-ui-shell")
    .WithExternalHttpEndpoints();

var productManagerApi = builder.AddProject<Projects.OpenHub_ProductManager_Api>("product-manager-api")
    .WithHttpEndpoint(port: 5200)
    .WithHttpHealthCheck("/health")
    .WithReference(catalogDb)
    .WithReference(redis)
    .WithReference(keycloak)
    .WithEnvironment("KeyCloak__BaseUrl", keycloak.GetEndpoint("http"))
    .WithEnvironment("KeyCloak__AdminUsername", "admin")
    .WithEnvironment("KeyCloak__AdminPassword", keycloakAdminPassword)
    .WithEnvironment("Consul__Url", consul.GetEndpoint("http"))
    .WithEnvironment("Vault__Address", vault.GetEndpoint("http"))
    .WithEnvironment("Vault__Token", vaultToken)
    .WaitFor(flyway)
    .WaitFor(keycloak);

builder.Build().Run();
