import type {
  Organization,
  CreateOrganizationRequest,
  OrgUser,
  CreateUserRequest,
  Role,
} from "../types";

const BASE = "/adm";

/**
 * A factory, not a module-level singleton like the shell's productManagerApi.ts.
 * This remote has no Keycloak session of its own to read a token from — the
 * token getter is handed in by whoever mounts <AdminApp>, always the shell in
 * practice. Relative fetches to /adm resolve against the *host page's* origin
 * regardless of which federated chunk issued them, so the shell's existing
 * Vite dev proxy (/adm -> Administration.Api) keeps working unchanged; this
 * remote's own standalone dev server proxies the same path for when it runs
 * in isolation (see rspack.config.ts devServer.proxy).
 */
export function createAdministrationApi(getToken: () => Promise<string>) {
  async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
    const token = await getToken();
    const res = await fetch(`${BASE}${path}`, {
      ...init,
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
        ...init?.headers,
      },
    });
    if (!res.ok) {
      const body = await res.json().catch(() => null);
      const err = Object.assign(new Error(res.statusText), {
        status: res.status,
        body,
      });
      throw err;
    }
    return res;
  }

  return {
    getOrganizations: (): Promise<Organization[]> =>
      apiFetch("/api/v1/organizations").then((r) => r.json()),

    createOrganization: (req: CreateOrganizationRequest): Promise<Organization> =>
      apiFetch("/api/v1/organizations", {
        method: "POST",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    getOrganizationUsers: (realmSlug: string): Promise<OrgUser[]> =>
      apiFetch(`/api/v1/organizations/${realmSlug}/users`).then((r) => r.json()),

    createOrganizationUser: (realmSlug: string, req: CreateUserRequest): Promise<OrgUser> =>
      apiFetch(`/api/v1/organizations/${realmSlug}/users`, {
        method: "POST",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    getAssignableRoles: (realmSlug: string): Promise<Role[]> =>
      apiFetch(`/api/v1/organizations/${realmSlug}/roles`).then((r) => r.json()),

    assignRole: (realmSlug: string, userId: string, roleName: string): Promise<void> =>
      apiFetch(`/api/v1/organizations/${realmSlug}/users/${userId}/roles`, {
        method: "POST",
        body: JSON.stringify({ roleName }),
      }).then(() => undefined),
  };
}

export type AdministrationApi = ReturnType<typeof createAdministrationApi>;
