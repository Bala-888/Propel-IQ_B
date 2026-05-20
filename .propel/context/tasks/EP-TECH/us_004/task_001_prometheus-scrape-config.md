# Task - TASK_001

## Requirement Reference
- **User Story:** us_004
- **Story Location:** .propel/context/tasks/EP-TECH/us_004/us_004.md
- **Acceptance Criteria:**
  - AC-001: Prometheus UI at `http://localhost:9090/targets` shows the `api` scrape target with `State: UP` and last scrape timestamp < 30 seconds ago
- **Edge Cases:**
  - N/A — Grafana datasource provisioning edge case is addressed in task_002

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
| Infrastructure | Prometheus | 2.45+ | TR-015 (metrics collection), NFR-002 (uptime and request monitoring) |

---

## Task Overview

Author the `docker/prometheus/prometheus.yml` scrape configuration (started in us_001 task_001) with a complete `scrape_configs` entry targeting the `api` service on its internal Docker network hostname and port. Set a 15-second global scrape interval. The resulting config must cause the Prometheus `api` target to display `State: UP` immediately after `docker compose up --wait` completes.

---

## Dependent Tasks
- task_001 (us_001) — `docker-compose.yml` must define the `prometheus` service with the bind-mount of `docker/prometheus/prometheus.yml`
- task_001 (us_003) — The .NET API `/metrics` endpoint must exist before Prometheus can successfully scrape it

---

## Impacted Components
- `docker/prometheus/prometheus.yml` — modified to add the full `scrape_configs` section targeting `api:8080/metrics`

---

## Implementation Plan
1. Open `docker/prometheus/prometheus.yml` (created as a stub in us_001 task_001) and add `global: scrape_interval: 15s` and `evaluation_interval: 15s` at the top level
2. Add `scrape_configs:` with a single job: `job_name: api`, `static_configs: targets: ['api:8080']`, `metrics_path: /metrics`
3. Confirm the internal Docker Compose service hostname used in `targets` is `api` (matching the `services:` key in `docker-compose.yml`) and the port matches the Kestrel port declared in the `api` service definition
4. Add `honor_labels: true` to preserve any labels the .NET API sets on its own metrics

---

## Current Project State
```
docker/
└── prometheus/
    └── prometheus.yml     (MODIFY — add scrape_configs targeting api:8080/metrics)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | docker/prometheus/prometheus.yml | Add `global` scrape/evaluation interval (15s) and `scrape_configs` entry for `api:8080/metrics` |

---

## External References
- https://prometheus.io/docs/prometheus/2.45/configuration/configuration/#scrape_config (Prometheus 2.45 scrape_config spec)
- https://prometheus.io/docs/prometheus/2.45/getting_started/#configuring-prometheus-to-monitor-itself (Prometheus getting started — target config pattern)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] `docker compose up --wait` then open `http://localhost:9090/targets` — `api` target shows `State: UP` (AC-001)
- [x] `http://localhost:9090/graph?g0.expr=http_requests_received_total` returns data points confirming the API metrics are being scraped (AC-001)

---

## Implementation Checklist
- [x] `prometheus.yml` sets `global: scrape_interval: 15s` and `evaluation_interval: 15s` (AC-001 — scrape recency < 30s)
- [x] `scrape_configs` defines `job_name: api` with `static_configs: [{targets: ['api:8080']}]` and `metrics_path: /metrics` (AC-001)
- [x] The `targets` hostname `api` matches the exact Docker Compose service name from `docker-compose.yml` — not `localhost` or an IP — so DNS resolution works inside the Docker bridge network (AC-001)
- [x] `honor_labels: true` is set on the `api` scrape job to preserve metric labels set by prometheus-net (AC-001 — data fidelity)
