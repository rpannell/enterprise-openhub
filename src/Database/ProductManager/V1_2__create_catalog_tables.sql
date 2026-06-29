-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.tenants
-- slug must exactly match the Keycloak realm name — this is how tenant is resolved from JWT iss
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.tenants (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    slug       TEXT        NOT NULL,  -- matches Keycloak realm name exactly
    name       TEXT        NOT NULL,
    plan_tier  TEXT        NOT NULL DEFAULT 'standard',
    is_active  BOOL        NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ,
    CONSTRAINT uq_tenants_slug UNIQUE (slug)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.users
-- unique_id = Keycloak sub claim (unique within a realm, not globally)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.users (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id  UUID        NOT NULL REFERENCES catalog.tenants (id),
    unique_id  TEXT        NOT NULL,  -- Keycloak sub claim
    email      TEXT        NOT NULL,
    first_name TEXT        NOT NULL,
    last_name  TEXT        NOT NULL,
    is_active  BOOL        NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ,
    CONSTRAINT uq_users_tenant_unique_id UNIQUE (tenant_id, unique_id),
    CONSTRAINT uq_users_tenant_email     UNIQUE (tenant_id, email)
);

CREATE INDEX idx_users_tenant    ON catalog.users (tenant_id);
CREATE INDEX idx_users_unique_id ON catalog.users (tenant_id, unique_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.products
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.products (
    id                      UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id               UUID         NOT NULL REFERENCES catalog.tenants (id),
    sku                     TEXT         NOT NULL,
    name                    TEXT         NOT NULL,
    description             TEXT,
    short_description       TEXT,
    manufacturer_id         UUID         REFERENCES ref.manufacturers (id),
    brand_id                UUID         REFERENCES ref.brands (id),
    condition_type_id       UUID         REFERENCES ref.condition_types (id),
    availability_status_id  UUID         REFERENCES ref.availability_statuses (id),
    base_uom_id             UUID         REFERENCES ref.units_of_measure (id),
    manufacturer_part_number TEXT,
    is_active               BOOL         NOT NULL DEFAULT TRUE,
    is_published            BOOL         NOT NULL DEFAULT FALSE,
    weight                  NUMERIC(10,4),
    weight_uom_id           UUID         REFERENCES ref.units_of_measure (id),
    create_user_id          UUID         NOT NULL REFERENCES catalog.users (id),
    modify_user_id          UUID         NOT NULL REFERENCES catalog.users (id),
    delete_user_id          UUID         REFERENCES catalog.users (id),
    sys_period              TSTZRANGE    NOT NULL DEFAULT tstzrange(now(), NULL),
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ  NOT NULL DEFAULT now(),
    deleted_at              TIMESTAMPTZ,
    CONSTRAINT uq_products_tenant_sku UNIQUE (tenant_id, sku)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.products_history  (temporal shadow — append-only, no FK constraints)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.products_history (
    id                      UUID,
    tenant_id               UUID,
    sku                     TEXT,
    name                    TEXT,
    description             TEXT,
    short_description       TEXT,
    manufacturer_id         UUID,
    brand_id                UUID,
    condition_type_id       UUID,
    availability_status_id  UUID,
    base_uom_id             UUID,
    manufacturer_part_number TEXT,
    is_active               BOOL,
    is_published            BOOL,
    weight                  NUMERIC(10,4),
    weight_uom_id           UUID,
    create_user_id          UUID,
    modify_user_id          UUID,
    delete_user_id          UUID,
    sys_period              TSTZRANGE    NOT NULL,
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    deleted_at              TIMESTAMPTZ
);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_identifiers  (UPC, EAN, GTIN-14, ASIN, etc.)
-- MPN is NOT stored here — use catalog.products.manufacturer_part_number
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.product_identifiers (
    id               UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id        UUID        NOT NULL REFERENCES catalog.tenants (id),
    product_id       UUID        NOT NULL REFERENCES catalog.products (id),
    identifier_type  TEXT        NOT NULL, -- UPC | EAN | GTIN14 | ASIN | ISBN | INTERNAL
    identifier_value TEXT        NOT NULL,
    is_primary       BOOL        NOT NULL DEFAULT FALSE,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT uq_product_identifiers UNIQUE (tenant_id, identifier_type, identifier_value)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_images
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.product_images (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id  UUID        NOT NULL REFERENCES catalog.tenants (id),
    product_id UUID        NOT NULL REFERENCES catalog.products (id),
    url        TEXT        NOT NULL,
    alt_text   TEXT,
    is_primary BOOL        NOT NULL DEFAULT FALSE,
    sort_order INT         NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ
);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_attributes  (EAV — typed value columns avoid casting errors)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.product_attributes (
    id                    UUID          NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id             UUID          NOT NULL REFERENCES catalog.tenants (id),
    product_id            UUID          NOT NULL REFERENCES catalog.products (id),
    attribute_definition_id UUID        NOT NULL REFERENCES ref.attribute_definitions (id),
    value_text            TEXT,
    value_integer         BIGINT,
    value_decimal         NUMERIC(19,6),
    value_boolean         BOOL,
    created_at            TIMESTAMPTZ   NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT uq_product_attributes UNIQUE (tenant_id, product_id, attribute_definition_id)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_categories  (many-to-many; products can be in multiple categories)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.product_categories (
    id          UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id   UUID        NOT NULL REFERENCES catalog.tenants (id),
    product_id  UUID        NOT NULL REFERENCES catalog.products (id),
    category_id UUID        NOT NULL REFERENCES ref.categories (id),
    is_primary  BOOL        NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_product_categories UNIQUE (tenant_id, product_id, category_id)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_prices
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.product_prices (
    id              UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id       UUID         NOT NULL REFERENCES catalog.tenants (id),
    product_id      UUID         NOT NULL REFERENCES catalog.products (id),
    price_type_id   UUID         NOT NULL REFERENCES ref.price_types (id),
    currency_id     UUID         NOT NULL REFERENCES ref.currencies (id),
    amount          NUMERIC(19,4) NOT NULL,
    effective_from  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    effective_until TIMESTAMPTZ,
    is_active       BOOL         NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_product_prices UNIQUE (tenant_id, product_id, price_type_id, currency_id, effective_from)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_price_history  (insert-only audit log)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.product_price_history (
    id               UUID          NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id        UUID          NOT NULL REFERENCES catalog.tenants (id),
    product_price_id UUID          NOT NULL REFERENCES catalog.product_prices (id),
    product_id       UUID          NOT NULL,
    price_type_id    UUID          NOT NULL,
    currency_id      UUID          NOT NULL,
    old_amount       NUMERIC(19,4),
    new_amount       NUMERIC(19,4) NOT NULL,
    changed_at       TIMESTAMPTZ   NOT NULL DEFAULT now(),
    changed_by       UUID          REFERENCES catalog.users (id),
    change_reason    TEXT
);
