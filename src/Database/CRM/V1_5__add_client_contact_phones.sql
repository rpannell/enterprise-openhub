-- ─────────────────────────────────────────────────────────────────────────────
-- crm.client_contact_phones  (a contact can have several — work, cell, home, other)
--
-- Replaces crm.client_contacts.phone (a single free-text column) — one phone number
-- couldn't represent "call them at work, but text their cell" the way a real CRM needs.
-- ON DELETE CASCADE: a contact's phones are meaningless once the contact itself is gone,
-- and ClientAccessor.ReplaceContactsAsync always replaces the full contact list wholesale
-- (delete-all-insert-all), so this cascade is what actually cleans up orphaned phone rows.
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE crm.client_contacts DROP COLUMN phone;

CREATE TABLE crm.client_contact_phones (
    id           UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id    UUID        NOT NULL REFERENCES crm.tenants (id),
    contact_id   UUID        NOT NULL REFERENCES crm.client_contacts (id) ON DELETE CASCADE,
    phone_type   TEXT        NOT NULL DEFAULT 'work',
    phone_number TEXT        NOT NULL,
    is_primary   BOOL        NOT NULL DEFAULT FALSE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at   TIMESTAMPTZ,
    CONSTRAINT ck_client_contact_phones_type CHECK (
        phone_type IN ('work', 'cell', 'home', 'other')
    )
);

CREATE INDEX idx_client_contact_phones_tenant_contact
    ON crm.client_contact_phones (tenant_id, contact_id);

ALTER TABLE crm.client_contact_phones ENABLE ROW LEVEL SECURITY;
ALTER TABLE crm.client_contact_phones FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON crm.client_contact_phones
    USING (tenant_id = current_setting('app.current_tenant_id')::UUID);

GRANT SELECT, INSERT, UPDATE, DELETE ON crm.client_contact_phones TO app_user;
