# OpenHub Administration UI

The Administration module (Organizations/Users management), federated into the
[`OpenHub.UI`](../OpenHub.UI) shell via [Module Federation 2.0](https://module-federation.io/).
Built with [Rspack](https://rspack.dev/), not Vite — this is the one module in the platform that's
a genuinely separate, independently buildable frontend artifact rather than a folder inside the
shell's own build. See [`OpenHub.UI/docs/module-federation.md`](../OpenHub.UI/docs/module-federation.md)
for the full story of why and how, including every real failure hit getting it working.

## Running standalone

```bash
npm install
npm run dev
```

Opens on `http://localhost:5175`. `src/bootstrap.tsx` renders `AdminApp` directly with a fake
auth context (`src/dev/fakeHostAuth.ts`) — no Keycloak, no shell needed. Real API calls will
come back `401` (the fake token isn't a real Keycloak JWT); this mode is for UI iteration, not
end-to-end testing.

## Running federated (the real integration)

This remote has no meaningful UI to show on its own in production — it's designed to be loaded
by the shell. Start both:

```bash
# this project
npm run dev

# in a second terminal, from src/OpenHub.UI
npm run dev
```

Then visit the shell at `http://localhost:5173` (or via Aspire — both are wired into
`OpenHub.Platform.AppHost`). Navigating to `/admin` triggers the shell's federated
`import("adminRemote/AdminApp")`.

## The one exposed surface

```
src/AdminApp.tsx    ← exposed as "./AdminApp" (see rspack.config.ts `exposes`)
```

Everything else under `src/` is a private implementation detail — the shell never imports it
directly, and it's free to change without touching the shell's code. `AdminApp` takes a single
`auth: HostAuthContext` prop (`src/federation/HostAuthContext.ts`) and owns its own nested routing
(Organizations/Users) internally via `react-router-dom`.

## Build

```bash
npm run build
```

Produces `dist/remoteEntry.js` (the federation container), the exposed `AdminApp` chunk, and
`dist/mf-manifest.json`. Whatever serves this output in production must send
`Access-Control-Allow-Origin` for the shell's real origin(s) — see the CORS section in
[`module-federation.md`](../OpenHub.UI/docs/module-federation.md).

## Environment variables

Unlike [`OpenHub.UI`](../OpenHub.UI)'s three `VITE_`-prefixed vars, this project has no `.env`
file — there's exactly one, `PUBLIC_PATH`, read as plain `process.env.PUBLIC_PATH` in
`rspack.config.ts` (not `import.meta.env`; that's a Vite-specific convention, this is Rspack).
Two things about this project's design keep the list this short:

- **No Keycloak dependency.** This remote never logs anyone in — the shell is always the
  authenticated parent and hands down a `HostAuthContext` (roles, realm, a `getToken()` function)
  as a plain prop when it mounts `AdminApp`. Check `package.json`: `keycloak-js` isn't a
  dependency here. See "Auth: who logs in, who just consumes" in
  [`module-federation.md`](../OpenHub.UI/docs/module-federation.md) for the full contract.
- **No API base URL to configure.** `src/services/administrationApi.ts` hits a hardcoded relative
  `/adm` prefix, same shape as the shell's `/pm`/`/adm` — same-origin reverse-proxy routing is
  expected in front of it, not a configurable base URL.

| Variable | Required | Example | Notes |
|---|---|---|---|
| `PUBLIC_PATH` | Yes (for production) | `https://cdn.example.com/admin-ui/` | Base URL every chunk this remote emits (`remoteEntry.js`, the exposed `AdminApp` chunk, its CSS) resolves against once loaded into a browser — has to be wherever this build's output actually gets hosted. Falls back to the local dev URL if unset — don't rely on that fallback for anything but local dev. **The shell has to be told the same URL**: `OpenHub.UI`'s `ADMIN_REMOTE_URL` build arg needs to be this exact value plus `remoteEntry.js` — see that project's README. Like `VITE_` vars, this is read at **build time** (`npm run build`); setting it on an already-built container does nothing. |

Like the shell, this is a **build-time-only** value — one image per `PUBLIC_PATH`, not something
a running container can be pointed at a different URL without a rebuild. A true runtime-
configurable public path is possible in Rspack/webpack (setting `__webpack_public_path__` off a
value read from `window` before the async boundary in `src/index.tsx`), just not implemented
here — see `docs/module-federation.md`'s "What's simplified here" for why this project stays at
that simplicity level.

`devServer.*` (port, CORS `headers`, `allowedHosts`, the `/adm` proxy) only applies to
`rspack serve` — none of it is present in a `rspack build` output. Production CORS is a
container-runtime concern instead — see "Docker" below.

## Docker

```bash
docker build \
  --build-arg PUBLIC_PATH=https://cdn.example.com/admin-ui/ \
  -t openhub-admin-ui .

docker run -p 80:80 \
  -e ALLOWED_ORIGIN_REGEX='^https://[a-z0-9-]+\.openhub\.com$' \
  openhub-admin-ui
```

Two-stage build (`Dockerfile`): `node:20-alpine` builds the static bundle, `nginx:1.27-alpine`
serves it with CORS headers. Two different configuration mechanisms, matching the two different
lifecycles:

- **`--build-arg PUBLIC_PATH`** — baked into the image at build time (see table above). A
  different `PUBLIC_PATH` needs a rebuild, not just a different `docker run`.
- **`-e ALLOWED_ORIGIN_REGEX`** — read by nginx at **container start**, via
  `nginx.conf.template` and the base image's built-in envsubst-on-launch. A `map` block reflects
  the request's `Origin` header back as `Access-Control-Allow-Origin` only when it matches this
  regex — a fixed origin string can't cover every tenant subdomain the shell is served from (see
  the CORS section in [`module-federation.md`](../OpenHub.UI/docs/module-federation.md)). Change
  this freely per environment without rebuilding the image.

No SPA fallback in `nginx.conf.template` (unlike the shell's) — this container only ever serves
federation chunks fetched by `import()`/`<link>`/`<script>`, never a browser navigation to a
route, so there's no client-side router to fall back for.
