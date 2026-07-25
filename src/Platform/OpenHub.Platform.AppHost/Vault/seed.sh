#!/bin/sh
# One-shot: writes the platform's dev secrets into Vault KV v2 so
# AddVaultSecrets() has something to actually resolve, instead of every value
# silently falling back to appsettings.Development.json. Values come in as env
# vars from AppHost.cs (the same Aspire parameters the containers themselves
# use), never hardcoded here. Safe to re-run — `vault kv put` overwrites.
set -e

vault kv put secret/platform \
  "KeyCloak:AdminPassword=${KEYCLOAK_ADMIN_PASSWORD}" \
  "KeyCloak:M2M:ClientSecrets:platform-admin=dev-secret"

vault kv put secret/product-manager \
  "RabbitMq:Password=${RABBITMQ_PASSWORD}" \
  "KeyCloak:M2M:ClientSecrets:platform-admin=dev-secret" \
  "KeyCloak:M2M:ClientSecrets:client-a=dev-secret" \
  "KeyCloak:M2M:ClientSecrets:client-b=dev-secret"

vault kv put secret/product-manager-function \
  "RabbitMq:Password=${RABBITMQ_PASSWORD}" \
  "KeyCloak:M2M:ClientSecrets:platform-admin=dev-secret" \
  "KeyCloak:M2M:ClientSecrets:client-a=dev-secret" \
  "KeyCloak:M2M:ClientSecrets:client-b=dev-secret"

vault kv put secret/crm \
  "RabbitMq:Password=${RABBITMQ_PASSWORD}"

vault kv put secret/salesorg \
  "RabbitMq:Password=${RABBITMQ_PASSWORD}"

echo "[vault-seed] done."
