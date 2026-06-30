-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.manufacturers — tenant-specific (each tenant manages its own list)
-- Replaces the global ref.manufacturers as the source for catalog.products.manufacturer_id
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.manufacturers (
    id           UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id    UUID        NOT NULL REFERENCES catalog.tenants (id),
    name         TEXT        NOT NULL,
    code         TEXT,
    country_code CHAR(2),             -- ISO 3166-1 alpha-2
    website_url  TEXT,
    is_active    BOOL        NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at   TIMESTAMPTZ,
    CONSTRAINT uq_catalog_manufacturers_tenant_code UNIQUE (tenant_id, code)
);

CREATE INDEX idx_catalog_manufacturers_tenant ON catalog.manufacturers (tenant_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.brands — tenant-specific
-- Replaces the global ref.brands as the source for catalog.products.brand_id
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE catalog.brands (
    id              UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id       UUID        NOT NULL REFERENCES catalog.tenants (id),
    manufacturer_id UUID        REFERENCES catalog.manufacturers (id),
    name            TEXT        NOT NULL,
    code            TEXT,
    logo_url        TEXT,
    is_active       BOOL        NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_catalog_brands_tenant_code UNIQUE (tenant_id, code)
);

CREATE INDEX idx_catalog_brands_tenant         ON catalog.brands (tenant_id);
CREATE INDEX idx_catalog_brands_manufacturer   ON catalog.brands (manufacturer_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Row Level Security — same tenant isolation pattern as the rest of catalog.*
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE catalog.manufacturers ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.manufacturers FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.manufacturers
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

ALTER TABLE catalog.brands ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.brands FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.brands
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.products — repoint manufacturer_id / brand_id from ref.* to catalog.*
-- No data migration needed: no tenant data has been written yet (catalog.tenants
-- is provisioned lazily on first authenticated request).
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE catalog.products
    DROP CONSTRAINT IF EXISTS products_manufacturer_id_fkey,
    DROP CONSTRAINT IF EXISTS products_brand_id_fkey;

ALTER TABLE catalog.products
    ADD CONSTRAINT products_manufacturer_id_fkey
        FOREIGN KEY (manufacturer_id) REFERENCES catalog.manufacturers (id),
    ADD CONSTRAINT products_brand_id_fkey
        FOREIGN KEY (brand_id) REFERENCES catalog.brands (id);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.manufacturers / ref.brands are no longer referenced by catalog.products.
-- Left in place (harmless) in case another future ref-data consumer needs a
-- platform-wide manufacturer directory; the ProductManager API no longer reads them.
-- ─────────────────────────────────────────────────────────────────────────────
