# Task - TASK_003

## Requirement Reference
- **User Story:** us_003
- **Story Location:** .propel/context/tasks/EP-TECH/us_003/us_003.md
- **Acceptance Criteria:**
  - AC-005: A pull request to `main` triggers `.github/workflows/ci.yml`; the workflow runs lint, `dotnet build`, `npm run build`, `docker compose up --wait`, and a smoke-test `curl` against `/api/health`; all steps complete with exit code 0 within 10 minutes
- **Edge Cases:**
  - CI Docker layer cache invalidation: If `dotnet restore` fails due to NuGet rate limiting, the workflow must automatically retry the restore step once before failing the pipeline

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
| DevOps/CI | GitHub Actions | latest (ubuntu-latest runner) | NFR-010 (free tier CI, no paid services), AC-005 mandated |
| Backend | .NET SDK | 8.0 | TR-002 (mandated backend runtime) — used in CI `dotnet build` step |
| Frontend | Node.js | 20.x LTS | NFR-010 (free OSS) — used in CI `npm run build` and `npm run lint` steps |
| Infrastructure | Docker Compose | 2.x | TR-014 (single-stack deployment) — used in CI integration step |

---

## Task Overview

Author `.github/workflows/ci.yml` as a GitHub Actions workflow that triggers on `pull_request` targeting `main`. The workflow runs on `ubuntu-latest` with a single `ci` job containing ordered steps: repository checkout, Node.js 20.x setup + `npm ci` + `npm run lint`, .NET 8 SDK setup + `dotnet restore` (with one automatic retry on failure) + `dotnet build`, `npm run build`, `docker compose up --wait` with a 120-second timeout, and a final `curl -f --insecure https://localhost/api/health` smoke test. The workflow uses GitHub Actions caching for `dotnet` NuGet packages and `node_modules` to reduce execution time within the 10-minute budget.

---

## Dependent Tasks
- task_001 (us_003) — `.NET API` must build successfully before the CI workflow can validate it
- task_002 (us_001) — `docker-compose.yml` must define the full 8-service stack for the `docker compose up --wait` step

---

## Impacted Components
- `.github/workflows/ci.yml` — new GitHub Actions CI workflow file

---

## Implementation Plan
1. Create `.github/workflows/ci.yml` with `on: pull_request: branches: [main]` trigger and `jobs: ci: runs-on: ubuntu-latest`
2. Add step `actions/checkout@v4` as the first step
3. Add step `actions/setup-node@v4` with `node-version: '20'`; follow with `npm ci --prefix frontend` and `npm run lint --prefix frontend`
4. Add step `actions/setup-dotnet@v4` with `dotnet-version: '8.0.x'`; add NuGet package cache using `actions/cache@v4` keyed on `src/api/Api.csproj` hash; add `dotnet restore src/api` step with `continue-on-error: false`
5. Wrap the `dotnet restore` step with retry logic using `nick-fields/retry@v3` action: `max_attempts: 2`, `retry_wait_seconds: 30` — covers the NuGet rate-limiting edge case
6. Add `dotnet build src/api --configuration Release --no-restore` step
7. Add `npm run build --prefix frontend` step (depends on lint passing)
8. Add `docker compose up --wait` step with `timeout-minutes: 3`; follow with `curl -f --insecure https://localhost/api/health` smoke-test step

---

## Current Project State
```
.github/
└── workflows/
    └── ci.yml             (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | .github/workflows/ci.yml | GitHub Actions CI workflow: checkout → lint → dotnet restore (retry) → dotnet build → npm build → docker compose up --wait → smoke test |

---

## External References
- https://docs.github.com/en/actions/writing-workflows/workflow-syntax-for-github-actions (GitHub Actions workflow syntax)
- https://github.com/actions/setup-dotnet (actions/setup-dotnet@v4 — .NET 8.0.x)
- https://github.com/actions/setup-node (actions/setup-node@v4 — Node.js 20)
- https://github.com/nick-fields/retry (nick-fields/retry@v3 — step retry action)
- https://github.com/actions/cache (actions/cache@v4 — NuGet and npm caching)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Open a test pull request against `main`; confirm the `ci` workflow appears in the PR checks panel and all steps show green within 10 minutes (AC-005)
- [ ] Temporarily remove the NuGet cache step and throttle the network (or simulate); confirm the `dotnet restore` step is retried once before succeeding or failing (Edge: CI Docker cache)

---

## Implementation Checklist
- [ ] `ci.yml` trigger is `on: pull_request: branches: [main]` — workflow fires exactly on PRs to `main`, not on direct pushes (AC-005)
- [ ] `npm ci --prefix frontend` followed by `npm run lint --prefix frontend` is an explicit step with `name: Lint frontend` — lint failure blocks subsequent steps (AC-005)
- [ ] `dotnet restore` is wrapped in `nick-fields/retry@v3` with `max_attempts: 2` and `retry_wait_seconds: 30` — retries once on failure before marking the step failed (Edge: CI Docker cache retry)
- [ ] `dotnet build src/api --configuration Release --no-restore` step uses `--no-restore` to avoid double restore and respects the cached packages from the restore step (AC-005)
- [ ] `npm run build --prefix frontend` step produces the `frontend/dist/` artifact; step fails if `tsc` or `vite build` exits non-zero (AC-005)
- [ ] `docker compose up --wait` step has `timeout-minutes: 3`; the smoke-test step `curl -f --insecure https://localhost/api/health` follows immediately and fails the job if it returns non-200 (AC-005)
- [ ] NuGet packages are cached using `actions/cache@v4` keyed on `hashFiles('**/Api.csproj')` to speed up subsequent runs within the 10-minute budget (AC-005 — performance)
