-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_people.unique_id — links a sales person to their Keycloak
-- identity (Keycloak sub claim, same convention as sales_org.users.unique_id).
-- Nullable: existing rows predate this link and aren't retroactively matched.
-- New sales people are created by picking an existing Keycloak user, so the
-- app always supplies this going forward.
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE sales_org.sales_people ADD COLUMN unique_id TEXT;

ALTER TABLE sales_org.sales_people
    ADD CONSTRAINT uq_salesorg_sales_people_tenant_unique_id UNIQUE (tenant_id, unique_id);
