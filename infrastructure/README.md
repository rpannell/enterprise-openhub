# Enterprise Kubernetes Platform Context & Architecture

## Overview

I am building an enterprise-grade Kubernetes platform that runs locally on k3s (WSL2) and will later deploy to AKS using GitHub Actions.

The platform is infrastructure-first, production-oriented, Helm-based, and must be reproducible between:

* Local development using ACT (GitHub Actions runner locally)
* Production CI/CD using GitHub Actions targeting AKS

The goal is to fully establish the platform infrastructure before application development begins.

---

# Critical Platform Requirements

* ALL components must be production-ready by design
* Clustering must be enabled wherever supported
* Replica counts MUST be configurable via Helm values.yaml
* No hardcoded replica counts in templates
* Prefer HA deployments whenever practical
* Use OFFICIAL Helm charts whenever possible
* DO NOT use Bitnami charts
* Prefer vendor-supported or CNCF-recognized charts
* Prefer Operators over Helm charts when they provide superior HA, scaling, or lifecycle management
* One source of truth for deployments (ACT and GitHub Actions)
* Minimal namespaces with clear ownership boundaries
* ServiceMonitors are attached per module
* TLS enabled by default where practical
* Certificates managed through cert-manager
* DNS automation supported through ExternalDNS
* Infrastructure should be fully deployable before application development begins
* Application-to-application communication should support Istio service mesh
* Service mesh configuration should be managed as code
* Applications should be deployable with or without service mesh participation via Helm values
* Helm values.yaml should be the single source of truth for:

  * replica counts
  * resources
  * persistence
  * storage classes
  * affinity rules
  * service mesh participation

Example:

```yaml
replicaCount: 3

mesh:
  enabled: true
```

---

# Core Platform Components

## Foundation Services

* cert-manager
* ExternalDNS

## Security & Identity

* Vault
* Keycloak
* Consul

## Data Platform

* PostgreSQL
* Redis
* RabbitMQ
* MinIO
* Elasticsearch

## Edge Platform

* Kong Gateway

## Service Mesh Platform

* Istio
* Kiali

## Observability Platform

* Prometheus
* Alertmanager
* Grafana
* Loki
* Tempo
* Mimir

## Application Platform

* .NET APIs
* Worker Services
* Event Processors
* Background Jobs

---

# Namespace Strategy

The platform intentionally uses a small number of namespaces.

## platform-system

Purpose:
Core platform and stateful infrastructure.

Components:

* cert-manager
* ExternalDNS
* PostgreSQL
* Redis
* RabbitMQ
* MinIO
* Elasticsearch

Ownership:

* Platform Engineering

---

## platform-security

Purpose:
Identity, secrets, authentication, authorization, and service configuration.

Components:

* Vault
* Keycloak
* Consul

Potential future additions:

* External Secrets Operator
* OPA Gatekeeper
* SPIRE

Ownership:

* Platform Engineering
* Security Engineering

---

## platform-edge

Purpose:
Ingress, API management, and north/south traffic.

Components:

* Kong Gateway

Ownership:

* Platform Engineering

---

## platform-observability

Purpose:
Metrics, logging, tracing, dashboards, and alerting.

Components:

* Prometheus
* Alertmanager
* Grafana
* Loki
* Tempo
* Mimir

Potential future additions:

* OpenTelemetry Collector
* Pyroscope

Ownership:

* Platform Engineering
* SRE

---

## platform-mesh

Purpose:
Service-to-service communication, traffic management, and service security.

Components:

* Istio Base CRDs
* Istiod
* Kiali

Potential future additions:

* Istio Ambient Mesh
* Envoy extensions
* WASM plugins

Ownership:

* Platform Engineering
* SRE

---

## apps

Purpose:
Business applications.

Components:

* .NET APIs
* Workers
* Event Processors
* Scheduled Jobs

Ownership:

* Application Teams

---

# Networking & TLS Principles

* TLS is the default posture
* cert-manager manages certificate lifecycle
* Certificates should not be manually managed where cert-manager can be used
* Kong is the primary ingress and API gateway
* All ingress resources support TLS
* ExternalDNS manages cloud DNS records
* Local k3s environments should preserve the same ingress and TLS patterns used in AKS

---

# Cluster Design Principles

## Stateful Systems

* PostgreSQL should use CloudNativePG Operator
* RabbitMQ should use RabbitMQ Cluster Operator
* Redis should support clustering where practical
* MinIO should support distributed mode
* Elasticsearch should be deployed as a multi-node cluster
* Vault should support HA mode

## Stateless Systems

