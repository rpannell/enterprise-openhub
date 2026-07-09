-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.tenants
-- slug must exactly match the Keycloak realm name — this is how tenant is resolved from JWT iss
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE sales_org.tenants (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    slug       TEXT        NOT NULL,  -- matches Keycloak realm name exactly
    name       TEXT        NOT NULL,
    plan_tier  TEXT        NOT NULL DEFAULT 'standard',
    is_active  BOOL        NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ,
    CONSTRAINT uq_salesorg_tenants_slug UNIQUE (slug)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.users
-- unique_id = Keycloak sub claim (unique within a realm, not globally)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE sales_org.users (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id  UUID        NOT NULL REFERENCES sales_org.tenants (id),
    unique_id  TEXT        NOT NULL,  -- Keycloak sub claim
    email      TEXT        NOT NULL,
    first_name TEXT        NOT NULL,
    last_name  TEXT        NOT NULL,
    is_active  BOOL        NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ,
    CONSTRAINT uq_salesorg_users_tenant_unique_id UNIQUE (tenant_id, unique_id),
    CONSTRAINT uq_salesorg_users_tenant_email     UNIQUE (tenant_id, email)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_people
-- tenant_id is a bare UUID column (no cross-schema/cross-database REFERENCES),
-- matching how catalog/crm schemas handle tenant scoping without a shared tenants table
-- across modules.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE sales_org.sales_people (
    id               UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id        UUID        NOT NULL,
    first_name       TEXT        NOT NULL,
    last_name        TEXT        NOT NULL,
    email            TEXT        NOT NULL,
    phone            TEXT,
    job_title        TEXT,
    employee_number  TEXT,
    active_status     BOOL        NOT NULL DEFAULT TRUE,
    start_date       DATE,
    end_date         DATE,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at       TIMESTAMPTZ
);

-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_roles
-- Tenant-configurable — no hardcoded role list. HierarchyLevel is nullable: a role
-- with no configured level can still exist but never outranks anyone in the
-- manager-chain algorithm (see SalesOrgHierarchyEngine).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE sales_org.sales_roles (
    id             UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id      UUID        NOT NULL,
    name           TEXT        NOT NULL,
    description    TEXT,
    hierarchy_level INT,
    active_status   BOOL        NOT NULL DEFAULT TRUE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at     TIMESTAMPTZ
);

-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_organization_units  (self-referential hierarchy — arbitrary depth)
-- organization_unit_type is a free TEXT column (app-level allow-list), not a DB CHECK
-- enum, so new unit types can be added without a migration.
-- Full audit fields (create/modify/delete user id + timestamps) because this table is
-- the record of structural reorgs.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE sales_org.sales_organization_units (
    id                          UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id                   UUID        NOT NULL,
    name                        TEXT        NOT NULL,
    description                 TEXT,
    organization_unit_type      TEXT        NOT NULL,
    parent_organization_unit_id UUID        REFERENCES sales_org.sales_organization_units (id),
    active_status                BOOL        NOT NULL DEFAULT TRUE,
    create_user_id              UUID        NOT NULL,
    modify_user_id              UUID        NOT NULL,
    delete_user_id              UUID,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at                  TIMESTAMPTZ
);

-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_assignments  (time-boxed: a person's assignment to a unit + role)
-- History is preserved by never overwriting a row in place — moving a person means
-- closing their old assignment (effective_end_date set) and creating a new one.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE sales_org.sales_assignments (
    id                       UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id                UUID        NOT NULL,
    sales_person_id          UUID        NOT NULL REFERENCES sales_org.sales_people (id),
    organization_unit_id     UUID        NOT NULL REFERENCES sales_org.sales_organization_units (id),
    role_id                  UUID        NOT NULL REFERENCES sales_org.sales_roles (id),
    effective_start_date     DATE        NOT NULL,
    effective_end_date       DATE,  -- NULL = current
    primary_assignment_flag  BOOL        NOT NULL DEFAULT TRUE,
    active_status             BOOL        NOT NULL DEFAULT TRUE,
    create_user_id           UUID        NOT NULL,
    modify_user_id           UUID        NOT NULL,
    delete_user_id           UUID,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at                TIMESTAMPTZ
);
