-- ─────────────────────────────────────────────────────────────────────────────
-- Non-superuser role for SalesOrg.Api's runtime connection.
--
-- Postgres superusers ALWAYS bypass Row Level Security, and FORCE ROW LEVEL SECURITY
-- (see V1_3__create_rls_policies.sql) explicitly does not override that. Flyway and any
-- other admin tooling keep running as the superuser (postgres) — only the application's
-- own runtime connection string should authenticate as app_user, or the tenant_isolation
-- policies on sales_org.users/sales_people/etc. silently do nothing.
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

GRANT USAGE ON SCHEMA sales_org TO app_user;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA sales_org TO app_user;

-- So future migrations' new tables are usable by app_user without a follow-up GRANT.
ALTER DEFAULT PRIVILEGES IN SCHEMA sales_org GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;
