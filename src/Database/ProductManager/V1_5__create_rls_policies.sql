-- ─────────────────────────────────────────────────────────────────────────────
-- Row Level Security — tenant isolation
--
-- FORCE ROW LEVEL SECURITY is critical: without it the table owner role bypasses
-- RLS silently, which would be a security hole in a multi-tenant system.
--
-- The application sets the tenant context per transaction:
--   SET LOCAL app.current_tenant_id = '<uuid>';
--
-- catalog.tenants is intentionally excluded — it is managed by a platform admin
-- role that operates above the tenant boundary.
-- ─────────────────────────────────────────────────────────────────────────────

-- catalog.users
ALTER TABLE catalog.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.users FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.users
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- catalog.products
ALTER TABLE catalog.products ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.products FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.products
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- catalog.products_history
ALTER TABLE catalog.products_history ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.products_history FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.products_history
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- catalog.product_identifiers
ALTER TABLE catalog.product_identifiers ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.product_identifiers FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.product_identifiers
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- catalog.product_images
ALTER TABLE catalog.product_images ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.product_images FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.product_images
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- catalog.product_attributes
ALTER TABLE catalog.product_attributes ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.product_attributes FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.product_attributes
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- catalog.product_categories
ALTER TABLE catalog.product_categories ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.product_categories FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.product_categories
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- catalog.product_prices
ALTER TABLE catalog.product_prices ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.product_prices FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.product_prices
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- catalog.product_price_history
ALTER TABLE catalog.product_price_history ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.product_price_history FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.product_price_history
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);
