import type {
  Client,
  SalesPersonPicklistItem,
  SalesPerson,
  SalesOrganizationUnit,
} from "../types";

// Deliberately not importing HostAuthContext's own type here — mirroring its shape locally
// keeps this fixture free of the "../federation/HostAuthContext" import.
type HostAuthContextLike = {
  roles: string[];
  realmSlug: string;
  getToken: () => Promise<string>;
};

export function buildHostAuth(overrides: Partial<HostAuthContextLike> = {}): HostAuthContextLike {
  return {
    roles: ["crm-user"],
    realmSlug: "client-a",
    getToken: async () => "test-token",
    ...overrides,
  };
}

export function buildSalesPerson(overrides: Partial<SalesPersonPicklistItem> = {}): SalesPersonPicklistItem {
  return {
    id: "person-1",
    displayName: "Jane Rep",
    ...overrides,
  };
}

export function buildSalesPersonFull(overrides: Partial<SalesPerson> = {}): SalesPerson {
  return {
    id: "person-1",
    firstName: "Jane",
    lastName: "Rep",
    email: "jane.rep@example.com",
    phone: null,
    jobTitle: "Territory Manager",
    employeeNumber: null,
    activeStatus: true,
    startDate: null,
    endDate: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

export function buildSalesOrgUnit(overrides: Partial<SalesOrganizationUnit> = {}): SalesOrganizationUnit {
  return {
    id: "unit-1",
    name: "Carolinas District",
    description: null,
    organizationUnitType: "district",
    parentOrganizationUnitId: null,
    activeStatus: true,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

export function buildClient(overrides: Partial<Client> = {}): Client {
  return {
    id: "client-1",
    name: "Acme Co",
    legalName: null,
    keycloakOrganizationId: "kc-org-1",
    salesPersonId: "person-1",
    salesPersonName: "Jane Rep",
    status: "prospect",
    industry: "Manufacturing",
    website: null,
    phone: null,
    annualRevenue: null,
    employeeCount: null,
    leadSource: null,
    accountTier: "standard",
    firstContactDate: null,
    lastActivityAt: null,
    notes: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    addresses: [],
    contacts: [],
    ...overrides,
  };
}
