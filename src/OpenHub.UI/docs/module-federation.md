# Module Federation walkthrough: turning the Admin module into a real remote

This is a worked example of the thing `README.md`'s "Architecture" section says the rest of
this app deliberately *isn't* doing: a genuine, independently-buildable, independently-deployable
micro-frontend, wired in via [Module Federation 2.0](https://module-federation.io/). Every other
module in this app (Product, CRM, Help Desk, Ordering) is a folder in this repo, part of one Vite
build. The Administration module is not — it lives in a separate project,
[`OpenHub.Administration.UI`](../../OpenHub.Administration.UI), built with a different bundler
(Rspack), and the shell (`OpenHub.UI`, this project) loads it at runtime from a URL, the same way
it would load a real deployed artifact.

The goal here is to make the earlier "what would it take, and what's the infra headache"
conversation concrete — everything below actually happened building this one remote, including
the four real failures hit along the way and how each was diagnosed.

## The shape of it

```
OpenHub.UI (host, Vite)                  OpenHub.Administration.UI (remote, Rspack)
├─ vite.config.ts                        ├─ rspack.config.ts
│   federation({                         │   new ModuleFederationPlugin({
│     name: "shell",                     │     name: "adminRemote",
│     remotes: { adminRemote: {...} },   │     exposes: { "./AdminApp": "./src/AdminApp.tsx" },
│     shared: { react, react-dom }       │     shared: { react, react-dom },
│   })                                   │     library: { type: "module" },
│                                        │   })
├─ src/router.tsx
│   lazy(() => import("adminRemote/AdminApp"))
```

At dev time, `OpenHub.UI` runs on `:5173` and `OpenHub.Administration.UI` runs on `:5175`. The
shell's browser bundle fetches `http://localhost:5175/remoteEntry.js` at the moment a user
navigates to `/admin`, negotiates shared React/React-DOM/react-router-dom versions with whatever
the shell already loaded, and mounts the remote's `AdminApp` component into the shell's own React
tree. In production, `5175` becomes wherever this remote's static build actually gets deployed —
see "What independent deployment would actually take" below.

## Auth: who logs in, who just consumes

Covered in the conversation that led here, restated as the actual contract now in code
(`src/federation/HostAuthContext.ts`, duplicated on both sides — see "What's simplified" below):

```ts
interface HostAuthContext {
  roles: string[];
  realmSlug: string;
  getToken: () => Promise<string>;
}
```

The shell (`OpenHub.UI`) is the only thing that ever calls `keycloak.init()`. It resolves the
tenant realm from the hostname (`src/keycloak.ts`), logs the user in, and hands the remote a
plain object: the roles already resolved, the realm slug already resolved, and a function that
wraps `keycloak.updateToken()`. The remote (`OpenHub.Administration.UI`) has no Keycloak
dependency at all — check its `package.json`, it isn't there.

This is deliberately **not** React context shared across the federation boundary. A shared
`AuthContext.Provider` from the shell would require the remote to resolve to the exact same
module instance as the host's `auth/AuthProvider.tsx` — fragile outside of an explicitly
federated singleton, and this contract doesn't need singleton sharing to work: it's just data and
a function, passed as a prop.

`react-router-dom` is deliberately **not** federated as a singleton either, even though `AdminApp`
has its own nested `<Routes>`. It was, briefly — sharing it as a singleton so `AdminApp` could read
Router context from whichever `<BrowserRouter>` the host mounted seemed like the obvious move. It
turned out to be a real regression: the MF runtime eagerly cross-negotiates every declared shared
singleton on page load, not just when the remote is actually used, so *every* shell page load
(not just `/admin`) triggered a cross-bundler (Rspack↔Vite) negotiation with `adminRemote` — and
that negotiation hung indefinitely, blanking the entire shell, not just Administration. Fixed by
dropping `react-router-dom` from both sides' `shared` config and giving `AdminApp` its own
independent `<BrowserRouter basename="/admin">` (see `AdminApp.tsx`). Two separate router
instances both reading the same actual browser URL is unusual, but it's fine for a single fixed
mount point with no need for cross-router navigation — and it avoids federating a package whose
whole value-add (shared navigation state) this integration never actually needed.

The real constraint this creates: **this only works because the remote is always loaded inside
the already-authenticated shell**, never visited on its own by an end user. The standalone dev
harness (`src/bootstrap.tsx`, `src/dev/fakeHostAuth.ts`) exists for exactly that reason — it's a
way to run and iterate on `AdminApp` in isolation (`npm run dev` inside
`OpenHub.Administration.UI`) without a live Keycloak session, using a hardcoded fake
`HostAuthContext`. Real API calls made this way come back `401`, because the fake token isn't a
real Keycloak JWT — that's an intentional limitation, not a bug: the harness is for UI iteration
(layout, forms, tab switching), not an end-to-end auth story. If this module needed to be visited
directly by a URL rather than always embedded in the shell, it would need its own login flow, and
the whole "one Keycloak session" simplification goes away.

## What actually broke, in the order it broke

Seven real failures, each surfaced only at runtime (never at `tsc` or a build step, with one
exception noted below) — this is the honest version of "what's the infra headache," worth reading
before assuming Module Federation "just works" once the config compiles. Left in the order they
were actually hit, including a wrong diagnosis along the way (#3) — the wrong fix looked like it
worked at the time, which is its own lesson.

**1. Vite 5 didn't have `vite/module-runner`.** `@module-federation/vite`'s host plugin declares
`peerDependencies: { vite: "^5.0.0 || ^6.0.0 || ..." }`, but `vite build` failed immediately with
`Missing "./module-runner" specifier in "vite" package` — that export genuinely doesn't exist in
Vite 5.4.x's `exports` map, peer range notwithstanding. Fixed by upgrading the shell to Vite 6.4.3
(confirmed `@vitejs/plugin-react@4.7.0`'s own peer range covers Vite 6 first, so this wasn't a
cascading upgrade). **Lesson: a federation plugin's peer range is not a guarantee — verify against
the actual package exports before trusting it, especially for a tool this young.**

**2. `loadShareSync` / "async boundary" runtime error (MF runtime error #RUNTIME-006).** The
remote's original entry point (`bootstrap.tsx`) synchronously `import`ed `react` and `react-dom`
at the top of the file, and that file *was* the Rspack entry. Module Federation's shared-scope
resolution is asynchronous (it needs to negotiate versions with whatever else is on the page
before handing back a shared module), so a synchronous `import "react"` in the entry chunk itself
runs before that negotiation can happen. Fixed by splitting the entry in two:
`src/index.tsx` — the real Rspack `entry`, containing nothing but `import("./bootstrap")` (a
dynamic import, which *is* an async boundary) — and `src/bootstrap.tsx`, which does the actual
`createRoot(...).render(...)` and can safely import shared packages because it's now loaded
*after* the runtime's negotiation step. **This is reportedly the single most common Module
Federation misconfiguration** — it only shows up once you run the bundle, never at build time.

**3. (Misdiagnosed at first.) The dev server crashed on that same entry split**, throwing
`Cannot set properties of undefined (setting './src/bootstrap.tsx!lazy-compilation-proxy')` —
specific to `rspack serve` (`rspack build` never hit this). The first attempt at a fix was
`experiments.lazyCompilation: false` — a plausible-looking guess, since the error mentions "lazy
compilation" and Rspack does lazy-compile dynamically-imported chunks like `bootstrap.tsx` in dev
mode. It appeared to work (switching to testing the production build sidestepped the crash
entirely), but it was never actually applying: `lazyCompilation` is a **top-level** Rspack option,
not nested under `experiments` — TypeScript caught this later (`tsc -b` error: `'lazyCompilation'
does not exist in type 'Experiments'`) once the field was in the config long enough to get
type-checked properly. The **real** fix turned out to be issue #6 below; once that landed, this
crash didn't recur, with the invalid `experiments.lazyCompilation` field removed. **Lesson: a fix
that makes the symptom go away isn't confirmation of the diagnosis** — this one was disproven by
its own type signature days later, and the actual root cause was a different, unrelated-looking
change.

**4. CORS blocked the host from ever fetching `remoteEntry.js`.** Neither Vite's nor Rspack's dev
server sends `Access-Control-Allow-Origin` by default, and a federated remote's assets are *always*
fetched cross-origin by whatever host consumes them (`localhost:5175` from a page running on
`localhost:5173`; a real deployment's actual shell origin in production). The browser blocked the
fetch outright with a CORS error that gives no hint it's federation-specific. Fixed by adding
explicit headers to the remote's dev server:
```ts
devServer: { headers: { "Access-Control-Allow-Origin": "http://localhost:5173" } }
```
**This is not a dev-only concern** — whatever actually serves this remote's static assets in
production (a CDN, a static host) needs the equivalent CORS configuration for the shell's real
origin(s), or federation silently fails in production the same way.

**5. Container format mismatch ("remote entry interface does not contain 'init'", MF runtime error
#RUNTIME-002).** Once CORS was fixed, the fetch succeeded but the runtime rejected the file — the
host declared this remote with `type: "module"` (a native-ESM container, loaded via a plain
`import()`), but Rspack's `ModuleFederationPlugin` defaults to the classic
script-tag-plus-global-variable container format, not ESM. Both sides need to agree on the
container shape. Fixed by adding `library: { type: "module" }` to the remote's
`ModuleFederationPlugin` config. This is the sharpest edge of mixing bundlers (Rspack remote, Vite
host) — same-bundler setups tend to default to compatible shapes; cross-bundler setups need this
made explicit.

**6. `Uncaught SyntaxError: Unexpected token 'export'` — only in the standalone dev harness**
(`http://localhost:5175/` on its own, not through the shell). The fix for #5
(`library: { type: "module" }`) makes the federation *container* real ESM, but that setting is
scoped to the container specifically — it doesn't flip the whole compilation's `output.module` on.
`HtmlRspackPlugin` only defaults its injected `<script>` tag to `type="module"` when
`output.module` is enabled compilation-wide; left at its default here, the browser tried to parse
an ESM chunk as a classic script and choked on the first `export` keyword. Fixed with an explicit
override: `new HtmlRspackPlugin({ template: "./index.html", scriptLoading: "module" })`. Doesn't
affect the federated path at all — the host never touches this remote's `index.html`, it loads
`AdminApp` via a dynamic `import()`, not a `<script>` tag. Purely a standalone-dev-harness
concern, but a confusing one to land on blind, since the error message gives no hint it's
connected to a Module Federation container-format setting three fixes back.

**7. The shell went blank with no console errors, no failed requests, and no redirect to
login — for a reason that had nothing to do with Module Federation at all.** The router-sharing
deadlock (see above) was the first, correct hypothesis, and fixing it was necessary — but it
wasn't sufficient; the blank screen persisted after that fix. Further isolation (a Playwright probe
confirmed the host's own MF-injected init script never resolved; direct `curl`/`Invoke-WebRequest`
to the remote's dev server hung indefinitely even for the root path and a nonexistent path, with no
response ever, though `Get-NetTCPConnection` showed the TCP connection was accepted) pointed at the
remote's dev server itself as the actual thing not responding. Stripping the remote's config down
in stages — no `dts`, no `ModuleFederationPlugin` at all, no plugins whatsoever — still hung on
port 5174 specifically. The same minimal config on a different port (5198) responded in
single-digit milliseconds. **Port 5174 itself had gotten stuck at the OS level** — almost certainly
stale Windows TCP/socket state from many rapid dev-server process restarts on that exact port over
a long debugging session (Aspire's DCP killing and relaunching the resource repeatedly). Fixed by
moving the remote's dev port to 5175 everywhere it's referenced (`rspack.config.ts`,
`vite.config.ts`'s remote `entry` URL, `AppHost.cs`'s `WithHttpEndpoint`) — not an application or
Module Federation fix at all. **Lesson: when a dev server's TCP connections are accepted but it
never responds to anything, on any path, regardless of app config, suspect the OS/port state before
the app** — this one cost far more debugging time than it should have because every earlier
hypothesis (router sharing, CORS, container format) was a *plausible* Module Federation failure
mode, and this symptom (accepted-but-silent connections) doesn't obviously rule those out without
testing on a fresh port.

**Plus one real application bug, not a federation bug, caught only because of this testing:**
`UserList.tsx`'s effect that loads the organization picker for a platform-admin caller had no
`.catch()` on its fetch — an unhandled promise rejection on any API failure. It existed
unnoticed in the pre-federation version of this code too; testing the remote in isolation (with no
backend running, so every call fails) is what surfaced it. Fixed alongside everything else.

Only one of these ever showed up as a build/type error (#3's invalid config field, and only once
`tsc -b` was run against it — `rspack build`/`rspack serve` both silently ignored the field rather
than rejecting it). Every other failure — #1, #2, #4, #5, #6, #7 — was a runtime-only failure,
caught by actually running the host and remote together in a browser. `tsc --noEmit`, `rspack build`, and
`vite build` all succeeded cleanly the entire time these were happening. **Build success proves
almost nothing about whether Module Federation will actually work at runtime** — plan verification
time accordingly, and don't trust a green build as a substitute for actually loading the page.

## What's simplified here, on purpose

This is a one-remote example, sized for teaching the mechanics, not a template for a
multi-team platform. Things a real multi-remote setup would need that this doesn't have:

- **The `HostAuthContext` contract is hand-duplicated**, once in each project
  (`src/federation/HostAuthContext.ts` in both). A real platform with several remotes would
  publish this as a small versioned package (e.g. `@openhub/mf-contracts`) both sides install,
  so the contract can't drift silently the way two hand-copied files can.
- **`DataTable.tsx` is hand-duplicated** into the remote
  (`OpenHub.Administration.UI/src/components/DataTable.tsx`), not shared. Real platforms usually
  solve this with a published shared-component package or a dedicated "design-system" remote that
  every other remote (and the shell) consumes — standing that up is a second Module Federation
  exercise on top of this one, deliberately out of scope here.
- **No independent CI/CD pipeline** for the remote — it's built and verified alongside everything
  else in this repo's normal flow, not deployed on its own schedule by a separate team. That's the
  actual point of Module Federation in a real org (see below) and the one thing this example
  doesn't demonstrate, since there's only one team here.
- **No contract-compatibility testing** between host and remote (e.g. a check that the shell's
  assumed `AdminApp` prop shape still matches what the remote actually exports before either side
  deploys). A real platform needs this once host and remote deploy independently — otherwise a
  remote's breaking change can silently break the shell (or every other remote) without anyone
  redeploying the shell to notice.

## What independent deployment would actually take

Everything above gets this remote *federating correctly*; it doesn't yet get it *deployed
independently*, which was the original ask. Two things were missing; one is now partially
addressed, one still isn't:

1. **A real hosting target for the remote's static output — now build-time-configurable, not yet
   runtime-configurable.** `vite.config.ts`'s `entry` and `rspack.config.ts`'s `output.publicPath`
   both used to be hardcoded dev URLs; both now read from env vars instead (`ADMIN_REMOTE_URL` and
   `PUBLIC_PATH` — see each project's README and `Dockerfile`), set via `docker build --build-arg`.
   That's real progress — a CDN URL, a staging URL, and the dev default are now three different
   builds of the same source, not three edits to it. But it's still resolved into a **bundled
   constant at build time**, the same way Vite inlines `VITE_` vars: the shell still needs a
   rebuild every time the remote's deployed URL changes, it just no longer needs a source edit to
   go with it. A true runtime-configurable public path (reading `window.__ADMIN_REMOTE_URL__` or
   similar, set by the serving infra rather than baked in) is possible in both Vite's federation
   plugin and Rspack/webpack's `__webpack_public_path__`, just not implemented here — see "What's
   simplified here" above.
2. **A separate build/deploy pipeline** for `OpenHub.Administration.UI` — its own CI job, its own
   artifact, its own release cadence, independent of `OpenHub.UI`'s. This repo builds both
   together today; nothing stops that, but it also means "independent deploys" isn't actually
   being exercised yet, just made possible. Each project's own `Dockerfile` is a step toward this
   (each is a genuinely independent build you can run and publish separately), but nothing here
   defines two separate CI jobs or release cadences yet.

## Running it locally

```bash
# Terminal 1 — the remote
cd src/OpenHub.Administration.UI
npm install
npm run dev          # http://localhost:5175

# Terminal 2 — the shell
cd src/OpenHub.UI
npm install
npm run dev           # http://localhost:5173
```

Or via Aspire — `admin-ui-remote` is wired into `OpenHub.Platform.AppHost/AppHost.cs` alongside
`shell`, started the same way (`AddJavaScriptApp`, not `AddViteApp`, since it isn't a Vite
project).

To iterate on the remote in isolation, without the shell or a live Keycloak session:

```bash
cd src/OpenHub.Administration.UI
npm run dev
# open http://localhost:5175 directly — src/bootstrap.tsx renders AdminApp
# with the fake auth context from src/dev/fakeHostAuth.ts
```
