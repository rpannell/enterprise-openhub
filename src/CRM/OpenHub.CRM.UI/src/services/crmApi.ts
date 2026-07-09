import type {
  Client,
  CreateClientRequest,
  UpdateClientRequest,
  SalesPersonPicklistItem,
  SalesPerson,
  SalesRole,
  SalesOrganizationUnit,
} from "../types";

const BASE = "/crmapi";
const SALESORG_BASE = "/salesorgapi";

/**
 * A factory, not a module-level singleton like the shell's productManagerApi.ts.
 * This remote has no Keycloak session of its own to read a token from — the
 * token getter is handed in by whoever mounts <CrmApp>, always the shell in
 * practice. Relative fetches to /crmapi resolve against the *host page's* origin
 * regardless of which federated chunk issued them, so the shell's existing
 * Vite dev proxy (/crmapi -> CRM.Api) keeps working unchanged; this remote's
 * own standalone dev server proxies the same path for when it runs in
 * isolation (see rspack.config.ts devServer.proxy).
 */
export function createCrmApi(getToken: () => Promise<string>) {
  async function apiFetch(base: string, path: string, init?: RequestInit): Promise<Response> {
    const token = await getToken();
    const res = await fetch(`${base}${path}`, {
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
    getClients: (): Promise<Client[]> =>
      apiFetch(BASE, "/api/v1/clients").then((r) => r.json()),

    getClient: (id: string): Promise<Client> =>
      apiFetch(BASE, `/api/v1/clients/${id}`).then((r) => r.json()),

    createClient: (req: CreateClientRequest): Promise<Client> =>
      apiFetch(BASE, "/api/v1/clients", {
        method: "POST",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    updateClient: (id: string, req: UpdateClientRequest): Promise<Client> =>
      apiFetch(BASE, `/api/v1/clients/${id}`, {
        method: "PUT",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    deleteClient: (id: string): Promise<void> =>
      apiFetch(BASE, `/api/v1/clients/${id}`, { method: "DELETE" }).then(() => undefined),

    // Lean picklist against salesorg-api directly (not proxied through CRM.Api) — backs the
    // "sales person" dropdown on the client form. Full SalesPerson/Role/OrgUnit/Assignment
    // *management* (create/update/delete) is still a separate, sizeable follow-up under
    // SalesOrg.UI — but a read-only viewer now lives in this remote (see getSalesPeople et al
    // below and src/salesorg/SalesHierarchyView.tsx).
    getSalesPeoplePicklist: (): Promise<SalesPersonPicklistItem[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-people").then((r) => r.json()).then((people: any[]) =>
        people.map((p) => ({ id: p.id, displayName: `${p.firstName} ${p.lastName}`.trim() }))
      ),

    // Read-only calls backing the "Sales Hierarchy" viewer (src/salesorg/SalesHierarchyView.tsx).
    // Same SALESORG_BASE / raw-apiFetch pattern as getSalesPeoplePicklist above — full
    // SalesOrg management (create/update/delete) is still out of scope for this remote.
    getSalesPeople: (): Promise<SalesPerson[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-people").then((r) => r.json()),

    getSalesRoles: (): Promise<SalesRole[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-roles").then((r) => r.json()),

    getSalesOrgUnits: (): Promise<SalesOrganizationUnit[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-org-units").then((r) => r.json()),

    getSalesPersonManagerChain: (id: string): Promise<SalesPerson[]> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-people/${id}/manager-chain`).then((r) => r.json()),

    getSalesPersonReports: (id: string): Promise<SalesPerson[]> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-people/${id}/reports`).then((r) => r.json()),

    getSalesOrgUnitDescendants: (id: string): Promise<string[]> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-org-units/${id}/descendants`).then((r) => r.json()),
  };
}

export type CrmApi = ReturnType<typeof createCrmApi>;
