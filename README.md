# Enterprise CRM

A comprehensive infrastructure-as-code setup for deploying Consul, RabbitMQ, Redis, and HashiCorp Vault locally on Kubernetes with Docker Desktop. This guide is designed for developers on Windows who want to run the full service stack locally for development, testing, and GitHub Actions workflow validation.

---

## Table of Contents

1. [Quick Start (5 minutes)](#quick-start-5-minutes)
2. [Architecture Overview](#architecture-overview)
3. [Service Details](#service-details)
4. [DNS Configuration](#dns-configuration)
5. [Using GitHub Actions Workflows Locally with act](#using-github-actions-workflows-locally-with-act)
6. [Docker Desktop Setup](#docker-desktop-setup)
7. [Bootstrap & Initialization](#bootstrap--initialization)
8. [Accessing Services Locally](#accessing-services-locally)
9. [Troubleshooting](#troubleshooting)
10. [Next Steps](#next-steps)

---

## Quick Start (5 minutes)

### Prerequisites

Before you begin, ensure you have the following installed:

- **Docker Desktop** (with Kubernetes enabled)
  - Download: https://www.docker.com/products/docker-desktop
  - Enable Kubernetes: Docker Desktop > Settings > Kubernetes > Enable Kubernetes
  
- **kubectl** (Kubernetes command-line tool)
  - Included with Docker Desktop, or install: https://kubernetes.io/docs/tasks/tools/
  
- **Vault CLI**
  - Download: https://www.vaultproject.io/downloads
  - Or via package manager: `choco install vault` (Chocolatey on Windows)
  
- **jq** (JSON processor)
  - Via Chocolatey: `choco install jq`
  - Or: https://stedolan.github.io/jq/download/
  
- **act** (Optional, for testing GitHub Actions workflows locally)
  - Via Chocolatey: `choco install act`
  - Or direct download: https://github.com/nektos/act/releases

### Deploy All Services

1. **Apply all Kubernetes manifests:**

```bash
# Create namespaces and deploy services
kubectl apply -f infrastructure/k8s/consul/
kubectl apply -f infrastructure/k8s/vault/
kubectl apply -f infrastructure/k8s/rabbitmq/
kubectl apply -f infrastructure/k8s/redis/

# Wait for all pods to be ready (2-3 minutes)
kubectl get pods -A --watch
```

2. **Bootstrap Vault:**

```bash
# Copy bootstrap script into Vault pod and execute
kubectl cp infrastructure/scripts/bootstrap-vault.sh vault/vault-0:/tmp/
kubectl exec -it vault/vault-0 -n vault -- bash /tmp/bootstrap-vault.sh

# Save the init.json output securely (contains unseal keys and root token)
kubectl cp vault/vault-0:/vault/bootstrap/init.json ./vault-init.json -n vault
cat vault-init.json
```

3. **Setup RabbitMQ with Vault:**

```bash
# Export Vault token and address
export VAULT_ADDR="http://vault.vault.svc.cluster.local:8200"
export VAULT_TOKEN=$(jq -r '.root_token' ./vault-init.json)
export RABBITMQ_ADMIN_PASSWORD="changeme"  # Change this to a secure password

# Copy setup script and execute
kubectl cp infrastructure/scripts/setup-rabbitmq-vault.sh rabbitmq/rabbitmq-0:/tmp/
kubectl exec -it rabbitmq/rabbitmq-0 -n rabbitmq -- bash /tmp/setup-rabbitmq-vault.sh
```

4. **Add DNS entries to Windows hosts file:**

Open `C:\Windows\System32\drivers\etc\hosts` in Administrator mode and add:

```
127.0.0.1       vault.vault.svc.cluster.local
127.0.0.1       consul.consul.svc.cluster.local
127.0.0.1       rabbitmq.rabbitmq.svc.cluster.local
127.0.0.1       redis-cluster.redis.svc.cluster.local
```

5. **Verify health:**

```bash
# Check all pods are running
kubectl get pods -A

# Vault status (via port-forward)
kubectl port-forward -n vault svc/vault 8200:8200 &
curl http://localhost:8200/v1/sys/health

# RabbitMQ status
kubectl port-forward -n rabbitmq svc/rabbitmq 15672:15672 &
curl http://admin:changeme@localhost:15672/api/overview

# Consul status
kubectl port-forward -n consul svc/consul 8500:8500 &
curl http://localhost:8500/v1/status/leader
```

**✅ You're done!** All services should now be running and accessible locally.

---

## Architecture Overview

This setup deploys four interconnected services in a local Kubernetes cluster:

```
┌─────────────────────────────────────────────────────────┐
│           Docker Desktop Kubernetes Cluster              │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │   Consul     │  │    Vault     │  │  RabbitMQ    │  │
│  │   (Service   │  │  (Secrets &  │  │ (Message     │  │
│  │  Registry)   │  │   Auth)      │  │  Broker)     │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│        8500             8200              5672/15672    │
│                                                           │
│  ┌──────────────────────────────────────────────────┐  │
│  │              Redis Cluster                       │  │
│  │         (Caching & Data Store)                   │  │
│  │              Port 6379                           │  │
│  └──────────────────────────────────────────────────┘  │
│                                                           │
└─────────────────────────────────────────────────────────┘
         Host Network (localhost / Windows)
```

**Service Dependencies:**
- **Vault** is the central secrets and authentication engine
- **RabbitMQ** retrieves credentials from Vault via agent injection
- **Consul** provides service discovery and health checking
- **Redis** acts as a distributed cache and data store
- All services use Kubernetes DNS for inter-service communication

---

## Service Details

### Consul (Service Registry & Health Checking)

**What it does:**
Consul provides service discovery, configuration management, and health checking for your distributed services. It maintains a catalog of all available services and their instances.

**Namespace:** `consul`
**Service DNS:** `consul.consul.svc.cluster.local`
**HTTP API Port:** 8500
**Serf LAN Port:** 8301

**Access the Consul UI:**
```bash
kubectl port-forward -n consul svc/consul 8500:8500
# Open: http://localhost:8500/ui/
```

**Health Check:**
```bash
kubectl exec -n consul consul-0 -- consul members
kubectl logs -n consul -l app=consul
```

**Configuration Notes:**
- Running as a single-node cluster locally (serf-lan for inter-node communication)
- Config file: `infrastructure/k8s/consul/config.yaml`
- For multi-node clustering, ensure all pods can communicate on port 8301

---

### HashiCorp Vault (Secrets Management & Authentication)

**What it does:**
Vault is the central repository for storing, encrypting, and accessing secrets. It provides dynamic secret generation, encryption-as-a-service, and identity-based access control for RabbitMQ and other services.

**Namespace:** `vault`
**Service DNS:** `vault.vault.svc.cluster.local`
**API Port:** 8200
**Storage:** Integrated storage (raft-based)

**Access Vault:**
```bash
# Port-forward to localhost
kubectl port-forward -n vault svc/vault 8200:8200 &

# Open UI: http://localhost:8200/ui/
# Or use CLI from within pod:
kubectl exec -it vault/vault-0 -n vault -- vault status
```

**Health Check:**
```bash
curl http://localhost:8200/v1/sys/health
# Returns HTTP 200 (unsealed) or 503 (sealed)
```

**Unseal & Login:**
```bash
# After bootstrap, unseal keys and root token are in:
cat vault-init.json | jq '.unseal_keys_b64' 
cat vault-init.json | jq '.root_token'

# Inside pod:
vault login <ROOT_TOKEN>
vault status
vault kv get rabbitmq/config
```

**Key Policies & Roles:**
- **AppRole**: Used by RabbitMQ to authenticate and retrieve credentials
- **RabbitMQ Policy**: Allows read/write access to `rabbitmq/config` path
- **Transit Engine**: Used to generate random bytes for Erlang cookie

**Configuration Notes:**
- Initialized via bootstrap script (see [Bootstrap & Initialization](#bootstrap--initialization))
- Uses 3 unseal keys (2 required to unseal)
- Root token and unseal keys stored in `/vault/bootstrap/init.json` on the pod
- Internal TLS not enabled (suitable for local development only)

---

### RabbitMQ (Message Broker)

**What it does:**
RabbitMQ is a distributed message broker that enables asynchronous communication between microservices. It retrieves admin credentials and the Erlang cookie from Vault via agent injection.

**Namespace:** `rabbitmq`
**Service DNS:** `rabbitmq.rabbitmq.svc.cluster.local`
**AMQP Port:** 5672
**Management UI Port:** 15672
**Replicas:** 3 (high availability cluster)

**Access RabbitMQ Management UI:**
```bash
kubectl port-forward -n rabbitmq svc/rabbitmq 15672:15672 &

# Open: http://localhost:15672/
# Default credentials (set in bootstrap): admin/<RABBITMQ_ADMIN_PASSWORD>
```

**Health Check:**
```bash
kubectl get pods -n rabbitmq
kubectl logs -n rabbitmq -l app=rabbitmq

# Via management API:
curl -u admin:changeme http://localhost:15672/api/overview
```

**Cluster Status:**
```bash
kubectl exec -it rabbitmq/rabbitmq-0 -n rabbitmq -- rabbitmqctl cluster_status
```

**Configuration Notes:**
- Credentials injected by Vault agent sidecar
- Erlang cookie (for clustering) is dynamically generated via Transit engine
- All 3 replicas must be healthy for cluster formation
- Management UI enabled in image configuration
- AMQP connectivity via service DNS or localhost port-forward

---

### Redis Cluster (Distributed Cache & Data Store)

**What it does:**
Redis is an in-memory data store used for caching, session storage, and fast data access. This deployment uses Redis Cluster mode for high availability and horizontal scaling.

**Namespace:** `redis`
**Service DNS:** `redis-cluster.redis.svc.cluster.local`
**Redis Port:** 6379
**Cluster Replicas:** 3 (configured for redundancy)

**Access Redis Cluster:**
```bash
# CLI access via pod
kubectl exec -it redis-cluster-0 -n redis -- redis-cli -c

# Or port-forward
kubectl port-forward -n redis pod/redis-cluster-0 6379:6379 &
redis-cli -h localhost
```

**Cluster Status:**
```bash
kubectl exec -it redis-cluster-0 -n redis -- redis-cli cluster info
kubectl exec -it redis-cluster-0 -n redis -- redis-cli cluster nodes
```

**Redis Insights (Web UI):**
```bash
# If Redis Insights is deployed
kubectl port-forward -n redis svc/redis-insights 8001:8001 &
# Open: http://localhost:8001/
```

**Health Check:**
```bash
kubectl get pods -n redis
kubectl logs -n redis -l app=redis-cluster

# Detailed cluster status
kubectl exec -it redis-cluster-0 -n redis -- redis-cli CLUSTER INFO
```

**Configuration Notes:**
- Cluster initialization via init job (`redis-cluster-init.yaml`)
- 3 master nodes with replicas for fault tolerance
- Persistent volumes optional (check manifests for `persistentVolumeClaim`)
- No authentication configured (suitable for local development)
- Data persists across pod restarts if volumes are configured

---

## DNS Configuration

### Why DNS Configuration is Needed

Inside the Kubernetes cluster, services are resolvable via Kubernetes DNS (e.g., `vault.vault.svc.cluster.local`). However, from your **Windows host machine**, these names won't resolve automatically. By adding entries to your hosts file, you enable external tools (kubectl port-forwards, local CLI clients) to reference services by their in-cluster names.

### Adding Entries to Windows Hosts File

1. **Open Notepad as Administrator:**
   - Press `Win + R`, type `notepad`, right-click, select "Run as administrator"

2. **Open the hosts file:**
   - File > Open, navigate to: `C:\Windows\System32\drivers\etc\hosts`
   - Set file type filter to "All Files (*.*)"

3. **Add the following entries:**

```
127.0.0.1       vault.vault.svc.cluster.local
127.0.0.1       consul.consul.svc.cluster.local
127.0.0.1       rabbitmq.rabbitmq.svc.cluster.local
127.0.0.1       redis-cluster.redis.svc.cluster.local
```

4. **Save and close** (Ctrl + S)

5. **Verify resolution:**

```bash
ping vault.vault.svc.cluster.local
# Should resolve to 127.0.0.1
```

### Docker Desktop & Container DNS

**Important Note for Windows:**
- When running commands **inside containers** (via `kubectl exec`), DNS resolution uses the Kubernetes DNS resolver, not your hosts file.
- When running commands **from your Windows host**, you use the hosts file or external DNS.
- If a container needs to reach a Windows service, use `host.docker.internal` instead of `localhost`.

### Verification

List all services and their DNS names:

```bash
kubectl get svc -A
# Output shows: <SERVICE>.<NAMESPACE>.svc.cluster.local
```

---

## Using GitHub Actions Workflows Locally with act

### What is act?

**act** is a tool that runs GitHub Actions workflows locally on your machine, inside Docker containers. This is invaluable for testing CI/CD pipelines before pushing to GitHub, especially for complex multi-step workflows.

**Benefits:**
- Test deployment workflows without pushing to GitHub
- Catch errors early in development
- Simulate the exact GitHub Actions environment locally
- Useful for debugging act-specific code paths

### Install act

**Windows (via Chocolatey):**
```bash
choco install act
```

**Or download directly:**
Visit https://github.com/nektos/act/releases and download the Windows binary.

### Run Workflows Locally

**List available workflows and jobs:**
```bash
cd infrastructure
act --list
# Shows all workflows and their jobs, e.g., deploy-vault-consul
```

**Run a specific workflow job:**
```bash
# Run the Vault and Consul deployment job
act -j deploy-vault-consul

# Run with verbose output for debugging
act -j deploy-vault-consul -v
```

**Run all jobs in a workflow:**
```bash
# Run the entire deploy-vault.yaml workflow
act -W .github/workflows/deploy-vault.yaml
```

### Act Workflow Detection

The workflows already include logic to detect when running under act:

```yaml
- name: Detect act
  id: detect_act
  run: |
    if [ "$ACT" = "true" ]; then
      echo "running_under_act=true" >> $GITHUB_ENV
    else
      echo "running_under_act=false" >> $GITHUB_ENV
    fi
```

This allows workflows to skip steps that don't work in local Docker environments (e.g., `curl` for kubectl installation when kubectl is already available in the act image).

### Using act with Docker Desktop

Ensure Docker Desktop is running before executing act commands:

```bash
# Run a specific workflow job
act -j deploy-redis --input redis_password=Password1

# List all available jobs
act --list
```

---

## Docker Desktop Setup

### Prerequisites

Ensure Docker Desktop is installed and Kubernetes is enabled:

1. **Install Docker Desktop:** https://www.docker.com/products/docker-desktop
2. **Enable Kubernetes:**
   - Docker Desktop > Settings > Kubernetes
   - Check **Enable Kubernetes**
   - Click **Apply & Restart**
   - Wait 2-3 minutes for Kubernetes to start

### Resource Allocation

Allocate sufficient resources for the full stack:

- **CPUs:** 4+ cores (6+ recommended)
- **Memory:** 8 GB+ (12+ GB recommended)
- **Disk:** 10 GB available

To adjust:
1. Docker Desktop > Settings > Resources
2. Adjust CPU and Memory sliders
3. Click **Apply & Restart**

---

## Bootstrap & Initialization

### Initialization Order

The services must be initialized in a specific order:

1. **Consul** → Starts service registry (no dependencies)
2. **Vault** → Starts secrets engine (depends on Consul optionally)
3. **RabbitMQ** → Starts message broker (depends on Vault for credentials)
4. **Redis** → Starts cache cluster (no external dependencies; init job must complete)

### What the Bootstrap Scripts Do

#### bootstrap-vault.sh

This script runs inside the Vault pod and:

1. **Initializes Vault** (if not already initialized)
   - Generates 5 unseal keys, requires 3 to unseal
   - Generates root token
   - Saves both to `/vault/bootstrap/init.json`

2. **Unseals Vault** using the first 3 unseal keys

3. **Logs in with root token**

4. **Creates RabbitMQ policy**
   - Grants read/write to `rabbitmq/config` path
   - Grants access to Transit engine for random bytes

5. **Enables AppRole authentication** (if not already enabled)
   - Creates AppRole role for RabbitMQ

**Outputs stored in `/vault/bootstrap/init.json`:**
```json
{
  "unseal_keys_b64": ["key1", "key2", "key3", ...],
  "root_token": "s.xxxxxxxxxxxx"
}
```

**Critical:** Save this file securely; you'll need the root token to manage Vault.

#### setup-rabbitmq-vault.sh

This script runs inside the RabbitMQ pod and:

1. **Checks Vault status** (ensures Vault is initialized)

2. **Creates RabbitMQ config in Vault KV store**
   - Stores admin username
   - Stores admin password (from `RABBITMQ_ADMIN_PASSWORD` env var)

3. **Generates Erlang cookie**
   - Uses Vault's Transit engine to generate random bytes
   - Stores in `rabbitmq/config`
   - Ensures all RabbitMQ replicas use the same cookie for clustering

4. **Confirms setup completion**

**Environment variables required:**
```bash
export VAULT_ADDR="http://vault.vault.svc.cluster.local:8200"
export VAULT_TOKEN="s.xxxxxxxxxxxx"  # Root token from bootstrap
export RABBITMQ_ADMIN_PASSWORD="your_secure_password"
```

### Running Bootstrap Manually

If the automated scripts fail or you need to re-run initialization:

```bash
# 1. Copy scripts to pods
kubectl cp infrastructure/scripts/bootstrap-vault.sh vault/vault-0:/tmp/ -n vault
kubectl cp infrastructure/scripts/setup-rabbitmq-vault.sh rabbitmq/rabbitmq-0:/tmp/ -n rabbitmq

# 2. Execute bootstrap
kubectl exec -it vault/vault-0 -n vault -- bash /tmp/bootstrap-vault.sh

# 3. Save init.json
kubectl cp vault/vault-0:/vault/bootstrap/init.json ./vault-init.json -n vault

# 4. Extract credentials
export VAULT_TOKEN=$(jq -r '.root_token' vault-init.json)
export RABBITMQ_ADMIN_PASSWORD="your_password"

# 5. Execute RabbitMQ setup
kubectl exec -it rabbitmq/rabbitmq-0 -n rabbitmq -- \
  bash -c "VAULT_ADDR=http://vault.vault.svc.cluster.local:8200 \
           VAULT_TOKEN=$VAULT_TOKEN \
           RABBITMQ_ADMIN_PASSWORD=$RABBITMQ_ADMIN_PASSWORD \
           bash /tmp/setup-rabbitmq-vault.sh"
```

### Retrieving Unseal Keys & Root Token

After bootstrap, retrieve the credentials:

```bash
# Copy init.json from Vault pod
kubectl cp vault/vault-0:/vault/bootstrap/init.json ./vault-init.json -n vault

# Extract root token
jq -r '.root_token' vault-init.json

# Extract unseal keys (if needed to manually unseal)
jq -r '.unseal_keys_b64[]' vault-init.json
```

**Security Warning:** Keep `vault-init.json` in a secure location (encrypted storage or secrets manager). Never commit it to version control.

---

## Accessing Services Locally

### Service Access Reference

| Service | Port | URL | Authentication |
|---------|------|-----|-----------------|
| **Vault** | 8200 | `http://localhost:8200/ui/` | Root token or AppRole |
| **RabbitMQ UI** | 15672 | `http://localhost:15672/` | `admin` / `<password>` |
| **RabbitMQ AMQP** | 5672 | `amqp://localhost:5672` | `admin` / `<password>` |
| **Consul UI** | 8500 | `http://localhost:8500/ui/` | None (local mode) |
| **Redis CLI** | 6379 | `redis-cli -h localhost` | None (local mode) |
| **Redis Insights** | 8001 | `http://localhost:8001/` | None (local mode) |

### Setting Up Port-Forwards

Port-forwarding allows you to access services from your Windows host as if they're running on `localhost`.

**Vault (Port 8200):**
```bash
kubectl port-forward -n vault svc/vault 8200:8200 &
# Access: http://localhost:8200/ui/
```

**RabbitMQ Management (Port 15672):**
```bash
kubectl port-forward -n rabbitmq svc/rabbitmq 15672:15672 &
# Access: http://localhost:15672/
# Credentials: admin / <RABBITMQ_ADMIN_PASSWORD>
```

**RabbitMQ AMQP (Port 5672):**
```bash
kubectl port-forward -n rabbitmq svc/rabbitmq 5672:5672 &
# Connect applications: amqp://localhost:5672
```

**Consul (Port 8500):**
```bash
kubectl port-forward -n consul svc/consul 8500:8500 &
# Access: http://localhost:8500/ui/
```

**Redis Cluster (Port 6379):**
```bash
kubectl port-forward -n redis svc/redis-cluster 6379:6379 &
redis-cli -h localhost
```

### Using Services from Inside the Cluster

If your application is running in the same Kubernetes cluster, use the service DNS names directly (no port-forward needed):

```bash
# Inside a pod, connect to Vault
curl http://vault.vault.svc.cluster.local:8200/v1/sys/health

# Inside a pod, connect to RabbitMQ
amqp://admin:password@rabbitmq.rabbitmq.svc.cluster.local:5672

# Inside a pod, connect to Redis
redis-cli -h redis-cluster.redis.svc.cluster.local -p 6379
```

---

## Troubleshooting

### Vault Issues

#### Problem: Vault Initialization Fails

**Symptoms:** Bootstrap script exits with "Vault is not initialized" error.

**Solution:**
```bash
# Check Vault pod status
kubectl logs -n vault vault-0

# Manually initialize Vault
kubectl exec -it vault/vault-0 -n vault -- vault operator init -format=json

# If already initialized, check status
kubectl exec -it vault/vault-0 -n vault -- vault status
```

#### Problem: Cannot Unseal Vault

**Symptoms:** `error reading unseal key: unseal_keys_b64 not found`

**Solution:**
```bash
# Verify init.json exists and is readable
kubectl exec -it vault/vault-0 -n vault -- cat /vault/bootstrap/init.json

# Manually unseal using extracted keys
KEYS=$(kubectl exec -it vault/vault-0 -n vault -- jq -r '.unseal_keys_b64[]' /vault/bootstrap/init.json)
for key in $KEYS; do
  kubectl exec -it vault/vault-0 -n vault -- vault operator unseal $key
done
```

#### Problem: AppRole Secret ID Generation Fails

**Symptoms:** RabbitMQ setup script fails: "AppRole secret generation failed"

**Solution:**
```bash
# Log into Vault with root token
kubectl exec -it vault/vault-0 -n vault -- vault login <ROOT_TOKEN>

# Manually enable AppRole
kubectl exec -it vault/vault-0 -n vault -- vault auth enable approle

# Recreate the RabbitMQ role
kubectl exec -it vault/vault-0 -n vault -- vault write auth/approle/role/rabbitmq-role \
  token_ttl=1h token_max_ttl=24h

# Generate new secret ID
kubectl exec -it vault/vault-0 -n vault -- vault write -f auth/approle/role/rabbitmq-role/secret-id
```

### RabbitMQ Issues

#### Problem: RabbitMQ Pods Not Starting

**Symptoms:** `kubectl get pods -n rabbitmq` shows `CrashLoopBackOff` or `Pending`

**Solution:**
```bash
# Check pod logs
kubectl logs -n rabbitmq rabbitmq-0 -c rabbitmq
kubectl logs -n rabbitmq rabbitmq-0 -c vault-agent  # Check Vault agent sidecar

# Check resource availability
kubectl top nodes
kubectl describe node  # Look for memory/CPU pressure

# Restart pod
kubectl delete pod -n rabbitmq rabbitmq-0
kubectl rollout restart statefulset/rabbitmq -n rabbitmq
```

#### Problem: RabbitMQ Cluster Not Forming

**Symptoms:** `rabbitmqctl cluster_status` shows inconsistent nodes

**Solution:**
```bash
# Check Erlang cookie consistency across pods
kubectl exec -it rabbitmq/rabbitmq-0 -n rabbitmq -- cat /var/lib/rabbitmq/.erlang.cookie
kubectl exec -it rabbitmq/rabbitmq-1 -n rabbitmq -- cat /var/lib/rabbitmq/.erlang.cookie

# If different, restart pods
kubectl rollout restart statefulset/rabbitmq -n rabbitmq

# Manually cluster if needed
kubectl exec -it rabbitmq/rabbitmq-1 -n rabbitmq -- rabbitmqctl stop_app
kubectl exec -it rabbitmq/rabbitmq-1 -n rabbitmq -- rabbitmqctl reset
kubectl exec -it rabbitmq/rabbitmq-1 -n rabbitmq -- rabbitmqctl join_cluster rabbit@rabbitmq-0.rabbitmq.rabbitmq.svc.cluster.local
kubectl exec -it rabbitmq/rabbitmq-1 -n rabbitmq -- rabbitmqctl start_app
```

#### Problem: Cannot Access RabbitMQ Management UI

**Symptoms:** `http://localhost:15672/` returns "connection refused"

**Solution:**
```bash
# Ensure port-forward is running
kubectl port-forward -n rabbitmq svc/rabbitmq 15672:15672 &

# Verify RabbitMQ is accepting connections
kubectl exec -it rabbitmq/rabbitmq-0 -n rabbitmq -- rabbitmq-diagnostics -q ping

# Check credentials
kubectl exec -it rabbitmq/rabbitmq-0 -n rabbitmq -- cat /etc/rabbitmq/rabbitmq.conf
```

### Redis Issues

#### Problem: Redis Cluster Unhealthy

**Symptoms:** `redis-cli cluster info` shows "cluster_state:fail"

**Solution:**
```bash
# Check cluster initialization job logs
kubectl logs -n redis -l app=redis-cluster-init

# Manually initialize cluster
kubectl exec -it redis-cluster-0 -n redis -- redis-cli --cluster create \
  127.0.0.1:6379 127.0.0.1:6379 127.0.0.1:6379 \
  127.0.0.1:6379 127.0.0.1:6379 127.0.0.1:6379 \
  --cluster-replicas 1 --cluster-yes

# Check cluster status
kubectl exec -it redis-cluster-0 -n redis -- redis-cli cluster nodes
```

#### Problem: Redis Data Lost After Pod Restart

**Symptoms:** Data disappears when Redis pods restart

**Solution:** Ensure Redis manifests have persistent volumes configured:

```yaml
volumeMounts:
  - name: redis-data
    mountPath: /data
volumeClaimTemplates:
  - metadata:
      name: redis-data
    spec:
      resources:
        requests:
          storage: 10Gi
```

### Consul Issues

#### Problem: Consul Cluster Unhealthy

**Symptoms:** `consul members` shows "failed" status

**Solution:**
```bash
# Check Consul logs
kubectl logs -n consul consul-0

# Check serf LAN port connectivity
kubectl exec -it consul/consul-0 -n consul -- consul members

# Restart Consul
kubectl delete pod -n consul consul-0
kubectl rollout restart statefulset/consul -n consul
```

### act Issues

#### Problem: act Command Not Found

**Symptoms:** `act: command not found`

**Solution:**
```bash
# Install via Chocolatey
choco install act

# Or download binary and add to PATH
# https://github.com/nektos/act/releases
```

#### Problem: act Cannot Access Docker

**Symptoms:** `error: docker daemon not running`

**Solution:** Ensure Docker Desktop is running

#### Problem: Script Line Ending Errors in act

**Symptoms:** `bad interpreter: No such file or directory`

**Solution:** Convert scripts to LF line endings (not CRLF)

### General Debugging

#### Check Pod Status
```bash
# All pods in all namespaces
kubectl get pods -A

# Detailed pod information
kubectl describe pod <pod-name> -n <namespace>

# Pod logs
kubectl logs <pod-name> -n <namespace>
kubectl logs <pod-name> -n <namespace> -c <container-name>  # Specific container

# Stream logs in real-time
kubectl logs -f <pod-name> -n <namespace>
```

#### Check Service Connectivity
```bash
# Exec into a pod
kubectl exec -it <pod-name> -n <namespace> -- /bin/bash

# Test connectivity to another service (from inside pod)
curl http://vault.vault.svc.cluster.local:8200/v1/sys/health
telnet rabbitmq.rabbitmq.svc.cluster.local 5672
```

#### Reset the Entire Stack
```bash
# Delete all namespaces (careful!)
kubectl delete ns consul rabbitmq redis vault

# Re-apply all manifests
kubectl apply -f infrastructure/k8s/

# Wait for pods to start
kubectl get pods -A --watch

# Re-bootstrap
kubectl cp infrastructure/scripts/bootstrap-vault.sh vault/vault-0:/tmp/ -n vault
kubectl exec -it vault/vault-0 -n vault -- bash /tmp/bootstrap-vault.sh
```

---

## Next Steps

### Local Development Workflow

1. **Edit manifests** in `infrastructure/k8s/`
2. **Apply changes:** `kubectl apply -f infrastructure/k8s/`
3. **Verify pods:** `kubectl get pods -A`
4. **Check logs:** `kubectl logs -f -n <namespace> <pod-name>`
5. **Iterate** until desired behavior is achieved

### Testing Deployment Workflows

1. **Update GitHub Actions workflows** in `infrastructure/.github/workflows/`
2. **Test locally with act:** `act -j <job-name> -v`
3. **Fix any issues** detected by act
4. **Push changes** to GitHub when confident

### Integrating with Your Application

Once the infrastructure is running locally:

1. **Connect your app pods** to the services using service DNS:
   - Vault: `http://vault.vault.svc.cluster.local:8200`
   - RabbitMQ: `amqp://admin:password@rabbitmq.rabbitmq.svc.cluster.local:5672`
   - Redis: `redis-cluster.redis.svc.cluster.local:6379`
   - Consul: `http://consul.consul.svc.cluster.local:8500`

2. **Access services from your Windows host** using port-forwards:
   ```bash
   kubectl port-forward -n <namespace> svc/<service> <local-port>:<remote-port>
   ```

3. **Debug applications** using:
   ```bash
   kubectl logs -f deployment/<your-app>
   kubectl exec -it pod/<your-app> -- /bin/bash
   ```

### Additional Resources

- **Kubernetes Documentation:** https://kubernetes.io/docs/
- **Vault Documentation:** https://www.vaultproject.io/docs
- **RabbitMQ Documentation:** https://www.rabbitmq.com/documentation.html
- **Redis Documentation:** https://redis.io/documentation
- **Consul Documentation:** https://www.consul.io/docs
- **act GitHub Repository:** https://github.com/nektos/act
- **Docker Desktop Docs:** https://docs.docker.com/desktop/

---

## License

See [LICENSE](LICENSE) for details.

---

**Happy developing!** If you encounter issues, check the [Troubleshooting](#troubleshooting) section or review pod logs with `kubectl logs`.