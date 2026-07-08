# OpenHub

A React starter for a role-gated internal console: Keycloak login → dashboard
of tiles → one app per tile (Help Desk, Administration, Product Management +
Warehouse, Ordering, CRM). You only see a tile if you have a role for it.

## Architecture (read this first)

Four of five modules here are **one app, modularly split** — not separate
deployable micro-frontends. Each lives in `src/modules/<name>` as its own
folder with its own routes inside it. They're lazy-loaded, so a user without
the `crm-user` role never even downloads the CRM module's code.

Why not true micro-frontends (Module Federation) for all of them? That buys
independent deploys per team, at the cost of real infra work (shared
dependency versions, a federation host config, more CI/CD surfaces, more
runtime failure modes). With 5 internal tools, it's not worth it for most of
them. The module boundaries here are already clean — if one team eventually
needs to ship their module on its own schedule, you split that one folder out
into its own app and federate it in, without touching the others.

**The Administration module is that fifth one** — it's a real,
independently-buildable Module Federation remote
([`OpenHub.Administration.UI`](../../Administration/OpenHub.Administration.UI), built with
Rspack, not Vite), not a folder in this repo. It's a worked example of what
"split that one folder out and federate it in" actually involves, including
every real failure hit doing it. See
[`docs/module-federation.md`](docs/module-federation.md) for the full
walkthrough — start there if you're about to federate a second module.

## How it works

- `src/keycloak.ts` — Keycloak client config (reads `.env`). The **realm is
  derived from the hostname**, not a fixed env var — see "Multi-tenant login"
  below.
- `src/auth/AuthProvider.tsx` — runs Keycloak login on load, exposes the
  logged-in user's roles, handles silent token refresh
- `src/auth/RoleGate.tsx` — wraps anything that needs a role check
- `src/config/modules.ts` — **the single source of truth.** Each module
  declares its id, route path, required role(s), and its component. The
  dashboard tiles, the sidebar links, and the router are all generated from
  this one list.
- `src/pages/Dashboard.tsx` — shows a tile for every module the current user
  has a role for
