import type { HostAuthContext } from "../federation/HostAuthContext";

/**
 * Used only when this remote runs standalone (`npm run dev` in this project,
 * not through the shell) — see src/bootstrap.tsx. There is no real Keycloak
 * session here to read a token from, so getToken() returns a placeholder.
 * That's enough to exercise every screen/layout/form in isolation, but any
 * real request to Administration.Api will come back 401 — this harness is
 * for UI iteration, not an end-to-end auth story. If you need live data
 * while running standalone, paste a real access token copied from the
 * shell's browser session (DevTools → Application → sessionStorage, or log
 * `keycloak.token` in the shell) into DEV_TOKEN below.
 */
const DEV_TOKEN = "";

export const fakeHostAuth: HostAuthContext = {
  roles: ["platform-admin", "tenant-admin"],
  realmSlug: "client-a",
  getToken: async () => DEV_TOKEN,
};
