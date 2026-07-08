import Keycloak from "keycloak-js";

// Reads from .env / .env.local — copy .env.example to get started.
export const KEYCLOAK_URL = import.meta.env.VITE_KEYCLOAK_URL;
export const KEYCLOAK_CLIENT_ID = import.meta.env.VITE_KEYCLOAK_CLIENT_ID;

// Realm is derived from the hostname's first label — e.g. "client-a.openhub.com" or
// "client-a.localhost:5173" (browsers resolve *.localhost to 127.0.0.1 with no /etc/hosts
// edit) logs into the "client-a" realm, "admin.openhub.com" logs into "platform-admin".
// A production deployment needs no per-tenant build or env var at all: every tenant's
// subdomain maps straight to its realm. VITE_KEYCLOAK_DEFAULT_REALM is only a fallback for
// when the hostname has no recognizable subdomain (e.g. plain "localhost" in local dev).
function resolveRealm(): string {
  const host = window.location.hostname;
  const label = host.split(".")[0];

  if (label === "admin") return "platform-admin";

  // "localhost" itself (no subdomain) — fall back to the configured default realm.
  if (label === "localhost" || label === "127" || /^\d+$/.test(label)) {
    const fallback = import.meta.env.VITE_KEYCLOAK_DEFAULT_REALM as string | undefined;
    if (!fallback)
      throw new Error(
        "Could not resolve a Keycloak realm from the hostname and VITE_KEYCLOAK_DEFAULT_REALM is not set."
      );
    return fallback;
  }

  return label;
}

export const KEYCLOAK_REALM = resolveRealm();

const keycloak = new Keycloak({
  url: KEYCLOAK_URL,
  realm: KEYCLOAK_REALM,
  clientId: KEYCLOAK_CLIENT_ID,
});

// Keycloak's own built-in account management UI — every realm has one.
// Users can update their name, email, password, and (if you've enabled it)
// MFA, directly through Keycloak's own page.
export const accountConsoleUrl = `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/account`;

export default keycloak;
