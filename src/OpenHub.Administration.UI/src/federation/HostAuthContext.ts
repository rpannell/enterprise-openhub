/**
 * The entire contract between the shell and this remote. This is the one
 * thing both sides must agree on and version carefully — everything else in
 * src/ is a private implementation detail the host never touches.
 *
 * Deliberately NOT React context shared across the federation boundary.
 * Passing plain data/functions as props works reliably regardless of build
 * tooling on either side; a shared React context requires both sides to
 * resolve to the literal same module instance, which is fragile outside of
 * an explicitly federated singleton (react-router-dom is federated this way
 * here specifically because nested <Routes> needs it — see rspack.config.ts
 * for why that's the one deliberate exception).
 *
 * Also deliberately excludes anything Keycloak-shaped. This remote never
 * initializes its own Keycloak session or resolves its own tenant realm —
 * see docs/module-federation.md ("Auth: who logs in, who just consumes").
 */
export interface HostAuthContext {
  /** Every role (realm + client) the shell's Keycloak session already resolved. */
  roles: string[];

  /**
   * The caller's own realm slug (derived by the shell from the hostname —
   * see OpenHub.UI/src/keycloak.ts). A tenant-admin can only manage this
   * realm; a platform-admin ignores it and picks from every organization.
   */
  realmSlug: string;

  /**
   * Returns a fresh bearer token for calling OpenHub.Administration.Api.
   * Wraps the shell's own keycloak.updateToken() — this remote never talks
   * to Keycloak directly.
   */
  getToken: () => Promise<string>;
}
