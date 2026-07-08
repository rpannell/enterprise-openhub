#!/bin/sh
# One-shot: writes the platform's non-secret dev config into Consul KV so
# AddConsulConfiguration() has something to actually resolve. Safe to re-run —
# `consul kv put` overwrites the existing value at each path.
set -e

consul kv put platform/config @/seed/platform-config.json
consul kv put product-manager/config @/seed/product-manager-config.json
consul kv put product-manager-function/config @/seed/product-manager-function-config.json

echo "[consul-seed] done."
