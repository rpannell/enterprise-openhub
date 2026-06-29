# OpenHub

A React starter for a role-gated internal console: Keycloak login → dashboard
of tiles → one app per tile (Help Desk, Administration, Product Management +
Warehouse, Ordering, CRM). You only see a tile if you have a role for it.

## Architecture (read this first)

This is **one app, modularly split** — not separate deployable
micro-frontends. Each business area lives in `src/modules/<name>` as its own
folder with its own routes inside it. They're lazy-loaded, so a user without
the `crm-user` role never even downloads the CRM module's code.

Why not true micro-frontends (Module Federation)? That buys independent
deploys per team, at the cost of real infra work (shared dependency
versions, a federation host config, more CI/CD surfaces, more runtime
failure modes). With 5 internal tools, it's not worth it yet. The module
boundaries here are already clean — if one team eventually needs to ship
their module on its own schedule, you split that one folder out into its own
app and federate it in, without touching the others.

## How it works

- `src/keycloak.ts` — Keycloak client config (reads `.env`)
- `src/auth/AuthProvider.tsx` — runs Keycloak login on load, exposes the
  logged-in user's roles, handles silent token refresh
- `src/auth/RoleGate.tsx` — wraps anything that needs a role check
- `src/config/modules.ts` — **the single source of truth.** Each module
  declares its id, route path, required role(s), and its component. The
  dashboard tiles, the sidebar links, and the router are all generated from
  this one list.
- `src/pages/Dashboard.tsx` — shows a tile for every module the current user
  has a role for
- `src/modules/*` — one folder per business app, each just a placeholder
  page right now

## Getting started

```bash
cp .env.example .env       # fill in your Keycloak URL / realm / client ID
npm install
npm run dev
```

You'll need a Keycloak client set up as a **public client** with standard
flow enabled, and redirect URIs covering `http://localhost:5173/*`.

## Roles used in this starter

| Role | Unlocks |
|---|---|
| `helpdesk-user`, `helpdesk-admin` | Help Desk |
| `system-admin` | Administration |
| `product-manager` | Product Management → Catalog tab |
| `warehouse-staff` | Product Management → Warehouse tab |
| `ordering-user` | Ordering |
| `crm-user` | CRM |

Create these as **realm roles** in Keycloak (simplest) or as roles on the
`micro-ui-shell` client if you want them scoped to just this app. Assign them
to users or, better, to Keycloak groups and put users in groups.

Notice Product Management is unlocked by *either* role, but each tab inside
it checks its own role — that's the pattern to copy when a module has
finer-grained permissions than "can see this app at all."

## Adding a new module

1. Create `src/modules/<name>/<Name>App.tsx`
2. Add one entry to `src/config/modules.ts` with its path, required role(s),
   and a `lazy(() => import(...))` of your component
3. Done — it now appears as a dashboard tile, a sidebar link, and a route,
   automatically gated by the role(s) you listed

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
- If/when one module needs independent deploys, that's the point to
  introduce Module Federation for that single module — the rest of the app
  doesn't need to change