- `src/modules/*` — one folder per *locally-built* business app (Administration
  isn't here — see below):
  - `product/` — full catalog CRUD (list/create/edit, filters, lookups) —
    the reference example for a module with real API calls and per-tab
    permissions
  - `crm/` — a worked `DataTable` example with server-side pagination
    against a mocked API (swap for a real `fetch()` when there's a real CRM
    backend)
  - `helpdesk/`, `ordering/` — still placeholder pages, ready to be filled
    in the same way
- `src/federation/` — the contract this shell shares with the one federated
  remote (Administration). `src/router.tsx` lazy-loads it via
  `import("adminRemote/AdminApp")`, same `React.lazy` + `<Suspense>` pattern
  as every other module, just pointed at a URL instead of a local file. See
  [`docs/module-federation.md`](docs/module-federation.md).

## Multi-tenant login

OpenHub is multi-tenant via one Keycloak realm per organization. The realm to
log into is derived from the **hostname's subdomain** in `src/keycloak.ts` —
not a build-time env var — so the same deployed bundle serves every tenant:

- `client-a.openhub.com` → realm `client-a`
- `admin.openhub.com` → realm `platform-admin` (OpenHub staff only)
- plain `localhost` (no subdomain) → falls back to `VITE_KEYCLOAK_DEFAULT_REALM`

Locally, browsers resolve `*.localhost` to `127.0.0.1` with no `/etc/hosts`
edit, so `http://client-a.localhost:5173` and `http://admin.localhost:5173`
exercise different realms against the same running dev server.

## Getting started

```bash
cp .env.example .env       # fill in your Keycloak URL / client ID / default realm
npm install
npm run dev
```

You'll need a Keycloak client set up as a **public client** with standard
flow enabled, and redirect URIs covering `http://localhost:5173/*` (and
`http://*.localhost:5173/*` if you're testing multi-tenant login locally —
see "Multi-tenant login" above).

## Environment variables

All of these are read at **build time**, not runtime — three via Vite's `VITE_`-prefixed
`import.meta.env` convention, one (`ADMIN_REMOTE_URL`) directly off `process.env` since
`vite.config.ts` itself runs in Node, not the browser. Either way, Vite/the federation plugin
inline the resolved values into the compiled JS when you run `npm run build` (or `vite build`);
setting them as container/server environment variables on the already-built static files does
nothing. This is the most common way this trips people up, since it behaves differently from a
typical backend service's env vars. See "Docker" below for how the build args feed these in.

| Variable | Required | Example | Notes |
|---|---|---|---|
| `VITE_KEYCLOAK_URL` | Yes | `https://keycloak.example.com` | Base URL of your Keycloak server. No trailing slash. |
| `VITE_KEYCLOAK_CLIENT_ID` | Yes | `micro-ui-shell` | Must exist as a **public client** (standard flow enabled) in every tenant realm and in `platform-admin` — see "Getting started" above. Same client ID across all realms; only the realm changes per tenant. |
| `VITE_KEYCLOAK_DEFAULT_REALM` | Yes | `client-a` | Fallback realm used only when the hostname has no recognizable subdomain — see "Multi-tenant login" above. In production, real tenant traffic never hits this fallback (each tenant's subdomain resolves its own realm), but it still needs a valid value or login on the bare domain (health checks, a root/apex hit before DNS/routing is fully set up) will fail. |
| `ADMIN_REMOTE_URL` | Yes | `https://cdn.example.com/admin-ui/remoteEntry.js` | Where `OpenHub.Administration.UI`'s build is actually hosted — must be the same base URL that build's own `PUBLIC_PATH` was set to (see that project's README), plus `remoteEntry.js`. Read in `vite.config.ts`'s `federation({ remotes: { adminRemote: { entry: ... } } })`. Falls back to the local dev URL if unset — don't rely on that fallback for anything but local dev. |

Each Keycloak client's **Valid redirect URIs** need every production origin this bundle will
actually be served from — the wildcard subdomain pattern from "Multi-tenant login"
(`https://*.openhub.com/*`) plus the bare domain if you serve one, not just `localhost`.

**One more thing a real deployment needs that isn't a build-time env var:** `/pm` and `/adm` are
relative paths, not absolute URLs (see `src/services/*.ts`) — the SPA expects same-origin
reverse-proxy routing in front of it (`/pm/*` → `OpenHub.ProductManager.Api`, `/adm/*` →
`OpenHub.Administration.Api`), the same shape `vite.config.ts`'s dev-only `server.proxy` fakes
locally. Unlike the vars above, this one *is* a container-runtime concern, not a build-time one —
see "Docker" below, where it's `PRODUCT_MANAGER_API_URL`/`ADMINISTRATION_API_URL` substituted into
nginx's config on container start.

## Docker

```bash
docker build \
  --build-arg VITE_KEYCLOAK_URL=https://keycloak.example.com \
  --build-arg VITE_KEYCLOAK_CLIENT_ID=micro-ui-shell \
  --build-arg VITE_KEYCLOAK_DEFAULT_REALM=client-a \
  --build-arg ADMIN_REMOTE_URL=https://cdn.example.com/admin-ui/remoteEntry.js \
  -t openhub-shell .

docker run -p 80:80 \
  -e PRODUCT_MANAGER_API_URL=http://product-manager-api:8080 \
  -e ADMINISTRATION_API_URL=http://administration-api:8080 \
  openhub-shell
```

Two-stage build (`Dockerfile`): `node:20-alpine` builds the static bundle, `nginx:1.27-alpine`
serves it. Two different configuration mechanisms, matching the two different lifecycles above:

- **`--build-arg`s** (`VITE_KEYCLOAK_URL`, `VITE_KEYCLOAK_CLIENT_ID`, `VITE_KEYCLOAK_DEFAULT_REALM`,
  `ADMIN_REMOTE_URL`) — the build-time env vars from the table above. Baked into the image; a
  different environment (staging vs. production, or a changed `ADMIN_REMOTE_URL`) needs a
  rebuild, not just a different `docker run`.
- **`-e` / container env vars** (`PRODUCT_MANAGER_API_URL`, `ADMINISTRATION_API_URL`, and
  `DNS_RESOLVER`, all with dev-friendly defaults baked in — see `Dockerfile`) — read by nginx at
  **container start**, via `nginx.conf.template` and the base image's built-in envsubst-on-launch
  (anything under `/etc/nginx/templates/*.template`). Change these freely per environment without
  rebuilding the image, same image everywhere.
- `DNS_RESOLVER` defaults to `127.0.0.11` (Docker's embedded DNS — works out of the box under
  Compose/Swarm). Override it for Kubernetes (your cluster's CoreDNS/kube-dns service IP) or any
  other environment. This exists so nginx resolves `PRODUCT_MANAGER_API_URL`/
  `ADMINISTRATION_API_URL`'s hostnames at *request* time instead of once at container startup —
  without it, nginx refuses to start at all if the backing API isn't DNS-resolvable the instant
  this container boots (a real crash-loop risk if this container starts before those services do).

**No per-tenant container config, ever** — every tenant subdomain
(`client-a.openhub.com`, `admin.openhub.com`, ...) is served the exact same image/container;
realm selection happens entirely client-side, from the hostname (`src/keycloak.ts`, see
"Multi-tenant login" above). What has to be right at the infra layer instead: DNS/ingress routing
every tenant subdomain to this service, and a TLS cert covering all of them (wildcard, or one SAN
per tenant) — nginx itself (`nginx.conf.template`) doesn't need to know tenants exist.

## Roles used in this starter

| Role | Unlocks |
|---|---|
| `helpdesk-user`, `helpdesk-admin` | Help Desk |
| `platform-admin` | Administration → Organizations tab (cross-tenant; `platform-admin` realm only) |
| `tenant-admin` | Administration → Users tab (self-service, own organization only) |
| `product-manager` | Product Management → Catalog tab |
| `warehouse-staff` | Product Management → Warehouse tab |
| `ordering-user` | Ordering |
| `crm-user` | CRM |

Create these as **realm roles** in Keycloak (simplest) or as roles on the
`micro-ui-shell` client if you want them scoped to just this app. Assign them
to users or, better, to Keycloak groups and put users in groups.

Notice Product Management (and Administration) are unlocked by *either* role,
but each tab inside checks its own role — that's the pattern to copy when a
module has finer-grained permissions than "can see this app at all." A
`platform-admin` also implicitly satisfies anything a `tenant-admin` can do
(enforced server-side in `OpenHub.Administration.Api`, not just in the UI) —
the frontend gate here is only about which tabs render, not the source of
truth for what's actually allowed.

## Adding a new module

1. Create `src/modules/<name>/<Name>App.tsx`
2. Add one entry to `src/config/modules.ts` with its path, required role(s),
   and a `lazy(() => import(...))` of your component
3. Done — it now appears as a dashboard tile, a sidebar link, and a route,
   automatically gated by the role(s) you listed

If the module needs to be a genuinely separate, independently-deployable app instead (a team
that needs its own release schedule) — see
[`docs/module-federation.md`](docs/module-federation.md), which walks through exactly that using
Administration as the example.

## Dark mode

Toggled from the moon/sun icon in the header. Persisted in `localStorage`,
defaults to the visitor's OS preference on first visit. Implemented with
Tailwind's `dark:` class variant (`darkMode: "class"` in
`tailwind.config.js`) rather than the media-query default, so it's a real
user choice, not just "follows your OS."

## Responsive layout

The sidebar collapses into a hamburger-triggered drawer below the `lg`
breakpoint; the dashboard tile grid already scales from 1 → 2 → 3 columns.
Tailwind's breakpoints are min-width based, so unprefixed classes are the
mobile baseline and `sm:`/`lg:` scale up from there — that part comes free
with Tailwind, only the Shell's layout needed the explicit work.

## Account page

`/account` (linked from the username in the header) — reads the user's
name and roles straight from the token Keycloak already issued at login,
no extra API calls. It links out to Keycloak's own built-in Account
Console for actually changing profile info or password.

## Keycloak login theme

`keycloak-theme/` is a separate, standalone piece — not part of this React
app's build. It's a CSS theme for Keycloak itself, so the login page you
see *before* this app loads matches the same colors. See
`keycloak-theme/README.md` for how to install it.

## Data tables

`src/components/DataTable.tsx` — built on
[TanStack Table](https://tanstack.com/table) (headless; it's just sorting
and pagination logic, all the styling is the same plain Tailwind as
everything else). Two modes:

- **Client-side** (default) — pass the whole dataset, sorting/search/paging
  all happen in the browser. Fine up to a few thousand rows.
- **Server-side** — pass `manualPagination` (+ `pageIndex`, `pageCount`,
  `onPageChange`, `totalRows`) and `manualFiltering` (+ `searchValue`,
  `onSearchChange`). `data` becomes just the current page's rows; you own
  fetching them. `src/modules/crm/CrmApp.tsx` is a full worked example,
  including a mock API with simulated latency you can swap for a real
  `fetch()`.

**Per-row action buttons** (edit/delete/etc.) aren't a special prop — just
add a normal column whose `cell` renders buttons, same as the `actions`
column in the CRM example:

```tsx
{
  id: "actions",
  header: "",
  enableSorting: false,
  cell: ({ row }) => (
    <button onClick={() => handleEdit(row.original)}>
      <Pencil size={15} />
    </button>
  ),
}
```

## Icons

Uses [lucide-react](https://lucide.dev) — the modern, tree-shakeable
equivalent of Font Awesome for React (you only bundle the icons you
actually import, no icon-font file to load). Browse the full set at
[lucide.dev/icons](https://lucide.dev/icons), source on
[GitHub](https://github.com/lucide-icons/lucide), package on
[npm](https://www.npmjs.com/package/lucide-react).

Each module's icon is set once in `src/config/modules.ts` and is picked up
automatically by both the sidebar nav and the dashboard tiles:

```tsx
import { LifeBuoy } from "lucide-react";
// ...
{ id: "helpdesk", Icon: LifeBuoy, ... }
```

If you'd rather use Font Awesome instead (e.g. you already have a paid Pro
icon set), their React package is
[@fortawesome/react-fontawesome](https://github.com/FortAwesome/react-fontawesome)
— swap the `Icon` type/import in `modules.ts` and the `<m.Icon />` usages in
`Dashboard.tsx` and `Shell.tsx` accordingly.

## Branding

- **Colors**: indigo `#4A47C7` (primary/accent), amber `#F2A93B` (highlight,
  used sparingly), set in `tailwind.config.js`
- **Type**: Space Grotesk for headings, Inter for body — loaded from Google
  Fonts in `index.html` (swap for self-hosted files if you'd rather not
  depend on the CDN)
- **Logo**: `src/assets/logo-mark.svg` (icon only) and
  `logo-full-light.svg` / `logo-full-dark.svg` (mark + wordmark). The mark
  is a small hub-and-spoke glyph — five nodes around a center, echoing the
  five modules connecting into one shell
- **Favicons**: generated into `public/` (`favicon.ico` plus PNG sizes for
  modern browsers and iOS home screens)

## Where to go from here

- Swap the placeholder pages for real ones, or real API calls
- If you need permissions *within* a module (e.g. who can edit vs. view CRM
  records), check `hasRole(...)` inside that module rather than adding more
  top-level roles — see the Product Management tabs for the pattern
- If/when another module needs independent deploys, that's the point to
  introduce Module Federation for that single module — the rest of the app
  doesn't need to change. `docs/module-federation.md` walks through doing
  exactly this for Administration, including every real failure hit along
  the way
