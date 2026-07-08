import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { federation } from "@module-federation/vite";

export default defineConfig({
  plugins: [
    react(),
    // Consumes the Administration module as a genuine Module Federation
    // remote — a separately built (Rspack), separately deployed artifact,
    // not a folder in this repo. See src/OpenHub.Administration.UI and
    // docs/module-federation.md for the full story.
    federation({
      name: "shell",
      remotes: {
        adminRemote: {
          type: "module",
          name: "adminRemote",
          entry: "http://localhost:5175/remoteEntry.js",
          entryGlobalName: "adminRemote",
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
    },
  },
});
