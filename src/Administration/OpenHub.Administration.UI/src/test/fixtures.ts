import type { HostAuthContext } from "../federation/HostAuthContext";
import type { CreatedUser, CreateUserRequest, Organization, OrgUser, Role, UpdateUserRequest } from "../types";

export function buildHostAuth(overrides: Partial<HostAuthContext> = {}): HostAuthContext {
  return {
    roles: ["platform-admin", "tenant-admin"],
    realmSlug: "client-a",
    getToken: async () => "test-token",
    ...overrides,
  };
}

export function buildOrganization(overrides: Partial<Organization> = {}): Organization {
  return {
    id: "org-1",
    realmSlug: "client-a",
    name: "Client A Inc.",
    alias: "client-a",
    enabled: true,
    domains: ["client-a.com"],
    ...overrides,
  };
}

export function buildUser(overrides: Partial<OrgUser> = {}): OrgUser {
  return {
    id: "user-1",
    username: "jdoe",
    email: "jdoe@client-a.com",
    firstName: "Jane",
    lastName: "Doe",
    enabled: true,
    roles: ["product-manager"],
    ...overrides,
  };
}

export function buildRole(overrides: Partial<Role> = {}): Role {
  return {
    name: "product-manager",
    description: "Product catalog access",
    ...overrides,
  };
}

export function buildCreatedUser(overrides: Partial<CreatedUser> = {}): CreatedUser {
  return {
    ...buildUser(),
    temporaryPassword: "Tmp-abc123!",
    ...overrides,
  };
}

export function buildCreateUserRequest(overrides: Partial<CreateUserRequest> = {}): CreateUserRequest {
  return {
    email: "jdoe@client-a.com",
    ...overrides,
  };
}

export function buildUpdateUserRequest(overrides: Partial<UpdateUserRequest> = {}): UpdateUserRequest {
  return {
    email: "jdoe@client-a.com",
    enabled: true,
    ...overrides,
  };
}
