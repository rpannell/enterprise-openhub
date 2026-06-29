-- Required for composite GiST indexes that mix UUID + TSTZRANGE (temporal table indexes)
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE SCHEMA IF NOT EXISTS ref;
CREATE SCHEMA IF NOT EXISTS catalog;

COMMENT ON SCHEMA ref     IS 'Shared reference / lookup data — no tenant scoping, no RLS';
COMMENT ON SCHEMA catalog IS 'Tenant-scoped product catalog data — RLS enforced on all tables';
