import { defineConfig, devices } from "@playwright/test";

/**
 * Two projects, one shared dev server (this remote's own, port 5177):
 *
 * - "standalone": the standalone dev harness (fakeHostAuth). No backend, no
 *   shell — UI-only assertions plus route-stubbed /pm/api/v1/* calls (real
 *   calls would 401/fail here — no ProductManager.Api running).
 *
 * - "federated": MF-boundary regression checks (remoteEntry.js servability +
 *   CORS) against this remote's own dev server directly. NOT driven through
 *   the shell: OpenHub.UI's AuthProvider.tsx calls keycloak.init({ onLoad:
 *   "login-required" }) unconditionally, so any Playwright run hitting the
 *   shell's / or /product today gets redirected into a real Keycloak login
 *   before the router (and therefore the federated remote) ever mounts —
 *   there's no dev/test auth bypass to drive past that gate yet (same open
 *   question flagged for Administration.UI and CRM.UI). Until that's
 *   resolved, a true "load /product in the shell and watch the remote mount"
 *   E2E test isn't achievable without either a live Keycloak + seeded users,
 *   or a deliberate, reviewed change to AuthProvider — out of scope here.
 *
 * `webServer` is a top-level TestConfig option (not per-project) — a single
 * shared server here covers both projects since neither needs the shell.
 *
 * Must stay "localhost", not "127.0.0.1": rspack.config.ts's output.publicPath
 * hardcodes absolute "http://localhost:5177/..." script URLs into index.html,
 * so loading the page from a different origin (even 127.0.0.1, same machine
 * and port) makes the browser treat its own script tags as cross-origin — the
 * dev server's CORS `headers` function only allows localhost:5173 and its
 * subdomains, so those requests get blocked outright and the app never mounts.
 */
export default defineConfig({
  testDir: "./e2e",
  // This remote's dev server compiles lazily on first request per route
  // (rspack's lazy-compilation) — concurrent workers hitting the same routes
  // at once race that on-demand compile and time out. The suite is small
  // enough that running serially costs little and removes the flake.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: "list",
  use: {
    trace: "on-first-retry",
  },
  webServer: {
    command: "npm run dev",
    url: "http://localhost:5177",
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
  projects: [
    {
      name: "standalone",
      testMatch: "standalone.spec.ts",
      use: { ...devices["Desktop Chrome"], baseURL: "http://localhost:5177" },
    },
    {
      name: "federated",
      testMatch: "federated.spec.ts",
      use: { ...devices["Desktop Chrome"], baseURL: "http://localhost:5177" },
    },
  ],
});
