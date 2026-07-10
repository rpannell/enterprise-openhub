import type {
  Client,
  CreateClientRequest,
  UpdateClientRequest,
  SalesPersonPicklistItem,
  SalesPerson,
  AvailableKeycloakUser,
  CreateSalesPersonRequest,
  UpdateSalesPersonRequest,
  SalesRole,
  CreateSalesRoleRequest,
  UpdateSalesRoleRequest,
  SalesOrganizationUnit,
  CreateSalesOrganizationUnitRequest,
  UpdateSalesOrganizationUnitRequest,
  SalesAssignment,
  CreateSalesAssignmentRequest,
  UpdateSalesAssignmentRequest,
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
    // "sales person" dropdown on the client form.
    getSalesPeoplePicklist: (): Promise<SalesPersonPicklistItem[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-people").then((r) => r.json()).then((people: any[]) =>
        people.map((p) => ({ id: p.id, displayName: `${p.firstName} ${p.lastName}`.trim() }))
      ),

    // Calls backing the "Sales Hierarchy" viewer (src/salesorg/SalesHierarchyView.tsx). Same
    // SALESORG_BASE / raw-apiFetch pattern as getSalesPeoplePicklist above.
    getSalesPeople: (): Promise<SalesPerson[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-people").then((r) => r.json()),

    // Sales people are always created by linking an existing Keycloak identity, not by typing
    // name/email freehand — they need a Keycloak account to log in, and this is where it comes
    // from. Only returns users not already linked to a sales person.
    getAvailableKeycloakUsers: (): Promise<AvailableKeycloakUser[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-people/available-keycloak-users").then((r) => r.json()),

    createSalesPerson: (req: CreateSalesPersonRequest): Promise<SalesPerson> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-people", {
        method: "POST",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    updateSalesPerson: (id: string, req: UpdateSalesPersonRequest): Promise<SalesPerson> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-people/${id}`, {
        method: "PUT",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    deleteSalesPerson: (id: string): Promise<void> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-people/${id}`, { method: "DELETE" }).then(() => undefined),

    getSalesRoles: (): Promise<SalesRole[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-roles").then((r) => r.json()),

    createSalesRole: (req: CreateSalesRoleRequest): Promise<SalesRole> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-roles", {
        method: "POST",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    updateSalesRole: (id: string, req: UpdateSalesRoleRequest): Promise<SalesRole> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-roles/${id}`, {
        method: "PUT",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    deleteSalesRole: (id: string): Promise<void> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-roles/${id}`, { method: "DELETE" }).then(() => undefined),

    getSalesOrgUnits: (): Promise<SalesOrganizationUnit[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-org-units").then((r) => r.json()),

    createSalesOrgUnit: (req: CreateSalesOrganizationUnitRequest): Promise<SalesOrganizationUnit> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-org-units", {
        method: "POST",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    updateSalesOrgUnit: (id: string, req: UpdateSalesOrganizationUnitRequest): Promise<SalesOrganizationUnit> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-org-units/${id}`, {
        method: "PUT",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    deleteSalesOrgUnit: (id: string): Promise<void> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-org-units/${id}`, { method: "DELETE" }).then(() => undefined),

    getSalesAssignments: (): Promise<SalesAssignment[]> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-assignments").then((r) => r.json()),

    createSalesAssignment: (req: CreateSalesAssignmentRequest): Promise<SalesAssignment> =>
      apiFetch(SALESORG_BASE, "/api/v1/sales-assignments", {
        method: "POST",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    updateSalesAssignment: (id: string, req: UpdateSalesAssignmentRequest): Promise<SalesAssignment> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-assignments/${id}`, {
        method: "PUT",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    deleteSalesAssignment: (id: string): Promise<void> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-assignments/${id}`, { method: "DELETE" }).then(() => undefined),

    getSalesPersonManagerChain: (id: string): Promise<SalesPerson[]> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-people/${id}/manager-chain`).then((r) => r.json()),

    getSalesPersonReports: (id: string): Promise<SalesPerson[]> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-people/${id}/reports`).then((r) => r.json()),

    getSalesOrgUnitDescendants: (id: string): Promise<string[]> =>
      apiFetch(SALESORG_BASE, `/api/v1/sales-org-units/${id}/descendants`).then((r) => r.json()),
  };
}

export type CrmApi = ReturnType<typeof createCrmApi>;
