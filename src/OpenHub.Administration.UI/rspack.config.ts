import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "@rspack/cli";
import { HtmlRspackPlugin, type RspackPluginInstance } from "@rspack/core";
import { ModuleFederationPlugin } from "@module-federation/enhanced/rspack";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// Versions here MUST match the host's (src/OpenHub.UI/package.json) — singleton
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
    // Not 5174 — see AppHost.cs's comment on adminUiRemote for why this resource
    // has no .WithHttpEndpoint() at all. The actual bug was Aspire's DCP proxy
    // hanging in front of this dev server; the port number itself was never the
    // problem, but 5175 stuck as the port once the endpoint declaration came out.
    port: 5175,
    // Allows *.localhost dev requests (client-a.localhost, admin.localhost, ...) — same
    // reason as the shell's identical `allowedHosts: [".localhost"]` in vite.config.ts.
    // This is a *different* check than the CORS `headers` below: rspack's dev-server has
    // its own built-in Origin/Host CSRF guard (independent of CORS) that 403s any request
    // whose Origin isn't in this list — a plain `Access-Control-Allow-Origin` header does
    // nothing against it. Missing this is why loading `/admin` (which pulls this remote's
    // exposed-module CSS chunk cross-origin from a tenant subdomain) 403'd even though a
    // direct curl to the same URL succeeded — curl doesn't send a Referer/Origin the way a
    // real `<link>` tag load from client-a.localhost does, so the guard never triggered.
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
        context: ["/adm"],
        target: "http://localhost:5300",
        pathRewrite: { "^/adm": "" },
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
      name: "adminRemote",
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
        "./AdminApp": "./src/AdminApp.tsx",
      },
      // NOT react-router-dom — see docs/module-federation.md "The router-sharing
      // deadlock" in OpenHub.UI. It was a singleton so AdminApp's nested
      // <Routes> could read Router context from whichever <BrowserRouter> the
      // host mounted, but sharing it forced the MF runtime to eagerly
      // cross-negotiate this remote on *every* shell page load, not just
      // /admin, and that negotiation hung indefinitely. AdminApp now mounts
      // its own independent <BrowserRouter> instead (see AdminApp.tsx) — two
      // separate router instances both reading the same actual browser URL,
      // which is fine for our fixed /admin/* mount point.
      shared: {
        react: { singleton: true, requiredVersion: "^18.3.1" },
        "react-dom": { singleton: true, requiredVersion: "^18.3.1" },
      },
      manifest: true,
    }) as RspackPluginInstance,
  ],
  output: {
    path: path.resolve(__dirname, "dist"),
    uniqueName: "adminRemote",
    publicPath: "http://localhost:5175/",
  },
});
