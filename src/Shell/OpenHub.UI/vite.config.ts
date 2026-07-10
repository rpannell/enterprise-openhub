import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { federation } from "@module-federation/vite";

export default defineConfig({
  plugins: [
    react(),
    // Consumes the Administration module as a genuine Module Federation
    // remote — a separately built (Rspack), separately deployed artifact,
    // not a folder in this repo. See src/Administration/OpenHub.Administration.UI and
    // docs/module-federation.md for the full story.
    federation({
      name: "shell",
      remotes: {
        adminRemote: {
          type: "module",
          name: "adminRemote",
          // Resolved at build time (this file runs in Node, so plain process.env — no VITE_
          // prefix needed; the federation plugin bakes the resolved string into the compiled
          // host runtime that fetches it in the browser). Must match wherever
          // OpenHub.Administration.UI's build actually gets hosted — the same value its own
          // `PUBLIC_PATH` build arg was set to (see that project's Dockerfile/README) plus
          // "remoteEntry.js". Passed as `docker build --build-arg ADMIN_REMOTE_URL=...` here.
          entry: process.env.ADMIN_REMOTE_URL ?? "http://localhost:5175/remoteEntry.js",
          entryGlobalName: "adminRemote",
          shareScope: "default",
        },
        crmRemote: {
          type: "module",
          name: "crmRemote",
          // Same build-time-resolution pattern as adminRemote above. Must match
          // OpenHub.CRM.UI's own `PUBLIC_PATH` build arg plus "remoteEntry.js".
          // Passed as `docker build --build-arg CRM_REMOTE_URL=...` here.
          entry: process.env.CRM_REMOTE_URL ?? "http://localhost:5176/remoteEntry.js",
          entryGlobalName: "crmRemote",
          shareScope: "default",
        },
        productRemote: {
          type: "module",
          name: "productRemote",
          // Same build-time-resolution pattern as adminRemote/crmRemote above. Must
          // match OpenHub.ProductManager.UI's own `PUBLIC_PATH` build arg plus
          // "remoteEntry.js". Passed as `docker build --build-arg
          // PRODUCT_REMOTE_URL=...` here.
          entry: process.env.PRODUCT_REMOTE_URL ?? "http://localhost:5177/remoteEntry.js",
          entryGlobalName: "productRemote",
          shareScope: "default",
        },
      },
      // NOT react-router-dom — see docs/module-federation.md "The router-sharing
      // deadlock". Sharing it as a singleton forced the MF runtime to eagerly
      // cross-negotiate with adminRemote on every single page load (Vite's
      // federation plugin auto-injects host-init into the *whole app's* entry
      // point, not just the lazy /admin route), and that cross-bundler
      // (Rspack<->Vite) negotiation hung indefinitely — a genuine regression
      // that blanked the entire shell, not just Administration. AdminApp now
      // owns its own independent router instance instead of reading the
      // shell's ambient Router context.
      shared: {
        react: { singleton: true, requiredVersion: "^18.3.1" },
        "react-dom": { singleton: true, requiredVersion: "^18.3.1" },
      },
    }),
  ],
  server: {
    port: 5173,
    origin: "http://localhost:5173",
    // Allows *.localhost dev requests (client-a.localhost, admin.localhost, ...) so
    // hostname-based realm resolution (src/keycloak.ts) can be exercised locally —
    // browsers resolve *.localhost to 127.0.0.1 with no /etc/hosts edit needed.
    allowedHosts: [".localhost"],
    proxy: {
      "/pm": {
        target: "http://localhost:5200",
        rewrite: (path) => path.replace(/^\/pm/, ""),
        changeOrigin: true,
      },
      "/adm": {
        target: "http://localhost:5300",
        rewrite: (path) => path.replace(/^\/adm/, ""),
        changeOrigin: true,
      },
      "/crmapi": {
        target: "http://localhost:5400",
        rewrite: (path) => path.replace(/^\/crmapi/, ""),
        changeOrigin: true,
      },
      "/salesorgapi": {
        target: "http://localhost:5500",
        rewrite: (path) => path.replace(/^\/salesorgapi/, ""),
        changeOrigin: true,
      },
    },
  },
});
