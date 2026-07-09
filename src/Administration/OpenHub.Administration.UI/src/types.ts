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

/** Returned only from createOrganizationUser — see CreateUserModal's post-create screen. */
export interface CreatedUser extends OrgUser {
  temporaryPassword?: string;
}

export interface CreateUserRequest {
  email: string;
  firstName?: string;
  lastName?: string;
  initialRoles?: string[];
}

export interface UpdateUserRequest {
  email: string;
  firstName?: string;
  lastName?: string;
  enabled: boolean;
}

export interface Role {
  name: string;
  description: string | null;
}
