-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.products
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_products_tenant
    ON catalog.products (tenant_id);

CREATE INDEX idx_products_tenant_manufacturer
    ON catalog.products (tenant_id, manufacturer_id);

CREATE INDEX idx_products_tenant_brand
    ON catalog.products (tenant_id, brand_id);

CREATE INDEX idx_products_tenant_status
    ON catalog.products (tenant_id, availability_status_id);

-- Hot path: listing active, published products for a tenant
CREATE INDEX idx_products_tenant_listing
    ON catalog.products (tenant_id, is_published, is_active)
    WHERE deleted_at IS NULL;

-- Supplier catalog sync: look up by manufacturer + MPN
CREATE INDEX idx_products_tenant_mpn
    ON catalog.products (tenant_id, manufacturer_id, manufacturer_part_number);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.products_history  (GiST for range queries)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_products_history_id_period
    ON catalog.products_history USING GIST (id, sys_period);

CREATE INDEX idx_products_history_tenant_period
    ON catalog.products_history USING GIST (tenant_id, sys_period);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_identifiers
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_product_identifiers_tenant_product
    ON catalog.product_identifiers (tenant_id, product_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_images
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_product_images_tenant_product
    ON catalog.product_images (tenant_id, product_id);

-- Enforces at most one primary image per product per tenant (no trigger needed)
CREATE UNIQUE INDEX idx_product_images_primary
    ON catalog.product_images (tenant_id, product_id)
    WHERE is_primary = TRUE AND deleted_at IS NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_attributes
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_product_attributes_tenant_product
    ON catalog.product_attributes (tenant_id, product_id);

CREATE INDEX idx_product_attributes_tenant_definition
    ON catalog.product_attributes (tenant_id, attribute_definition_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_categories
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_product_categories_tenant_product
    ON catalog.product_categories (tenant_id, product_id);

CREATE INDEX idx_product_categories_tenant_category
    ON catalog.product_categories (tenant_id, category_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_prices
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_product_prices_tenant_product
    ON catalog.product_prices (tenant_id, product_id);

-- Hot path: fetch current active prices
CREATE INDEX idx_product_prices_active
    ON catalog.product_prices (tenant_id, product_id, price_type_id, currency_id)
    WHERE is_active = TRUE AND deleted_at IS NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.product_price_history
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_price_history_tenant_product
    ON catalog.product_price_history (tenant_id, product_id);

CREATE INDEX idx_price_history_changed_at
    ON catalog.product_price_history (changed_at);
