# Stops the local OpenHubCRM platform stack in reverse dependency order.
# Data is preserved (bind-mounted to ./**/nodeN etc.) - this does not pass -v,
# so nothing is deleted. Pass -Remove to also remove the containers (still
# keeps data on disk); without it this just stops them.
#
# Usage: .\down.ps1
param(
    [switch]$Remove
)

$ErrorActionPreference = "Stop"
$dockerDir = $PSScriptRoot

$composeFiles = @(
    "compose.platform.yaml",
    "compose.observability.yaml",
    "compose.data.minio.yaml",
    "compose.data.redis.yaml",
    "compose.data.postgres.yaml",
    "compose.data.yaml"
)

foreach ($file in $composeFiles) {
    $path = Join-Path $dockerDir $file
    if ($Remove) {
        Write-Host "==> docker compose -f $file down" -ForegroundColor Cyan
        docker compose -f $path down
    } else {
        Write-Host "==> docker compose -f $file stop" -ForegroundColor Cyan
        docker compose -f $path stop
    }
}