* Keycloak should support multiple replicas
* Kong should support horizontal scaling
* Istiod should support HA deployment
* Future application services should support horizontal scaling

All scaling parameters should be defined through Helm values.

---

# Deployment Order

## Phase 0 — Foundation & Observability Bootstrap

Namespaces:

* platform-system
* platform-observability

Cluster Foundation:

* cert-manager
* ExternalDNS

cert-manager responsibilities:

* Certificate lifecycle management
* Automatic renewal
* ClusterIssuer management
* Let's Encrypt integration
* Internal issuers

ExternalDNS responsibilities:

* Automated DNS record management
* Cloud DNS integration
* Ingress-driven DNS registration

Observability Bootstrap:

* Prometheus Operator
* Prometheus (without Grafana initially)
* kube-state-metrics
* node-exporter
* Alertmanager

Requirements:

* Grafana is NOT installed yet
* Metrics collection must exist before platform services
* ServiceMonitors should be available from day one

---

## Phase 1 — Security, Identity & Configuration

Namespace:

* platform-security

Components:

* Vault (HA)
* Keycloak (multi-replica)
* Consul

Recommended integrations:

* Vault Agent Injector
* Consul Connect (optional)

---

## Phase 2 — Data Platform

Namespace:

* platform-system

Components:

* PostgreSQL (CloudNativePG)
* Redis
* MinIO
* RabbitMQ Cluster Operator

Requirements:

* Persistent storage
* HA where applicable
* Metrics exposed
* ServiceMonitors configured

---

## Phase 3 — Edge Platform

Namespace:

* platform-edge

Components:

* Kong Gateway

Requirements:

* Keycloak OIDC integration
* Vault integration
* TLS through cert-manager
* Authentication plugins
* Authorization plugins
* Rate limiting
* JWT validation

---

## Phase 4 — Observability Expansion

Namespace:

* platform-observability

Components:

* Grafana
* Loki
* Tempo
* Mimir

Requirements:

* Integrated with Prometheus
* MinIO used as object storage backend where appropriate
* Dashboards provisioned as code

---

## Phase 5 — Service Mesh Platform

Namespace:

* platform-mesh

Components:

* Istio Base
* Istiod
* Kiali

Requirements:

* Prometheus integration
* Grafana integration
* Tempo tracing integration
* mTLS enabled by default
* Namespace-based sidecar injection
* Metrics exposed through Prometheus

Purpose:

* Service-to-service security
* Traffic management
* Observability enhancement
* Foundation for application networking

---

## Phase 6 — Search & Analytics Infrastructure

Namespace:

* platform-system

Components:

* Elasticsearch cluster

Requirements:

* Multi-node deployment
* Persistent storage
* Monitoring integration
* Production-ready configuration

Purpose:

* Search
* Analytics
* Future application indexing needs

---

## Phase 7 — Application Platform

Namespace:

* apps

Only after all infrastructure is operational.

Components:

* .NET APIs
* Workers
* Event Processors
* Background Services

Application requirements:

* Prometheus metrics
* Keycloak integration
* Vault integration
* Loki logging
* Tempo tracing
* Istio mesh participation (configurable)
* TLS support through cert-manager
* Scaling through Helm values

---

# Operator & CRD Requirements

Operator CRDs must be installed before dependent resources.

Examples:

* cert-manager CRDs before Certificates
* Prometheus Operator CRDs before ServiceMonitors
* RabbitMQ Cluster Operator before RabbitMQ clusters
* CloudNativePG before PostgreSQL clusters
* Istio Base CRDs before Istiod
* Any future operator CRDs before dependent custom resources

Deployment workflows should validate CRD availability before proceeding.

---

# CI/CD Requirements

I need help designing:

* GitHub Actions reusable workflows per module
* A master orchestration workflow ("god pipeline")
* ACT compatibility with GitHub Actions
* Helm-based modular deployment patterns
* Dependency-aware deployment sequencing
* Environment-specific values files
* ServiceMonitor deployment per module
* Rollback strategies
* Health validation between phases
* Operator-based deployment patterns where appropriate

GitHub Actions must remain the source of truth, with ACT executing the same workflows locally.

---

# Current Stage

I am currently designing the CI/CD architecture using:

* GitHub Actions
* ACT
* Helm
* k3s locally
* AKS in cloud environments

I need a production-grade structure for:

* reusable workflows
* orchestration workflows
* dependency ordering
* module-level Helm deployments
* ACT and GitHub Actions parity
* long-term maintainability and scalability
* infrastructure-first deployment automation

```
```
