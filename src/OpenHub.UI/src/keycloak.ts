import Keycloak from "keycloak-js";

// Reads from .env / .env.local — copy .env.example to get started.
export const KEYCLOAK_URL = import.meta.env.VITE_KEYCLOAK_URL;
export const KEYCLOAK_REALM = import.meta.env.VITE_KEYCLOAK_REALM;
export const KEYCLOAK_CLIENT_ID = import.meta.env.VITE_KEYCLOAK_CLIENT_ID;

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
