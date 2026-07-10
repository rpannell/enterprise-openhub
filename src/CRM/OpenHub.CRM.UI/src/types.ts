// A lean picklist entry for the new SalesOrg module's sales people — the CRM UI only needs
// enough to populate a client's "sales person" dropdown, not full SalesPerson/Role/OrgUnit/
// Assignment management (that's a separate, sizeable follow-up UI under SalesOrg.UI).
export interface SalesPersonPicklistItem {
  id: string;
  displayName: string;
}

// Mirrors OpenHub.SalesOrg.Api's Models/Responses/V1/*.cs shapes — used by the "Sales Hierarchy"
// viewer (src/salesorg/SalesHierarchyView.tsx), not by the client picklist above.
export interface SalesPerson {
  id: string;
  uniqueId: string | null;
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  jobTitle: string | null;
  employeeNumber: string | null;
  activeStatus: boolean;
  startDate: string | null;
  endDate: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AvailableKeycloakUser {
  uniqueId: string;
  username: string;
  email: string | null;
  firstName: string | null;
  lastName: string | null;
}

export interface CreateSalesPersonRequest {
  uniqueId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string | null;
  jobTitle?: string | null;
  employeeNumber?: string | null;
  startDate?: string | null;
}

// No email: it's owned by the linked Keycloak identity, not editable here.
export interface UpdateSalesPersonRequest {
  firstName?: string;
  lastName?: string;
  phone?: string | null;
  jobTitle?: string | null;
  employeeNumber?: string | null;
  activeStatus?: boolean;
  startDate?: string | null;
  endDate?: string | null;
}

export interface SalesRole {
  id: string;
  name: string;
  description: string | null;
  parentRoleId: string | null;
  hierarchyLevel: number | null;
  activeStatus: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSalesRoleRequest {
  name: string;
  description?: string | null;
  parentRoleId?: string | null;
}

// No hierarchyLevel: it's derived server-side from parentRoleId, not client-supplied.
export interface UpdateSalesRoleRequest {
  name?: string;
  description?: string | null;
  parentRoleId?: string | null;
  activeStatus?: boolean;
}

export interface SalesOrganizationUnit {
  id: string;
  name: string;
  description: string | null;
  organizationUnitType: string;
  parentOrganizationUnitId: string | null;
  activeStatus: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSalesOrganizationUnitRequest {
  name: string;
  description?: string | null;
  organizationUnitType: string;
  parentOrganizationUnitId?: string | null;
}

export interface UpdateSalesOrganizationUnitRequest {
  name?: string;
  description?: string | null;
  organizationUnitType?: string;
  parentOrganizationUnitId?: string | null;
  activeStatus?: boolean;
}

export interface SalesAssignment {
  id: string;
  salesPersonId: string;
  salesPersonName: string | null;
  organizationUnitId: string;
  organizationUnitName: string | null;
  roleId: string;
  roleName: string | null;
  effectiveStartDate: string;
  effectiveEndDate: string | null;
  primaryAssignmentFlag: boolean;
  activeStatus: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSalesAssignmentRequest {
  salesPersonId: string;
  organizationUnitId: string;
  roleId: string;
  effectiveStartDate: string;
  effectiveEndDate?: string | null;
  primaryAssignmentFlag?: boolean;
}

export interface UpdateSalesAssignmentRequest {
  effectiveEndDate?: string | null;
  primaryAssignmentFlag?: boolean;
  activeStatus?: boolean;
}

export type AddressType = "billing" | "shipping" | "headquarters" | "other";

export interface ClientAddress {
  id?: string;
  addressType: AddressType;
  line1: string;
  line2?: string | null;
  city: string;
  stateProvince?: string | null;
  postalCode?: string | null;
  countryCode: string;
  isPrimary: boolean;
}

export type ContactPhoneType = "work" | "cell" | "home" | "other";

export interface ContactPhone {
  id?: string;
  phoneType: ContactPhoneType;
  phoneNumber: string;
  isPrimary: boolean;
}

export interface ClientContact {
  id?: string;
  firstName: string;
  lastName: string;
  title?: string | null;
  email?: string | null;
  isPrimary: boolean;
  notes?: string | null;
  phones: ContactPhone[];
}

export type ClientStatus = "prospect" | "active" | "on_hold" | "churned";
export type AccountTier = "strategic" | "key" | "standard" | "small";

export interface Client {
  id: string;
  name: string;
  legalName: string | null;
  keycloakOrganizationId: string | null;
  salesPersonId: string;
  salesPersonName: string | null;
  status: ClientStatus;
  industry: string | null;
  website: string | null;
  phone: string | null;
  annualRevenue: number | null;
  employeeCount: number | null;
  leadSource: string | null;
  accountTier: AccountTier;
  firstContactDate: string | null;
  lastActivityAt: string | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
  addresses: ClientAddress[];
  contacts: ClientContact[];
}

export interface CreateClientRequest {
  name: string;
  legalName?: string | null;
  salesPersonId: string;
  status?: ClientStatus;
  industry?: string | null;
  website?: string | null;
  phone?: string | null;
  annualRevenue?: number | null;
  employeeCount?: number | null;
  leadSource?: string | null;
  accountTier?: AccountTier;
  firstContactDate?: string | null;
  notes?: string | null;
  addresses: ClientAddress[];
  contacts: ClientContact[];
}

export interface UpdateClientRequest {
  name?: string;
  legalName?: string | null;
  salesPersonId?: string;
  status?: ClientStatus;
  industry?: string | null;
  website?: string | null;
  phone?: string | null;
  annualRevenue?: number | null;
  employeeCount?: number | null;
  leadSource?: string | null;
  accountTier?: AccountTier;
  firstContactDate?: string | null;
  notes?: string | null;
  addresses?: ClientAddress[];
  contacts?: ClientContact[];
}
