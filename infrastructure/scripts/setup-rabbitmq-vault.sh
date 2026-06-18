#!/usr/bin/env bash
set -e

echo "Checking Vault status..."
vault status

echo "Ensuring KV path exists..."
vault kv get rabbitmq/config >/dev/null 2>&1 || {
  echo "KV path missing, creating..."
  vault kv put rabbitmq/config admin_user="admin" admin_password="${RABBITMQ_ADMIN_PASSWORD}"
}

echo "Checking for existing Erlang cookie..."
COOKIE=$(vault kv get -field=erlang_cookie rabbitmq/config 2>/dev/null || true)

if [ -z "$COOKIE" ]; then
  echo "No Erlang cookie found. Generating..."
  COOKIE=$(vault write -field=random_bytes transit/random/bytes bytes=32)
  vault kv patch rabbitmq/config erlang_cookie="$COOKIE"
  echo "Erlang cookie created and stored in Vault."
else
  echo "Erlang cookie already exists. Leaving it unchanged."
fi

echo "Updating admin password..."
vault kv patch rabbitmq/config admin_password="${RABBITMQ_ADMIN_PASSWORD}"

echo "Vault setup complete."
