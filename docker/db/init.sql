-- init.sql
-- Executed once on first PostgreSQL container start via docker-entrypoint-initdb.d/
-- Enables extensions required by the UPACIP platform.

-- pgcrypto: AES-256-GCM PHI field encryption (DR-001)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- pgvector: 1536-dim vector embeddings for RAG retrieval (DR-006)
CREATE EXTENSION IF NOT EXISTS vector;

-- ── app_user role ──────────────────────────────────────────────────────────
-- Idempotent: wraps CREATE ROLE in DO block to prevent error on repeated startup.
-- Password is a placeholder — override via APP_USER_PASSWORD env var in production.
-- OWASP A02: no real credentials committed to source; placeholder must be rotated
-- before deployment using ALTER ROLE app_user WITH PASSWORD '<secret>'.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
        CREATE ROLE app_user WITH LOGIN PASSWORD 'changeme';
    END IF;
END $$;

-- Allow app_user to connect to the application database
GRANT CONNECT ON DATABASE app TO app_user;

-- Allow app_user to see objects in the public schema
GRANT USAGE ON SCHEMA public TO app_user;

-- Grant broad DML access on all current tables — must precede the REVOKE below
-- so the selective narrowing on audit_logs takes effect (AC-005)
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;

-- Ensure future tables also get the same default privileges
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;

-- AC-003: make audit_logs append-only for app_user — remove mutation rights
-- INSERT and SELECT remain; UPDATE and DELETE are explicitly denied
REVOKE UPDATE, DELETE ON TABLE audit_logs FROM app_user;
