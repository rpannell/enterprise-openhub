-- ─────────────────────────────────────────────────────────────────────────────
-- Non-superuser role for ProductManager.Api's runtime connection.
--
-- Postgres superusers ALWAYS bypass Row Level Security, and FORCE ROW LEVEL SECURITY
-- (see V1_5__create_rls_policies.sql) explicitly does not override that. Flyway and any
-- other admin tooling keep running as the superuser (postgres) — only the application's
-- own runtime connection string should authenticate as app_user, or the tenant_isolation
-- policies on catalog.users/products/etc. silently do nothing.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'app_user') THEN
        CREATE ROLE app_user WITH LOGIN NOSUPERUSER PASSWORD '${app_user_password}';
    ELSE
        ALTER ROLE app_user WITH PASSWORD '${app_user_password}';
    END IF;
END
$$;

GRANT USAGE ON SCHEMA catalog TO app_user;
GRANT USAGE ON SCHEMA ref TO app_user;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA catalog TO app_user;
GRANT SELECT ON ALL TABLES IN SCHEMA ref TO app_user;

-- So future migrations' new tables are usable by app_user without a follow-up GRANT.
ALTER DEFAULT PRIVILEGES IN SCHEMA catalog GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA ref GRANT SELECT ON TABLES TO app_user;
