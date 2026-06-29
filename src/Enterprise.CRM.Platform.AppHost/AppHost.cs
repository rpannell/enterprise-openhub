var builder = DistributedApplication.CreateBuilder(args);


var postgres =
    builder.AddPostgres("postgres")
           .WithDataVolume();

var rabbitUser = builder.AddParameter("rabbitmq-username", value: "appuser");
var rabbitPass = builder.AddParameter("rabbitmq-password", value: "MyDevPassword123!", secret: true);

var rabbitmqConfigPath = Path.Combine(builder.AppHostDirectory, "rabbitmq", "rabbitmq.conf");
var rabbitmqDefinitionsPath = Path.Combine(builder.AppHostDirectory, "rabbitmq", "definitions.json");

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
    .WithHttpEndpoint(targetPort: 8500, name: "http")   // HTTP API + built-in web UI
    .WithEndpoint(targetPort: 8600, name: "dns", scheme: "udp")
    .WithHttpHealthCheck("/v1/status/leader");

var vaultToken = builder.AddParameter("vault-token", value: "dev-only-token", secret: true);

var vault = builder.AddContainer("vault", "hashicorp/vault")
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultToken)
    .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
    .WithHttpEndpoint(targetPort: 8200, name: "http")
    .WithHttpHealthCheck("/v1/sys/health");

var keycloakThemePath = Path.Combine(builder.AppHostDirectory, "..", "OpenHub.UI", "keycloak-theme", "openhub");

var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var keycloak =
    builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
           //.WithDataVolume()
           .WithRealmImport("./Keycloak")
           .WithBindMount(keycloakThemePath, "/opt/keycloak/themes/openhub", isReadOnly: true)
           .WithDockerfile("./keycloak-rabbitmq-listener")   // builds the Dockerfile in this repo
           //.WithLifetime(ContainerLifetime.Persistent)
           .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_HOST", "rabbit")
           .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PORT", "5672")
           .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USERNAME", "appuser")
           .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PASSWORD", "MyDevPassword123!")
           .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_QUEUE", "keycloak-events")
           .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_EVENT_TYPES", "LOGIN,LOGOUT,LOGIN_ERROR,REGISTER")
           .WithEnvironment("KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_RESOURCE_TYPES", "USER,ORGANIZATION,ORGANIZATION_MEMBERSHIP")
           .WaitFor(rabbitmq); 


var frontend = builder.AddViteApp("shell", "../OpenHub.UI")
    .WithReference(keycloak)
    .WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
    .WithEnvironment("VITE_KEYCLOAK_REALM", "MyApp")
    .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "micro-ui-shell")
    .WithExternalHttpEndpoints();

var apiService = builder.AddProject<Projects.Enterprise_CRM_Platform_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Enterprise_CRM_Platform_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
