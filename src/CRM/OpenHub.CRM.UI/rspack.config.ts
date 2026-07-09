import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "@rspack/cli";
import { HtmlRspackPlugin, type RspackPluginInstance } from "@rspack/core";
import { ModuleFederationPlugin } from "@module-federation/enhanced/rspack";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// Versions here MUST match the host's (src/Shell/OpenHub.UI/package.json) — singleton
// sharing means "exactly one copy of this package in memory," not "compatible
// copies." A mismatched React/react-router-dom version here is the single most
// common way a Module Federation setup breaks (usually as an opaque "invalid
// hook call" error with no obvious link back to a version mismatch).
export default defineConfig({
  // NOT bootstrap.tsx directly — see src/index.tsx for why the entry must be
  // a bare dynamic import (Module Federation's "async boundary" requirement).
  entry: "./src/index.tsx",
  resolve: {
    extensions: [".tsx", ".ts", ".jsx", ".js"],
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: {
          loader: "builtin:swc-loader",
          options: {
            jsc: {
              parser: { syntax: "typescript", tsx: true },
              transform: { react: { runtime: "automatic" } },
            },
          },
        },
        type: "javascript/auto",
      },
      {
        test: /\.css$/i,
        use: ["postcss-loader"],
        type: "css",
      },
    ],
  },
  devServer: {
    // Not the shell's port (5173) or Administration's (5175) — each federated remote
    // gets its own fixed dev port. See AppHost.cs's comment on crmUiRemote for why this
    // resource has no .WithHttpEndpoint() at all (same Aspire DCP proxy issue Administration
    // hit — see adminUiRemote's comment there).
    port: 5176,
    // CrmApp mounts its own <BrowserRouter basename="/crm"> (see CrmApp.tsx), which renders
    // nothing at all — not even the nav outside <Routes> — for any URL that doesn't start
    // with "/crm". Without this fallback, a direct navigation or a plain page refresh at
    // /crm/clients 404s outright instead of re-serving index.html and letting the
    // client-side router take over, since there's no such file on disk.
    historyApiFallback: true,
    // Allows *.localhost dev requests (client-a.localhost, admin.localhost, ...) — same
    // reason as the shell's identical `allowedHosts: [".localhost"]` in vite.config.ts.
    // This is a *different* check than the CORS `headers` below: rspack's dev-server has
    // its own built-in Origin/Host CSRF guard (independent of CORS) that 403s any request
    // whose Origin isn't in this list — a plain `Access-Control-Allow-Origin` header does
    // nothing against it.
    allowedHosts: [".localhost"],
    // Not optional, and not on by default — a remote's assets are always
    // fetched cross-origin by whatever host federates it. Without this, the
    // browser blocks the fetch of remoteEntry.js outright with an opaque CORS
    // error that gives no hint it's a federation-specific requirement.
    // A function (not a static value) because the shell is reachable at several
    // origins in dev: bare localhost:5173, and the tenant-realm subdomains
    // (client-a.localhost:5173, admin.localhost:5173, ...) used to exercise
    // hostname-based realm resolution (see OpenHub.UI/src/keycloak.ts). A real
    // deployment should send its actual origin(s) the same way, not "*".
    headers: (req: { headers: { origin?: string } }) => {
      const origin = req.headers.origin;
      const allowed =
        origin === "http://localhost:5173" ||
        (!!origin && /^http:\/\/[a-z0-9-]+\.localhost:5173$/.test(origin));
      return {
        "Access-Control-Allow-Origin": allowed ? origin! : "http://localhost:5173",
      };
    },
    // Lets this remote also run standalone (npm run dev, in isolation from the
    // shell) for fast UI iteration without needing the whole platform running —
    // see src/bootstrap.tsx and src/dev/fakeHostAuth.ts.
    proxy: [
      {
        // "/crmapi/api" (not the bare "/crmapi" prefix) — same reasoning as
        // Administration's "/adm/api" convention: keeps the API-only prefix
        // consistent even though "/crmapi" doesn't collide with this remote's
        // own "/crm" mount point the way "/adm" collided with "/admin".
        context: ["/crmapi/api"],
        target: "http://localhost:5400",
        pathRewrite: { "^/crmapi": "" },
      },
      {
        // Client's "sales person" picklist is sourced directly from salesorg-api, not
        // proxied through CRM.Api — see crmApi.ts's getSalesPeoplePicklist().
        context: ["/salesorgapi/api"],
        target: "http://localhost:5500",
        pathRewrite: { "^/salesorgapi": "" },
      },
    ],
  },
  plugins: [
    // scriptLoading: "module" is required for the standalone dev harness
    // (index.html, used by `npm run dev` in isolation — see bootstrap.tsx).
    // The federation container below is emitted as real ESM (`library:
    // { type: "module" }`), and HtmlRspackPlugin only defaults its injected
    // <script> tags to type="module" when the *entire* compilation's
    // `output.module` is on — which it isn't here, only the container is
    // ESM. Left at the default, the browser tries to parse an ESM chunk as
    // a classic script and fails on the first `export` keyword. Doesn't
    // affect the federated path at all (the host loads this remote via a
    // dynamic import(), never a <script> tag).
    new HtmlRspackPlugin({ template: "./index.html", scriptLoading: "module" }),
    new ModuleFederationPlugin({
      name: "crmRemote",
      filename: "remoteEntry.js",
      // The host's federation config (vite.config.ts) declares this remote
      // with `type: "module"` — a native-ESM container, loadable via a plain
      // `import()`, rather than the classic "script tag + global variable"
      // container Module Federation used pre-MF2. Both sides must agree on
      // this; a host expecting a module container against a remote emitting
      // the default (var/global) one fails at runtime with "remote entry
      // interface does not contain 'init'" — a MF2 runtime error, not a
      // build-time one, since the container loads fine, it just isn't the
      // shape the host's loader expects.
      library: { type: "module" },
      exposes: {
        // The one public surface this remote commits to. Everything else
        // under src/ is a private implementation detail the host never
        // imports directly — that boundary is what makes "deploy this
        // remote independently" actually safe.
        "./CrmApp": "./src/CrmApp.tsx",
      },
      // NOT react-router-dom — see docs/module-federation.md "The router-sharing
      // deadlock" in OpenHub.UI (same reasoning Administration's remote follows).
      // It was a singleton so CrmApp's nested <Routes> could read Router context
      // from whichever <BrowserRouter> the host mounted, but sharing it forced
      // the MF runtime to eagerly cross-negotiate this remote on *every* shell
      // page load, not just /crm, and that negotiation hung indefinitely. CrmApp
      // mounts its own independent <BrowserRouter> instead (see CrmApp.tsx) —
      // two separate router instances both reading the same actual browser URL,
      // which is fine for our fixed /crm/* mount point.
      shared: {
        react: { singleton: true, requiredVersion: "^18.3.1" },
        "react-dom": { singleton: true, requiredVersion: "^18.3.1" },
      },
      manifest: true,
    }) as RspackPluginInstance,
  ],
  output: {
    path: path.resolve(__dirname, "dist"),
    uniqueName: "crmRemote",
    // Every chunk this remote emits (remoteEntry.js, the exposed CrmApp chunk, its CSS)
    // resolves against this at runtime, so it has to be wherever this build's output is
    // actually hosted, not the dev URL, for anything other than local dev. Read from an env
    // var, set as a `docker build --build-arg PUBLIC_PATH=...` (see Dockerfile), rather than
    // hardcoded — same pattern as Administration's remote and the shell's own VITE_ vars:
    // one image per environment/publicPath, not something a running container can be
    // reconfigured to at deploy time without a rebuild.
    publicPath: process.env.PUBLIC_PATH ?? "http://localhost:5176/",
  },
});
