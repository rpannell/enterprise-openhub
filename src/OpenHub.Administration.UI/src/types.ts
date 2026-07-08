export interface Organization {
  id: string;
  realmSlug: string;
  name: string;
  alias: string;
  enabled: boolean;
  domains: string[];
}

export interface CreateOrganizationRequest {
  realmSlug: string;
  name: string;
  alias: string;
  domains?: string[];
}

export interface OrgUser {
  id: string;
  username: string;
  email: string | null;
  firstName: string | null;
  lastName: string | null;
  enabled: boolean;
  roles: string[];
}

export interface CreateUserRequest {
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  initialRole?: string;
}

export interface Role {
  name: string;
  description: string | null;
}
