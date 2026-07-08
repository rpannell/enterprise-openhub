import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
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
