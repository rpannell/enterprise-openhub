#!/usr/bin/env bash
set -euo pipefail

# ----------------------------
# Config
# ----------------------------
VAULT_NAMESPACE="${VAULT_NAMESPACE:-vault}"
VAULT_LABEL_SELECTOR="${VAULT_LABEL_SELECTOR:-app.kubernetes.io/name=vault}"
SECRET_PATH="${VAULT_SECRET_PATH:-secret/rabbitmq}"
SECRET_KEY="erlangCookie"

# ----------------------------
# Validate required env
# ----------------------------
if [[ -z "${VAULT_TOKEN:-}" ]]; then
  echo "❌ VAULT_TOKEN is not set"
  exit 1
fi

echo "🔐 Using Vault namespace: $VAULT_NAMESPACE"

# ----------------------------
# Find Vault pod
# ----------------------------
POD=$(kubectl get pods -n "$VAULT_NAMESPACE" \
  -l "$VAULT_LABEL_SELECTOR" \
  -o jsonpath="{.items[0].metadata.name}")

if [[ -z "$POD" ]]; then
  echo "❌ No Vault pod found in namespace $VAULT_NAMESPACE"
  exit 1
fi

echo "📦 Vault pod: $POD"

# ----------------------------
# SAFE Vault exec wrapper
# ----------------------------
vault_exec() {
  local cmd="$1"

  kubectl exec -n "$VAULT_NAMESPACE" "$POD" -- \
    sh -c "export VAULT_TOKEN=$VAULT_TOKEN && $cmd"
}

# ----------------------------
# Check existing cookie
# ----------------------------
echo "🔍 Checking existing Erlang cookie..."

COOKIE=$(vault_exec "vault kv get -field=$SECRET_KEY $SECRET_PATH" 2>/dev/null || true)

# ----------------------------
# Create if missing
# ----------------------------
if [[ -z "$COOKIE" ]]; then
  echo "🆕 No cookie found. Generating..."

  COOKIE=$(openssl rand -base64 48 | tr -d '\n')

  vault_exec "vault kv put $SECRET_PATH $SECRET_KEY=$COOKIE"

  echo "✅ Cookie stored in Vault"
else
  echo "✅ Cookie already exists in Vault"
fi

# ----------------------------
# Export for GitHub Actions
# ----------------------------
echo "COOKIE=$COOKIE" >> "$GITHUB_ENV"