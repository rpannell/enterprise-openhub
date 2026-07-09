import { defineConfig, devices } from "@playwright/test";

/**
 * Two projects, one shared dev server (this remote's own, port 5176):
 *
 * - "standalone": the standalone dev harness (fakeHostAuth). No backend, no
 *   shell — UI-only assertions plus route-stubbed /crmapi/api/v1/* calls (real
 *   calls would 401/fail here — no CRM.Api running).
 *
 * - "federated": MF-boundary regression checks (remoteEntry.js servability +
 *   CORS) against this remote's own dev server directly. NOT driven through
 *   the shell — same reasoning as Administration.UI's identical split (see
 *   that project's playwright.config.ts): OpenHub.UI's AuthProvider.tsx calls
 *   keycloak.init({ onLoad: "login-required" }) unconditionally, so any
 *   Playwright run hitting the shell's / or /crm today gets redirected into a
 *   real Keycloak login before the router (and therefore the federated
 *   remote) ever mounts.
 *
 * `webServer` is a top-level TestConfig option (not per-project) — a single
 * shared server here covers both projects since neither needs the shell.
 *
 * Must stay "localhost", not "127.0.0.1": rspack.config.ts's output.publicPath
 * hardcodes absolute "http://localhost:5176/..." script URLs into index.html,
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
    url: "http://localhost:5176",
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
  projects: [
    {
      name: "standalone",
      testMatch: "standalone.spec.ts",
      use: { ...devices["Desktop Chrome"], baseURL: "http://localhost:5176" },
    },
    {
      name: "federated",
      testMatch: "federated.spec.ts",
      use: { ...devices["Desktop Chrome"], baseURL: "http://localhost:5176" },
    },
  ],
});
