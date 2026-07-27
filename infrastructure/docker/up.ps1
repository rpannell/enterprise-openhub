# Brings up the local OpenHubCRM platform stack in the order the services
# actually depend on each other. Compose can't express depends_on across
# separate `docker compose -f` projects, so the order here is what enforces
# it: the network + data layer first, then observability, then the platform
# layer (Keycloak/Kong need Postgres already up and initialized).
#
# Usage: .\up.ps1
$ErrorActionPreference = "Stop"
$dockerDir = $PSScriptRoot

$composeFiles = @(
    "compose.data.yaml",          # creates the enterprise-platform network; rabbitmq + elasticsearch
    "compose.data.postgres.yaml", # postgres primary + replicas + haproxy + pgadmin
    "compose.data.redis.yaml",
    "compose.data.minio.yaml",
    "compose.observability.yaml",
    "compose.platform.yaml"       # consul, vault, keycloak, kong - keycloak/kong need postgres-1 up first
)

foreach ($file in $composeFiles) {
    Write-Host "==> docker compose -f $file up -d" -ForegroundColor Cyan
    docker compose -f (Join-Path $dockerDir $file) up -d
}

Write-Host ""
Write-Host "Stack is starting. First-time setup still needed (see README.md):" -ForegroundColor Yellow
Write-Host "  - vault operator init / unseal (vault-1, vault-2, vault-3)"
Write-Host "  - redis-cli --cluster create (redis-1..6)"
Write-Host "  - mc alias set / mirror for minio, if you need buckets seeded"
