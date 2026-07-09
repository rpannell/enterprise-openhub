import { test, expect } from "@playwright/test";

/**
 * MF-boundary regression checks tied to real failure modes documented in
 * docs/module-federation.md (in the shell project): CORS misconfiguration,
 * and the MF2 container-format mismatch ("remote entry interface does not
 * contain 'init'").
 *
 * NOT driven through the shell — see playwright.config.ts's top comment for
 * why: OpenHub.UI's AuthProvider.tsx calls keycloak.init({ onLoad:
 * "login-required" }) unconditionally, so loading the shell's /admin route
 * today redirects into a real Keycloak login before the federated remote
 * ever mounts. Until a test auth path exists there (flagged as an open
 * question in the test plan), the strongest check achievable without shell
 * changes is against this remote's own dev server directly: does
 * remoteEntry.js actually serve, as valid ESM, with the CORS header the
 * shell's origins need.
 */
test.describe("Module Federation boundary", () => {
  test("remoteEntry.js is servable and CORS-allows the shell's dev origin", async ({ request }) => {
    const response = await request.get("http://localhost:5175/remoteEntry.js", {
      headers: { Origin: "http://localhost:5173" },
    });

    expect(response.status()).toBe(200);
    expect(response.headers()["access-control-allow-origin"]).toBe("http://localhost:5173");
  });

  test("remoteEntry.js CORS-allows a tenant subdomain origin (client-a.localhost:5173)", async ({ request }) => {
    const response = await request.get("http://localhost:5175/remoteEntry.js", {
      headers: { Origin: "http://client-a.localhost:5173" },
    });

    expect(response.status()).toBe(200);
    expect(response.headers()["access-control-allow-origin"]).toBe("http://client-a.localhost:5173");
  });

  test("remoteEntry.js is emitted as an ES module container (MF2 shape, not the classic var/global container)", async ({
    request,
  }) => {
    const response = await request.get("http://localhost:5175/remoteEntry.js");
    const body = await response.text();

    // A classic (pre-MF2) container assigns itself to a global var instead —
    // this is the one-line signature of the "container-format mismatch"
    // failure mode documented in docs/module-federation.md, catchable
    // without ever loading this file in a real host.
    expect(body).toMatch(/export/);
  });
});
