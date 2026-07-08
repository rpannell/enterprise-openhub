# syntax=docker/dockerfile:1

# ---- Build ----
# PUBLIC_PATH is baked into the compiled JS at build time (see rspack.config.ts's
# `output.publicPath` and README.md "Environment variables") — the same build-time-only
# pattern as the shell's VITE_ vars, just without an env-var-prefix convention of its own
# (this is a plain rspack/webpack config value, not something Rspack reads a special prefix
# for). One image per publicPath: setting this on the running container later does nothing.
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ARG PUBLIC_PATH
ENV PUBLIC_PATH=$PUBLIC_PATH

RUN npm run build

# ---- Serve ----
FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template

# Runtime env var — substituted into nginx.conf.template on container start. This remote's
# assets are always fetched cross-origin by whatever shell federates it (see the CORS section
# in OpenHub.UI/docs/module-federation.md); a regex (not a fixed origin) so every tenant
# subdomain the shell is served from is covered, same shape as the dev server's
# devServer.allowedHosts/headers logic in rspack.config.ts — which only applies to `rspack
# serve` and has no effect on this build's output, so it has to be re-enforced here instead.
ENV ALLOWED_ORIGIN_REGEX="^https://[a-z0-9-]+\.openhub\.com$"

EXPOSE 80
