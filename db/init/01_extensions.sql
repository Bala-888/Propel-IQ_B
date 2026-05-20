-- TASK-008: Install PostgreSQL extensions required by UPACIP
-- This script runs automatically on first `docker compose up` via the
-- /docker-entrypoint-initdb.d/ volume mount.

-- pgcrypto: AES-256 column-level PHI encryption via pgp_sym_encrypt / pgp_sym_decrypt
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- pgvector 0.5+: vector similarity search for RAG pipeline (dim=1536, ivfflat cosine)
CREATE EXTENSION IF NOT EXISTS vector;

-- uuid-ossp: server-side UUID generation fallback
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
