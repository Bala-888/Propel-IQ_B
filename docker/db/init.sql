-- init.sql
-- Executed once on first PostgreSQL container start via docker-entrypoint-initdb.d/
-- Enables extensions required by the UPACIP platform.

-- pgcrypto: AES-256-GCM PHI field encryption (DR-001)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- pgvector: 1536-dim vector embeddings for RAG retrieval (DR-006)
CREATE EXTENSION IF NOT EXISTS vector;
