-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.products — cover the default listing sort
-- GetAllAsync/GetPagedAsync always filter (tenant_id, deleted_at IS NULL) and
-- ORDER BY name; idx_products_tenant_listing covers the filter but not the sort,
-- so every list/page call paid for a Sort node before Skip/Take could apply.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_products_tenant_name
    ON catalog.products (tenant_id, name)
    WHERE deleted_at IS NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.products — condition_type_id filter (GetPagedAsync) had no matching
-- index, unlike its sibling filters (manufacturer_id, brand_id, availability_status_id).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_products_tenant_condition
    ON catalog.products (tenant_id, condition_type_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.products — trigram search support
-- GetPagedAsync's search filter uses ILIKE '%term%' (leading wildcard) against
-- sku/name, which no btree index (including uq_products_tenant_sku) can serve —
-- it always falls back to a per-tenant sequential scan. pg_trgm + GIN lets
-- Postgres use an index for substring matches.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX idx_products_sku_trgm
    ON catalog.products USING gin (sku gin_trgm_ops);

CREATE INDEX idx_products_name_trgm
    ON catalog.products USING gin (name gin_trgm_ops);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.users — drop redundant index
-- uq_users_tenant_unique_id already creates a unique index on (tenant_id, unique_id);
-- idx_users_unique_id duplicated the same columns in the same order, adding write
-- overhead with no query benefit.
-- ─────────────────────────────────────────────────────────────────────────────
DROP INDEX catalog.idx_users_unique_id;
