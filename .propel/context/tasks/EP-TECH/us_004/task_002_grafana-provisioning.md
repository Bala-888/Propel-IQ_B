# Task - TASK_002

## Requirement Reference
- **User Story:** us_004
- **Story Location:** .propel/context/tasks/EP-TECH/us_004/us_004.md
- **Acceptance Criteria:**
  - AC-002: Grafana UI at `http://localhost:3000` shows the base dashboard with at least three panels (request latency, error rate, uptime), each with live data points from the last 5 minutes
- **Edge Cases:**
  - Grafana datasource provisioning failure: If the Prometheus hostname in the datasource YAML is incorrect, the Grafana UI must display `Error: could not connect to Prometheus` — not a blank dashboard; the datasource config must use the internal Docker hostname `prometheus` (not `localhost`)

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
| Infrastructure | Grafana | 10+ | TR-015 (operational dashboards), NFR-002 (uptime and request monitoring dashboards) |
| Infrastructure | Prometheus | 2.45+ | TR-015 (datasource for Grafana panels) |

---

## Task Overview

Configure Grafana auto-provisioning via `docker/grafana/provisioning/` so that on `docker compose up` Grafana automatically registers the Prometheus datasource and loads the base UPACIP dashboard. The base dashboard must contain three panels: (1) Request Latency (`histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))`), (2) Error Rate (`rate(http_requests_received_total{code=~"5.."}[5m])`), (3) API Uptime (`up{job="api"}`). Datasource YAML must use the internal Docker hostname `prometheus:9090` to prevent the edge-case blank dashboard on hostname misconfiguration.

---

## Dependent Tasks
- task_001 (us_001) — `docker-compose.yml` must define the `grafana` service with the bind-mount of `docker/grafana/provisioning/`
- task_001 (us_004) — Prometheus scrape config must be complete so panels have live data to display

---

## Impacted Components
- `docker/grafana/provisioning/datasources/prometheus.yml` — new Grafana datasource provisioning YAML
- `docker/grafana/provisioning/dashboards/dashboard.yml` — new Grafana dashboard provider YAML
- `docker/grafana/provisioning/dashboards/upacip-base.json` — new Grafana dashboard JSON with 3 panels

---

## Implementation Plan
1. Create `docker/grafana/provisioning/datasources/prometheus.yml` with `apiVersion: 1`, `datasources: [{name: Prometheus, type: prometheus, url: http://prometheus:9090, access: proxy, isDefault: true}]` — use internal Docker hostname `prometheus`, not `localhost`
2. Create `docker/grafana/provisioning/dashboards/dashboard.yml` with `apiVersion: 1`, `providers: [{name: Default, folder: '', type: file, options: {path: /var/lib/grafana/dashboards}}]`
3. Create `docker/grafana/provisioning/dashboards/upacip-base.json` as a valid Grafana dashboard JSON with `uid: upacip-base`, `title: UPACIP Base`, and three panel objects:
   - Panel 1 `Request Latency p95` — type `timeseries` — query `histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))`
   - Panel 2 `Error Rate 5xx` — type `timeseries` — query `rate(http_requests_received_total{code=~"5.."}[5m])`
   - Panel 3 `API Up` — type `stat` — query `up{job="api"}`
4. Bind-mount `docker/grafana/provisioning/` to `/etc/grafana/provisioning` in the `grafana` service in `docker-compose.yml` (if not already set in us_001); also bind-mount `docker/grafana/provisioning/dashboards/` to `/var/lib/grafana/dashboards`
5. Set `GF_AUTH_ANONYMOUS_ENABLED=true` and `GF_AUTH_ANONYMOUS_ORG_ROLE=Viewer` environment variables on the `grafana` service for frictionless local access (development only)

---

## Current Project State
```
docker/
└── grafana/
    └── provisioning/
        ├── datasources/
        │   └── prometheus.yml     (CREATE)
        └── dashboards/
            ├── dashboard.yml      (CREATE)
            └── upacip-base.json   (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | docker/grafana/provisioning/datasources/prometheus.yml | Grafana datasource YAML pointing to `http://prometheus:9090` with `isDefault: true` |
| CREATE | docker/grafana/provisioning/dashboards/dashboard.yml | Grafana dashboard provider YAML referencing `/var/lib/grafana/dashboards` |
| CREATE | docker/grafana/provisioning/dashboards/upacip-base.json | Grafana dashboard JSON with 3 panels: request latency p95, error rate 5xx, API uptime |
| MODIFY | docker-compose.yml | Add bind-mount of `docker/grafana/provisioning/` to `/etc/grafana/provisioning` and dashboard dir to `/var/lib/grafana/dashboards` on the `grafana` service |

---

## External References
- https://grafana.com/docs/grafana/v10.0/administration/provisioning/#datasources (Grafana 10 datasource provisioning)
- https://grafana.com/docs/grafana/v10.0/administration/provisioning/#dashboards (Grafana 10 dashboard provisioning)
- https://grafana.com/docs/grafana/v10.0/dashboards/build-dashboards/create-dashboard/ (Grafana dashboard JSON format)
- https://prometheus.io/docs/prometheus/2.45/querying/functions/#histogram_quantile (histogram_quantile for latency panels)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `docker compose up --wait` then open `http://localhost:3000` — base dashboard loads with three panels each showing live data in the last 5-minute window (AC-002)
- [ ] Temporarily change datasource URL to an incorrect hostname; restart Grafana — datasource health check shows descriptive error message, not a blank dashboard (Edge: provisioning failure)

---

## Implementation Checklist
- [x] `docker/grafana/provisioning/datasources/prometheus.yml` sets `url: http://prometheus:9090` using the Docker Compose internal hostname — not `localhost:9090` (Edge: datasource provisioning failure)
- [x] Dashboard provider YAML maps `/var/lib/grafana/dashboards` as the `path` under `options`; `docker-compose.yml` bind-mounts the dashboard JSON dir to that exact path (AC-002)
- [x] `upacip-base.json` contains exactly three panel objects with non-empty `targets[0].expr` values: p95 latency histogram, 5xx error rate, and `up{job="api"}` (AC-002)
- [x] `GF_AUTH_ANONYMOUS_ENABLED=true` and `GF_AUTH_ANONYMOUS_ORG_ROLE=Viewer` are set as environment variables on the `grafana` service — dev-only convenience, not a production security setting (AC-002 — frictionless UI access in dev)
- [x] Each panel `datasource` field references the provisioned datasource name `Prometheus` so panels bind correctly on first load (AC-002)
