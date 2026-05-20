# Task - TASK_001

## Requirement Reference
- **User Story:** us_001
- **Story Location:** .propel/context/tasks/EP-TECH/us_001/us_001.md
- **Acceptance Criteria:**
  - AC-001: All 8 services (nginx, api, frontend, db, ollama, prometheus, grafana, seq) reach `healthy` state within 120 seconds of `docker compose up --wait` with zero non-zero exits
  - AC-002: Named volumes `db_data`, `ollama_models`, `seq_data`, `prometheus_data`, `grafana_data` persist data across `docker compose down` + `docker compose up` cycles
  - AC-004: Stateful containers (db, ollama, seq, prometheus, grafana) restart automatically within 30 seconds per `restart: always`
- **Edge Cases:**
  - Port conflict: If host ports 80, 443, 5432, 11434, 9090, 3000, or 5341 are already bound, compose must fail fast with a clear binding error — no silent fallback to alternate ports
  - Missing `.env`: If `PHI_ENCRYPTION_KEY` is absent, the `db` service healthcheck must exit non-zero and print a descriptive error; the stack must not start with a null encryption key
  - Ollama model not ready: On first start the Llama 3.1 8B model may not yet be pulled; the `api` service must not crash — it must defer returning HTTP 503 `{"error":"AI model not ready"}` to the HTTP layer (handled in us_003); the docker-compose `api` `depends_on` must not wait for the model pull to complete

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References
| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version | Justification |
|-------|------------|---------|---------------|
| Infrastructure | Docker Compose | 2.x | TR-014 (single-stack deployment), NFR-010 (free OSS only) |
| Infrastructure | PostgreSQL | 15.3+ | DR-001 (pgcrypto AES-256), DR-003 (ACID), TR-003 |
| Infrastructure | pgvector | 0.5+ | DR-006 (vector embeddings for RAG) |
| Infrastructure | Ollama | latest stable | AIR-001 (local Llama 3.1 8B inference, zero PHI transmission) |
| Infrastructure | Prometheus | 2.45+ | NFR-002 (uptime monitoring), TR-015 |
| Infrastructure | Grafana | 10+ | NFR-002 (operational dashboards), TR-015 |
| Infrastructure | Seq | 2023.4+ | NFR-006 (structured audit log), TR-016 |

---

## Task Overview

Author `docker-compose.yml` defining all 8 services (nginx, frontend, api, db, ollama, prometheus, grafana, seq) with Docker health checks, `depends_on: condition: service_healthy` ordering, 5 named persistent volumes, `restart: always` on stateful containers, and environment variable injection from `.env`. Create `.env.example` with all required variables and blocking validation comment for `PHI_ENCRYPTION_KEY`. Configure Prometheus scrape target for the `api` service.

---

## Dependent Tasks
- None — this is the foundational infrastructure task for the entire platform (us_001, EP-TECH)

---

## Impacted Components
- `docker-compose.yml` — new root-level compose file defining all 8 services
- `.env.example` — new template for all required environment variables
- `docker/prometheus/prometheus.yml` — new Prometheus scrape configuration
- `docker/db/init.sql` — new PostgreSQL init script enabling `pgcrypto` and `pgvector` extensions

---

## Implementation Plan
1. Create `docker-compose.yml` skeleton with `version: "3.9"` and `services:`, `volumes:`, `networks:` top-level keys
2. Add `db` service using `postgres:15-alpine`, mount `db_data` volume, configure `HEALTHCHECK` to test `pg_isready` and verify `PHI_ENCRYPTION_KEY` env var is set; set `restart: always`
3. Add `api` service using `build: ./src/api`; set `depends_on: db: condition: service_healthy`; expose internal port 8080; set `restart: on-failure`
4. Add `ollama` service using `ollama/ollama:latest`, mount `ollama_models` volume, add `HEALTHCHECK` hitting `GET /api/version`; set `restart: always`
5. Add `prometheus` service using `prom/prometheus:v2.45.0`, mount `prometheus_data` and bind-mount `docker/prometheus/prometheus.yml`; set `restart: always`
6. Add `grafana` service using `grafana/grafana:10.0.0`, mount `grafana_data`; set `restart: always`
7. Add `seq` service using `datalust/seq:2023.4`, mount `seq_data`, set `ACCEPT_EULA=Y`; set `restart: always`
8. Declare all 5 named volumes under top-level `volumes:` key and create `.env.example` with all required variables

---

## Current Project State
```
/
├── docker-compose.yml           (CREATE)
├── .env.example                 (CREATE)
├── docker/
│   ├── prometheus/
│   │   └── prometheus.yml       (CREATE)
│   └── db/
│       └── init.sql             (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | docker-compose.yml | 8-service compose file with health checks, volumes, restart policies, port bindings |
| CREATE | .env.example | Template listing PHI_ENCRYPTION_KEY, POSTGRES_PASSWORD, SEQ_API_KEY, GRAFANA_ADMIN_PASSWORD with inline comments |
| CREATE | docker/prometheus/prometheus.yml | Static scrape config targeting `api:8080/metrics` with 15s scrape interval |
| CREATE | docker/db/init.sql | `CREATE EXTENSION IF NOT EXISTS pgcrypto; CREATE EXTENSION IF NOT EXISTS vector;` |

---

## External References
- https://docs.docker.com/compose/compose-file/05-services/ (Docker Compose service spec — v3)
- https://hub.docker.com/_/postgres (PostgreSQL 15 image tags)
- https://hub.docker.com/r/ollama/ollama (Ollama image)
- https://hub.docker.com/r/datalust/seq (Seq 2023.4 image)
- https://prometheus.io/docs/prometheus/2.45/configuration/configuration/ (Prometheus 2.45 scrape config)
- https://hub.docker.com/r/grafana/grafana (Grafana 10 image)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] `docker compose up --wait` exits with code 0 and all 8 services show `Status: running (healthy)` in `docker compose ps`
- [x] `docker compose down` then `docker compose up` — query `SELECT 1 FROM pg_extension WHERE extname='pgvector'` returns a row (volume persistence AC-002)

---

## Implementation Checklist
- [x] `docker-compose.yml` defines exactly 8 services: nginx, frontend, api, db, ollama, prometheus, grafana, seq — each with `image:` or `build:` directive (AC-001)
- [x] Each service has a `healthcheck:` block and `depends_on:` uses `condition: service_healthy` for ordered startup; `--wait` flag resolves within 120s (AC-001)
- [x] `volumes:` top-level key declares `db_data`, `ollama_models`, `seq_data`, `prometheus_data`, `grafana_data` as named volumes with no `driver:` override (AC-002)
- [x] `restart: always` is set on db, ollama, seq, prometheus, grafana services; api uses `restart: on-failure` (AC-004)
- [x] Port bindings use `<host>:<container>` short syntax; no `0.0.0.0` catchall or range bindings that could silently reroute on conflict (Edge: port conflict)
- [x] `db` service `healthcheck` command includes a shell check `test -n "$PHI_ENCRYPTION_KEY"` before `pg_isready`; compose exits non-zero if var is unset (Edge: missing .env)
- [x] `.env.example` lists every required variable with `# REQUIRED:` comment prefix and `# OPTIONAL:` for non-critical vars; `.gitignore` includes `.env` (Edge: missing .env)
- [x] `docker/prometheus/prometheus.yml` declares a `static_configs` target for `api:8080/metrics` and `docker/db/init.sql` runs `CREATE EXTENSION IF NOT EXISTS pgcrypto; CREATE EXTENSION IF NOT EXISTS vector;` (AC-001 — db service must be fully functional on startup)
