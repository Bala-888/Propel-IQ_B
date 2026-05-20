# Task - TASK_002

## Requirement Reference
- **User Story:** us_001
- **Story Location:** .propel/context/tasks/EP-TECH/us_001/us_001.md
- **Acceptance Criteria:**
  - AC-003: A GET request to `http://localhost:80/` receives HTTP 301 with `Location: https://localhost/`
  - AC-005: A GET request to `https://localhost/` returns HTTP 200 with `Content-Type: text/html` and the React SPA shell HTML
- **Edge Cases:**
  - (Port conflict and .env edge cases are Infrastructure/Docker concerns addressed in task_001; no additional Nginx-specific edge cases beyond the two ACs)

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
| Infrastructure | Nginx | 1.25+ | TR-008 (TLS termination at reverse proxy), NFR-005 (TLS 1.2+ enforcement) |
| Security | Nginx TLS (self-signed dev cert) | Built-in (OpenSSL) | NFR-001 (HIPAA transport encryption), NFR-005 (AES-256 cipher suite, TLS 1.2+), TR-008 |

---

## Task Overview

Author `docker/nginx/nginx.conf` and the Nginx `Dockerfile` to implement:
1. An HTTP (port 80) server block that issues a permanent `301 Moved Permanently` redirect to the HTTPS equivalent URL.
2. An HTTPS (port 443) server block that terminates TLS, enforces TLS 1.2/1.3 with strong cipher suites, serves the built React SPA static files from `/usr/share/nginx/html` using `try_files` SPA fallback, and reverse-proxies `/api/` requests to the `api` service container.
3. A `generate-dev-cert.sh` shell script that generates a self-signed certificate for local development using `openssl`.

---

## Dependent Tasks
- task_001 — `docker-compose.yml` must define the `nginx` service before this config is applied to it

---

## Impacted Components
- `docker/nginx/nginx.conf` — new Nginx virtual host configuration
- `docker/nginx/Dockerfile` — new Nginx container image definition
- `docker/nginx/ssl/generate-dev-cert.sh` — new self-signed certificate generation script

---

## Implementation Plan
1. Create `docker/nginx/nginx.conf` with a `server` block listening on port 80 returning `return 301 https://$host$request_uri;`
2. Add a second `server` block listening on port 443 with `ssl_certificate`, `ssl_certificate_key`, `ssl_protocols TLSv1.2 TLSv1.3`, and `ssl_ciphers` restricted to ECDHE/AES-GCM suites
3. Add `location /` block with `root /usr/share/nginx/html;` and `try_files $uri $uri/ /index.html;` for React Router SPA fallback
4. Add `location /api/` block with `proxy_pass http://api:8080/;`, `proxy_set_header Host $host;`, and `proxy_set_header X-Real-IP $remote_addr;`
5. Set `Cache-Control: max-age=31536000, immutable` for hashed JS/CSS assets; `Cache-Control: no-cache` for `index.html`
6. Create `docker/nginx/Dockerfile` using `nginx:1.25-alpine` base; `COPY nginx.conf /etc/nginx/conf.d/default.conf`; `COPY ssl/ /etc/nginx/ssl/`; run `openssl` in build step for dev cert
7. Create `docker/nginx/ssl/generate-dev-cert.sh` with `openssl req -x509 -nodes -days 365 -newkey rsa:2048` command targeting `/etc/nginx/ssl/`

---

## Current Project State
```
/
├── docker-compose.yml              (exists after task_001)
├── docker/
│   └── nginx/
│       ├── nginx.conf              (CREATE)
│       ├── Dockerfile              (CREATE)
│       └── ssl/
│           └── generate-dev-cert.sh (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | docker/nginx/nginx.conf | HTTP redirect + HTTPS TLS server blocks with SPA fallback and API reverse proxy |
| CREATE | docker/nginx/Dockerfile | nginx:1.25-alpine image with config copy and dev cert generation via openssl |
| CREATE | docker/nginx/ssl/generate-dev-cert.sh | Self-signed RSA 2048 cert generation script for local development |

---

## External References
- https://nginx.org/en/docs/http/ngx_http_ssl_module.html (Nginx SSL module — ssl_protocols, ssl_ciphers)
- https://nginx.org/en/docs/http/ngx_http_core_module.html#try_files (try_files directive for SPA)
- https://hub.docker.com/_/nginx (Nginx 1.25 Alpine image tags)
- https://www.openssl.org/docs/man1.1.1/man1/req.html (openssl req — self-signed cert generation)
- https://ssl-config.mozilla.org/ (Mozilla SSL Configuration Generator — TLS 1.2/1.3 intermediate profile)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] `curl -I http://localhost:80/` returns HTTP 301 with `Location: https://localhost/` header
- [x] `curl -k -I https://localhost/` returns HTTP 200 with `Content-Type: text/html` header confirming SPA shell is served

---

## Implementation Checklist
- [x] `nginx.conf` port-80 `server` block contains `return 301 https://$host$request_uri;` and no other `location` blocks (AC-003)
- [x] `nginx.conf` port-443 `server` block sets `ssl_protocols TLSv1.2 TLSv1.3` and restricts `ssl_ciphers` to ECDHE-ECDSA-AES128-GCM-SHA256 / ECDHE-RSA-AES128-GCM-SHA256 suites or the Mozilla intermediate profile (NFR-005 / TR-008)
- [x] `location /` uses `root /usr/share/nginx/html;` and `try_files $uri $uri/ /index.html;` so direct navigation to any React route returns SPA shell (AC-005)
- [x] `location /api/` reverse-proxies to `api:8080` with `proxy_set_header Host`, `X-Real-IP`, and `X-Forwarded-Proto https` headers (AC-005 integration)
- [x] `index.html` is served with `Cache-Control: no-cache, no-store, must-revalidate` and hashed static assets with `Cache-Control: max-age=31536000, immutable` (AC-005 correctness)
- [x] `docker/nginx/Dockerfile` copies `nginx.conf` and runs `openssl req -x509 -nodes -days 365 -newkey rsa:2048` to produce dev TLS cert at image build time; no hardcoded secrets in the Dockerfile (AC-003, AC-005, OWASP A02 — no credentials in image layers)
