# syntax=docker/dockerfile:1

# ---- Build ----
# Vite inlines VITE_ vars into the JS bundle at build time (import.meta.env), and the
# federation plugin bakes ADMIN_REMOTE_URL into the same bundle at build time too (see
# vite.config.ts) — none of these are readable/settable on the running container later.
# See README.md "Environment variables" for what each one does and why.
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ARG VITE_KEYCLOAK_URL
ARG VITE_KEYCLOAK_CLIENT_ID
ARG VITE_KEYCLOAK_DEFAULT_REALM
ARG ADMIN_REMOTE_URL
ENV VITE_KEYCLOAK_URL=$VITE_KEYCLOAK_URL \
    VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID \
    VITE_KEYCLOAK_DEFAULT_REALM=$VITE_KEYCLOAK_DEFAULT_REALM \
    ADMIN_REMOTE_URL=$ADMIN_REMOTE_URL

RUN npm run build

# ---- Serve ----
FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template

# Runtime env vars (unlike everything above) — substituted into nginx.conf.template on
# container start by the base image's built-in entrypoint. Same-origin reverse-proxy targets
# for /pm and /adm, which the app itself hardcodes as relative paths (see README.md).
# No tenant/realm config here: realm selection is 100% client-side, from the request
# hostname (src/keycloak.ts) — this container serves the identical bundle to every tenant
# subdomain, so nothing about multi-tenancy needs to be set per-container. What DOES need to
# be right at the infra layer: DNS/ingress routing every tenant subdomain to this same
# service, and a TLS cert covering them (wildcard, or SAN per tenant).
ENV PRODUCT_MANAGER_API_URL=http://product-manager-api:8080 \
    ADMINISTRATION_API_URL=http://administration-api:8080 \
    DNS_RESOLVER=127.0.0.11

EXPOSE 80
