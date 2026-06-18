#!/usr/bin/env bash
set -euo pipefail

VAULT_ADDR=${VAULT_ADDR:-"http://vault.vault.svc.cluster.local:8200"}
BOOTSTRAP_DIR=${BOOTSTRAP_DIR:-"/vault/bootstrap"}

mkdir -p "$BOOTSTRAP_DIR"

echo "Using VAULT_ADDR=$VAULT_ADDR"
export VAULT_ADDR

echo "Checking Vault status..."
if vault status >/dev/null 2>&1; then
  echo "Vault is already initialized."
else
  echo "Vault is not initialized. Initializing..."
  vault operator init -format=json > "$BOOTSTRAP_DIR/init.json"

  echo "Vault initialized. Storing unseal keys and root token in $BOOTSTRAP_DIR/init.json"
fi

ROOT_TOKEN=$(jq -r '.root_token' "$BOOTSTRAP_DIR/init.json")
UNSEAL_KEYS=($(jq -r '.unseal_keys_b64[]' "$BOOTSTRAP_DIR/init.json"))

echo "Unsealing Vault..."
vault operator unseal "${UNSEAL_KEYS[0]}"
vault operator unseal "${UNSEAL_KEYS[1]}"
vault operator unseal "${UNSEAL_KEYS[2]}"

echo "Logging in with root token..."
vault login "$ROOT_TOKEN"

echo "Writing RabbitMQ policy..."
cat > "$BOOTSTRAP_DIR/rabbitmq-policy.hcl" << 'EOF'
path "rabbitmq/config" {
  capabilities = ["create", "read", "update", "patch"]
}

path "transit/random/bytes" {
  capabilities = ["update"]
}
EOF

vault policy write rabbitmq-policy "$BOOTSTRAP_DIR/rabbitmq-policy.hcl"

echo "Enabling AppRole auth (if not already)..."
vault auth enable approle >/dev/null 2>&1 || echo "AppRole already enabled."

echo "Creating RabbitMQ AppRole..."
vault write auth/approle/role/rabbitmq-role \
  policies="rabbitmq-policy" \
  token_ttl="1h" \
  token_max_ttl="4h" \
  secret_id_ttl="4h" \
  secret_id_num_uses=0

ROLE_ID=$(vault read -field=role_id auth/approle/role/rabbitmq-role/role-id)
SECRET_ID=$(vault write -field=secret_id -f auth/approle/role/rabbitmq-role/secret-id)

echo "RabbitMQ AppRole created."
echo "ROLE_ID: $ROLE_ID"
echo "SECRET_ID: $SECRET_ID"

echo "$ROLE_ID" > "$BOOTSTRAP_DIR/rabbitmq-role-id"
echo "$SECRET_ID" > "$BOOTSTRAP_DIR/rabbitmq-secret-id"

echo "Vault bootstrap complete."
