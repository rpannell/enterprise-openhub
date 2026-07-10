# OpenHub Product Management UI

The Product Management module (catalog, pricing, and warehouse inventory), federated into the
[`OpenHub.UI`](../../Shell/OpenHub.UI) shell via [Module Federation 2.0](https://module-federation.io/).
Built with [Rspack](https://rspack.dev/), not Vite — same as Administration and CRM, this is a
genuinely separate, independently buildable frontend artifact rather than a folder inside the
shell's own build. See [`OpenHub.UI/docs/module-federation.md`](../../Shell/OpenHub.UI/docs/module-federation.md)
for the full story of why and how, including every real failure hit getting the pattern working.

This project used to be a plain folder inside the shell (`src/Shell/OpenHub.UI/src/modules/product`),
built straight into the shell's own bundle. It's been spliced off into its own remote here,
following the same shape as [`OpenHub.Administration.UI`](../../Administration/OpenHub.Administration.UI)
and [`OpenHub.CRM.UI`](../../CRM/OpenHub.CRM.UI).

## Running standalone

```bash
npm install
npm run dev
```

Opens on `http://localhost:5177`. `src/bootstrap.tsx` renders `ProductApp` directly with a fake
auth context (`src/dev/fakeHostAuth.ts` — roles `product-manager` and `warehouse-staff`) — no
Keycloak, no shell needed. Real API calls will come back `401` (the fake token isn't a real
Keycloak JWT); this mode is for UI iteration, not end-to-end testing.

## Running federated (the real integration)

This remote has no meaningful UI to show on its own in production — it's designed to be loaded
by the shell. Start both:

```bash
# this project
npm run dev

# in a second terminal, from src/Shell/OpenHub.UI
npm run dev
```

Then visit the shell at `http://localhost:5173` (or via Aspire — both are wired into
`OpenHub.Platform.AppHost`, as `product-manager-ui-remote`). Navigating to `/product` triggers
the shell's federated `import("productRemote/ProductApp")`.

## The one exposed surface

```
src/ProductApp.tsx    ← exposed as "./ProductApp" (see rspack.config.ts `exposes`)
```

Everything else under `src/` is a private implementation detail — the shell never imports it
directly, and it's free to change without touching the shell's code. `ProductApp` takes a single
`auth: HostAuthContext` prop (`src/federation/HostAuthContext.ts`) and owns its own nested routing
(Catalog list/create/edit) internally via `react-router-dom`, mounted under its own
`<BrowserRouter basename="/product">`.

The **Warehouse** tab is shown to anyone with the `warehouse-staff` role, same as before this
module was federated — there's no Warehouse screen behind it yet (pre-existing gap, not something
introduced by the split).

## Build

```bash
npm run build
```

Produces `dist/remoteEntry.js` (the federation container), the exposed `ProductApp` chunk, and
`dist/mf-manifest.json`. Whatever serves this output in production must send
`Access-Control-Allow-Origin` for the shell's real origin(s) — see the CORS section in
[`module-federation.md`](../../Shell/OpenHub.UI/docs/module-federation.md).

## Testing

```bash
npm test          # vitest — unit tests (components, the runtime context, the API client)
npm run test:e2e  # playwright — standalone-harness flows + MF-boundary (CORS/container-shape) checks
```

`npm run test:e2e` needs Playwright's browser installed once: `npx playwright install --with-deps chromium`.
See `playwright.config.ts` for why the e2e suite talks to this remote's own dev server directly
rather than driving it through the shell (the shell's Keycloak login gate has no test bypass yet —
same open question flagged for Administration.UI and CRM.UI).

## Environment variables

Unlike [`OpenHub.UI`](../../Shell/OpenHub.UI)'s three `VITE_`-prefixed vars, this project has no `.env`
file — there's exactly one, `PUBLIC_PATH`, read as plain `process.env.PUBLIC_PATH` in
`rspack.config.ts` (not `import.meta.env`; that's a Vite-specific convention, this is Rspack).
Two things about this project's design keep the list this short:

- **No Keycloak dependency.** This remote never logs anyone in — the shell is always the
  authenticated parent and hands down a `HostAuthContext` (roles, realm, a `getToken()` function)
  as a plain prop when it mounts `ProductApp`. Check `package.json`: `keycloak-js` isn't a
  dependency here. See "Auth: who logs in, who just consumes" in
  [`module-federation.md`](../../Shell/OpenHub.UI/docs/module-federation.md) for the full contract.
- **No API base URL to configure.** `src/services/productManagerApi.ts` hits a hardcoded relative
  `/pm` prefix, same shape as the shell's old in-bundle version and Administration's `/adm` —
  same-origin reverse-proxy routing is expected in front of it, not a configurable base URL.

| Variable | Required | Example | Notes |
|---|---|---|---|
| `PUBLIC_PATH` | Yes (for production) | `https://cdn.example.com/product-ui/` | Base URL every chunk this remote emits (`remoteEntry.js`, the exposed `ProductApp` chunk, its CSS) resolves against once loaded into a browser — has to be wherever this build's output actually gets hosted. Falls back to the local dev URL (`http://localhost:5177/`) if unset — don't rely on that fallback for anything but local dev. **The shell has to be told the same URL**: `OpenHub.UI`'s `PRODUCT_REMOTE_URL` build arg needs to be this exact value plus `remoteEntry.js` — see that project's README/`vite.config.ts`. Like `VITE_` vars, this is read at **build time** (`npm run build`); setting it on an already-built container does nothing. |

Like Administration and CRM, this is a **build-time-only** value — one image per `PUBLIC_PATH`,
not something a running container can be pointed at a different URL without a rebuild. A true
runtime-configurable public path is possible in Rspack/webpack (setting `__webpack_public_path__`
off a value read from `window` before the async boundary in `src/index.tsx`), just not
implemented here — see `docs/module-federation.md`'s "What's simplified here" for why this
project stays at that simplicity level.

`devServer.*` (port, CORS `headers`, `allowedHosts`, the `/pm` proxy) only applies to
`rspack serve` — none of it is present in a `rspack build` output. Production CORS is a
container-runtime concern instead — see "Docker" below.

## Docker

```bash
docker build \
  --build-arg PUBLIC_PATH=https://cdn.example.com/product-ui/ \
  -t openhub-productmanager-ui .

docker run -p 80:80 \
  -e ALLOWED_ORIGIN_REGEX='^https://[a-z0-9-]+\.openhub\.com$' \
  openhub-productmanager-ui
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
  the CORS section in [`module-federation.md`](../../Shell/OpenHub.UI/docs/module-federation.md)). Change
  this freely per environment without rebuilding the image.

No SPA fallback in `nginx.conf.template` (unlike the shell's) — this container only ever serves
federation chunks fetched by `import()`/`<link>`/`<script>`, never a browser navigation to a
route, so there's no client-side router to fall back for.

## Deploying to a new environment (checklist)

1. Build and push the image, e.g.:
   ```bash
   docker build --build-arg PUBLIC_PATH=https://<product-ui-host>/ -t <registry>/openhub-productmanager-ui:<tag> .
   docker push <registry>/openhub-productmanager-ui:<tag>
   ```
2. Run it somewhere reachable over HTTPS from wherever the shell is served, with
   `ALLOWED_ORIGIN_REGEX` set to match the shell's real origin(s) (e.g. `^https://[a-z0-9-]+\.<env>\.openhub\.com$`).
3. Point the shell at it: rebuild `OpenHub.UI` with `--build-arg PRODUCT_REMOTE_URL=https://<product-ui-host>/remoteEntry.js`
   (see `OpenHub.UI/vite.config.ts` and its own README).
4. Confirm `OpenHub.ProductManager.Api` is reachable at the path the shell's reverse proxy maps
   to `/pm` in that environment — this remote issues plain relative `/pm/api/v1/...` fetches, it
   doesn't know or care where the API physically runs.
5. Smoke-test: load the shell, sign in as a user with `product-manager` and/or `warehouse-staff`,
   navigate to `/product`, confirm the catalog list loads and Create/Edit work.
