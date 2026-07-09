-- ─────────────────────────────────────────────────────────────────────────────
-- Row Level Security — tenant isolation
--
-- FORCE ROW LEVEL SECURITY is critical: without it the table owner role bypasses
-- RLS silently, which would be a security hole in a multi-tenant system.
--
-- The application sets the tenant context per transaction:
--   SET LOCAL app.current_tenant_id = '<uuid>';
--
-- crm.tenants is intentionally excluded — it is managed by a platform admin
-- role that operates above the tenant boundary.
-- ─────────────────────────────────────────────────────────────────────────────

-- crm.users
ALTER TABLE crm.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE crm.users FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON crm.users
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- crm.clients
ALTER TABLE crm.clients ENABLE ROW LEVEL SECURITY;
ALTER TABLE crm.clients FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON crm.clients
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- crm.client_addresses
ALTER TABLE crm.client_addresses ENABLE ROW LEVEL SECURITY;
ALTER TABLE crm.client_addresses FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON crm.client_addresses
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

-- crm.client_contacts
ALTER TABLE crm.client_contacts ENABLE ROW LEVEL SECURITY;
ALTER TABLE crm.client_contacts FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON crm.client_contacts
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);
