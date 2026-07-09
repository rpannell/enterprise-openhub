-- ─────────────────────────────────────────────────────────────────────────────
-- Row Level Security — tenant isolation
--
-- FORCE ROW LEVEL SECURITY is critical: without it the table owner role bypasses
-- RLS silently, which would be a security hole in a multi-tenant system.
--
-- The application sets the tenant context per transaction:
--   SET LOCAL app.current_tenant_id = '<uuid>';
--
-- sales_org.tenants is intentionally excluded — it is managed by a platform admin
-- role that operates above the tenant boundary.
-- ─────────────────────────────────────────────────────────────────────────────

-- sales_org.users
ALTER TABLE sales_org.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales_org.users FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sales_org.users
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- sales_org.sales_people
ALTER TABLE sales_org.sales_people ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales_org.sales_people FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sales_org.sales_people
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- sales_org.sales_roles
ALTER TABLE sales_org.sales_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales_org.sales_roles FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sales_org.sales_roles
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- sales_org.sales_organization_units
ALTER TABLE sales_org.sales_organization_units ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales_org.sales_organization_units FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sales_org.sales_organization_units
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- sales_org.sales_assignments
ALTER TABLE sales_org.sales_assignments ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales_org.sales_assignments FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sales_org.sales_assignments
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);
