-- ─────────────────────────────────────────────────────────────────────────────
-- crm.tenants
-- slug must exactly match the Keycloak realm name — this is how tenant is resolved from JWT iss
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE crm.tenants (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    slug       TEXT        NOT NULL,  -- matches Keycloak realm name exactly
    name       TEXT        NOT NULL,
    plan_tier  TEXT        NOT NULL DEFAULT 'standard',
    is_active  BOOL        NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ,
    CONSTRAINT uq_crm_tenants_slug UNIQUE (slug)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- crm.users
-- unique_id = Keycloak sub claim (unique within a realm, not globally)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE crm.users (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id  UUID        NOT NULL REFERENCES crm.tenants (id),
    unique_id  TEXT        NOT NULL,  -- Keycloak sub claim
    email      TEXT        NOT NULL,
    first_name TEXT        NOT NULL,
    last_name  TEXT        NOT NULL,
    is_active  BOOL        NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ,
    CONSTRAINT uq_crm_users_tenant_unique_id UNIQUE (tenant_id, unique_id),
    CONSTRAINT uq_crm_users_tenant_email     UNIQUE (tenant_id, email)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- crm.clients  (the account/business record — a prospective Keycloak Organization)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE crm.clients (
    id                          UUID          NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id                   UUID          NOT NULL REFERENCES crm.tenants (id),
    -- References sales_org.sales_people (a different database/module) — cross-database
    -- REFERENCES aren't possible in Postgres, so this is a bare UUID column. Validity is
    -- enforced at the API layer via ISalesOrgApiAccessor.
    sales_person_id             UUID          NOT NULL,
    name                        TEXT          NOT NULL,
    legal_name                  TEXT,
    keycloak_organization_id    TEXT,   -- NULL until the Keycloak Organization is created
    keycloak_organization_alias TEXT,
    status                      TEXT          NOT NULL DEFAULT 'prospect',
    industry                    TEXT,
    website                     TEXT,
    phone                       TEXT,
    annual_revenue              NUMERIC(19,2),
    employee_count              INT,
    lead_source                 TEXT,
    account_tier                TEXT          NOT NULL DEFAULT 'standard',
    first_contact_date          DATE,
    last_activity_at            TIMESTAMPTZ,
    notes                       TEXT,
    create_user_id              UUID          NOT NULL REFERENCES crm.users (id),
    modify_user_id              UUID          NOT NULL REFERENCES crm.users (id),
    delete_user_id              UUID          REFERENCES crm.users (id),
    created_at                  TIMESTAMPTZ   NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ   NOT NULL DEFAULT now(),
    deleted_at                  TIMESTAMPTZ,
    CONSTRAINT ck_clients_status CHECK (status IN ('prospect', 'active', 'on_hold', 'churned')),
    CONSTRAINT ck_clients_account_tier CHECK (account_tier IN ('strategic', 'key', 'standard', 'small'))
);

-- ─────────────────────────────────────────────────────────────────────────────
-- crm.client_addresses  (a client can have several — billing, shipping, HQ, branch)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE crm.client_addresses (
    id             UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id      UUID        NOT NULL REFERENCES crm.tenants (id),
    client_id      UUID        NOT NULL REFERENCES crm.clients (id),
    address_type   TEXT        NOT NULL DEFAULT 'billing',
    line1          TEXT        NOT NULL,
    line2          TEXT,
    city           TEXT        NOT NULL,
    state_province TEXT,
    postal_code    TEXT,
    country_code   CHAR(2)     NOT NULL,  -- ISO 3166-1 alpha-2
    is_primary     BOOL        NOT NULL DEFAULT FALSE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at     TIMESTAMPTZ,
    CONSTRAINT ck_client_addresses_type CHECK (
        address_type IN ('billing', 'shipping', 'headquarters', 'other')
    )
);

-- ─────────────────────────────────────────────────────────────────────────────
-- crm.client_contacts  (the people at the client — distinct from client-user logins,
-- which are a later phase)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE crm.client_contacts (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id  UUID        NOT NULL REFERENCES crm.tenants (id),
    client_id  UUID        NOT NULL REFERENCES crm.clients (id),
    first_name TEXT        NOT NULL,
    last_name  TEXT        NOT NULL,
    title      TEXT,
    email      TEXT,
    phone      TEXT,
    is_primary BOOL        NOT NULL DEFAULT FALSE,
    notes      TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ
);
