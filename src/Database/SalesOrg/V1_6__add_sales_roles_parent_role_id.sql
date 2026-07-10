-- ─────────────────────────────────────────────────────────────────────────────
-- sales_org.sales_roles.parent_role_id — the role this one "reports up to" in
-- rank (e.g. Sales Rep -> District Manager -> Regional -> CEO). The app derives
-- hierarchy_level automatically from this chain (parent's level - 100, or 1000
-- for a role with no parent) instead of asking the caller to pick a raw number.
-- hierarchy_level itself is unchanged/still persisted — it's what the manager
-- chain algorithm actually compares — this column is just how it now gets set.
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE sales_org.sales_roles ADD COLUMN parent_role_id UUID REFERENCES sales_org.sales_roles (id);
