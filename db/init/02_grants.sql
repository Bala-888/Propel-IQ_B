-- TASK-012: AuditLog INSERT-only privilege enforcement.
-- The application connects as 'app_user'. This role has INSERT only on audit_log —
-- no UPDATE or DELETE is permitted at the database level, satisfying HIPAA immutability.
--
-- NOTE: This script runs after EF Core migrations have created the audit_logs table.
-- If the table does not exist yet at init time, this is a no-op that will be re-applied
-- manually after the first migration run.

-- Create restricted application role (if not already created by EF Core migrations)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
        CREATE ROLE app_user WITH LOGIN PASSWORD 'REPLACE_AT_RUNTIME';
    END IF;
END
$$;

-- Grant full access to all tables EXCEPT audit_log
GRANT CONNECT ON DATABASE upacip TO app_user;
GRANT USAGE ON SCHEMA public TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO app_user;

-- CRITICAL: Revoke UPDATE and DELETE on audit_log — INSERT-only for app_user
REVOKE UPDATE, DELETE ON TABLE audit_logs FROM app_user;

-- Ensure future tables inherit the same grant pattern
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;
