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
