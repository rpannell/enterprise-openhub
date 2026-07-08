import keycloak from "../keycloak";
import type {
  Organization,
  CreateOrganizationRequest,
  OrgUser,
  CreateUserRequest,
  Role,
} from "../modules/admin/types";

const BASE = "/adm";

async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  await keycloak.updateToken(30).catch(() => keycloak.login());
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      Authorization: `Bearer ${keycloak.token}`,
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

export const administrationApi = {
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
