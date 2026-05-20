# Task - TASK_001

## Requirement Reference
- **User Story:** us_006
- **Story Location:** .propel/context/tasks/EP-DATA/us_006/us_006.md
- **Acceptance Criteria:**
  - AC-003: `app_user` role connected to the database cannot execute UPDATE or DELETE on `audit_logs` — PostgreSQL raises `ERROR: permission denied for table audit_logs`
  - AC-005: `app_user` role can successfully INSERT a row into `audit_logs` and the row count increases by 1
- **Edge Cases:**
  - N/A — PHI encryption, null handling, and key rotation edge cases are addressed in task_002

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
| Database | PostgreSQL | 15.3+ | TR-007, DR-002 (PostgreSQL role-based access control enforces audit log immutability); GRANT/REVOKE commands are PostgreSQL DDL |

---

## Task Overview

Create the `app_user` PostgreSQL login role and configure its permissions so it can operate all application tables while being prevented from modifying `audit_logs`. The role receives broad table-level DML rights on the public schema and then has UPDATE and DELETE explicitly revoked on `audit_logs`, making the audit log append-only for all application-layer code. The SQL must be idempotent so that running `docker compose up` on an already-initialised database volume does not error.

---

## Dependent Tasks
- task_001 (us_001) — PostgreSQL container and `app` database must be running; `docker/db/init.sql` stub must exist for appending role SQL
- task_002 (us_005) — All 12 domain tables including `audit_logs` must exist before GRANT statements can target them

---

## Impacted Components
- `docker/db/init.sql` — modified to add `app_user` role creation and permission grants/revokes

---

## Implementation Plan
1. In `docker/db/init.sql`, add `DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN CREATE ROLE app_user WITH LOGIN PASSWORD 'changeme'; END IF; END $$;` — password is a placeholder; actual value must be injected via the `APP_USER_PASSWORD` Docker Compose env var override or Kubernetes secret (OWASP A02)
2. Add `GRANT CONNECT ON DATABASE app TO app_user;`
3. Add `GRANT USAGE ON SCHEMA public TO app_user;`
4. Add `GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;` — grants broad application-level access to all tables
5. Add `REVOKE UPDATE, DELETE ON TABLE audit_logs FROM app_user;` — removes mutation rights on the audit table, leaving INSERT and SELECT only (AC-003)
6. Update the `db` service `environment` in `docker-compose.yml` to set `POSTGRES_USER` (or add a separate `APP_USER_PASSWORD` env var) so the password placeholder in init.sql can be replaced at runtime without hardcoding

---

## Current Project State
```
docker/
└── db/
    └── init.sql     (MODIFY — append app_user role SQL)
docker-compose.yml   (MODIFY — add APP_USER_PASSWORD env var to db service)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | docker/db/init.sql | Add idempotent `app_user` role creation, GRANT CONNECT/USAGE/DML on all tables, REVOKE UPDATE/DELETE on `audit_logs` |
| MODIFY | docker-compose.yml | Add `APP_USER_PASSWORD` environment variable to the `db` service for the `app_user` login role password |

---

## External References
- https://www.postgresql.org/docs/15/sql-grant.html (PostgreSQL 15 GRANT syntax — table-level DML privileges)
- https://www.postgresql.org/docs/15/sql-revoke.html (PostgreSQL 15 REVOKE — removing specific privileges)
- https://www.postgresql.org/docs/15/sql-createrole.html (PostgreSQL 15 CREATE ROLE WITH LOGIN)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Start the stack with `docker compose up --wait`, then connect as `app_user` and execute `UPDATE audit_logs SET id = id WHERE false` — PostgreSQL must return `ERROR: permission denied for table audit_logs` (AC-003)
- [ ] Connect as `app_user` and execute a valid INSERT into `audit_logs`; verify `SELECT COUNT(*) FROM audit_logs` increases by 1 (AC-005)

---

## Implementation Checklist
- [x] `app_user` role creation SQL in `docker/db/init.sql` is wrapped in an idempotent `DO $$ BEGIN IF NOT EXISTS ... END $$;` block to prevent error on repeated container startup (AC-003, AC-005 — stack stability)
- [x] `GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;` is added before the REVOKE so the broad grant is applied first and then selectively narrowed (AC-005 — INSERT on all other tables must remain permitted)
- [x] `REVOKE UPDATE, DELETE ON TABLE audit_logs FROM app_user;` is added after the broad GRANT, making `audit_logs` INSERT+SELECT only for the application role (AC-003)
- [x] The `app_user` password in `docker/db/init.sql` is a placeholder value and the `docker-compose.yml` `db` service environment block references `APP_USER_PASSWORD` — no hardcoded credentials in any committed file (OWASP A02)
- [x] The .NET API connection string (in `POSTGRES_CONNECTION_STRING` env var from us_005 task_001) is updated to use `app_user` credentials so all API database operations run under the restricted role (AC-003, AC-005 — role enforcement requires API connects as `app_user` not `postgres`)
