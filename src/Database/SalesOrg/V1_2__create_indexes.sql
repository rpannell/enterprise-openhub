-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_people
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_sales_people_tenant ON sales_org.sales_people (tenant_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_roles
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_sales_roles_tenant ON sales_org.sales_roles (tenant_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_organization_units
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_sales_org_units_tenant ON sales_org.sales_organization_units (tenant_id);
CREATE INDEX idx_sales_org_units_parent ON sales_org.sales_organization_units (parent_organization_unit_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_assignments
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_sales_assignments_tenant ON sales_org.sales_assignments (tenant_id);
CREATE INDEX idx_sales_assignments_person ON sales_org.sales_assignments (tenant_id, sales_person_id);
CREATE INDEX idx_sales_assignments_unit ON sales_org.sales_assignments (tenant_id, organization_unit_id);

-- Enforces at most one active+primary assignment per (tenant_id, sales_person_id) —
-- a person can only have one "home" assignment at a time; moving them means closing
-- the old assignment (effective_end_date set, active_status = false) first.
CREATE UNIQUE INDEX idx_sales_assignments_one_active_primary
    ON sales_org.sales_assignments (tenant_id, sales_person_id)
    WHERE primary_assignment_flag = TRUE AND active_status = TRUE AND deleted_at IS NULL;
