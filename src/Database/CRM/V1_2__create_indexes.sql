-- ─────────────────────────────────────────────────────────────────────────────
-- crm.clients
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_clients_tenant
    ON crm.clients (tenant_id);

CREATE INDEX idx_clients_tenant_sales_person
    ON crm.clients (tenant_id, sales_person_id);

CREATE INDEX idx_clients_tenant_status
    ON crm.clients (tenant_id, status)
    WHERE deleted_at IS NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- crm.client_addresses
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_client_addresses_tenant_client
    ON crm.client_addresses (tenant_id, client_id);

-- Enforces at most one primary address per client per tenant (no trigger needed)
CREATE UNIQUE INDEX idx_client_addresses_primary
    ON crm.client_addresses (tenant_id, client_id)
    WHERE is_primary = TRUE AND deleted_at IS NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- crm.client_contacts
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_client_contacts_tenant_client
    ON crm.client_contacts (tenant_id, client_id);

CREATE UNIQUE INDEX idx_client_contacts_primary
    ON crm.client_contacts (tenant_id, client_id)
    WHERE is_primary = TRUE AND deleted_at IS NULL;
