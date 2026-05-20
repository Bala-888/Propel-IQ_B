# Task - TASK_002

## Requirement Reference
- **User Story:** us_003
- **Story Location:** .propel/context/tasks/EP-TECH/us_003/us_003.md
- **Acceptance Criteria:**
  - AC-002 (Nginx): `GET https://localhost/api/health` from outside the Docker network is reverse-proxied to the .NET container; no request reaches the API over plain HTTP from outside the Docker bridge; response includes `X-Service: api` header (set by backend, preserved by Nginx)
  - AC-003: Clients attempting TLS 1.0 or TLS 1.1 connections receive an SSL handshake error — connection is refused
- **Edge Cases:**
  - N/A — JWT_SECRET and CI retry edge cases are addressed in task_001 and task_003 respectively

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
| Security | Nginx TLS (ssl_protocols TLSv1.2 TLSv1.3) | Built-in (OpenSSL) | NFR-001 (HIPAA transport), NFR-005 (AES-256 ciphers, TLS 1.2+), TR-008 |

---

## Task Overview

Extend the `docker/nginx/nginx.conf` created in us_001 to add a dedicated `location /api/` reverse-proxy block targeting the `.NET api` service on its internal Docker port, forwarding all required proxy headers, and ensuring that the plain-HTTP (port 80) server block does **not** include a `location /api/` passthrough — only the HTTPS block proxies API traffic. Confirm `ssl_protocols TLSv1.2 TLSv1.3` is the only ssl_protocols directive (no TLSv1 or TLSv1.1) and add HTTP Strict Transport Security (HSTS) header to the 443 block.

---

## Dependent Tasks
- task_002 (us_001) — `docker/nginx/nginx.conf` and Nginx `Dockerfile` must exist before this task's modifications apply
- task_001 (us_003) — The `.NET api` service must be defined before the proxy target `http://api:8080` is valid

---

## Impacted Components
- `docker/nginx/nginx.conf` — modified to add `/api/` proxy location block and HSTS header

---

## Implementation Plan
1. In the HTTPS `server` block (port 443) of `nginx.conf`, add `location /api/ { proxy_pass http://api:8080/; ... }` with full proxy header set
2. Set proxy headers: `proxy_set_header Host $host;`, `proxy_set_header X-Real-IP $remote_addr;`, `proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;`, `proxy_set_header X-Forwarded-Proto https;`
3. Add `proxy_read_timeout 30s;` and `proxy_connect_timeout 5s;` to the `/api/` location to prevent indefinite hangs
4. Add `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;` to the HTTPS server block
5. Verify the port-80 HTTP `server` block contains **only** `return 301 https://$host$request_uri;` — no `location /api/` block — ensuring API is unreachable over plain HTTP from outside Docker

---

## Current Project State
```
docker/
└── nginx/
    ├── nginx.conf        (MODIFY — add /api/ proxy location + HSTS)
    ├── Dockerfile        (exists — no change needed)
    └── ssl/
        └── generate-dev-cert.sh  (exists — no change needed)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | docker/nginx/nginx.conf | Add `location /api/` reverse-proxy block in HTTPS server block with proxy headers; add HSTS header; confirm no `/api/` passthrough in HTTP server block |

---

## External References
- https://nginx.org/en/docs/http/ngx_http_proxy_module.html (Nginx proxy_pass and proxy_set_header directives)
- https://nginx.org/en/docs/http/ngx_http_headers_module.html (add_header directive — HSTS)
- https://nginx.org/en/docs/http/ngx_http_ssl_module.html (ssl_protocols — TLS 1.2/1.3 enforcement)
- https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Strict-Transport-Security (HSTS specification)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] `curl -I https://localhost/api/health --insecure` returns HTTP 200 and `X-Service: api` header is present in the response (AC-002)
- [x] `openssl s_client -connect localhost:443 -tls1` (or `-tls1_1`) returns `handshake failure` — connection is refused for TLS 1.0/1.1 (AC-003)

---

## Implementation Checklist
- [x] `nginx.conf` HTTPS block contains `location /api/ { proxy_pass http://api:8080/; }` with `proxy_set_header X-Forwarded-Proto https;` and `proxy_set_header X-Real-IP $remote_addr;` (AC-002)
- [x] `proxy_read_timeout 30s;` and `proxy_connect_timeout 5s;` are set in the `/api/` location block to prevent gateway hangs (AC-002 — reliability)
- [x] `nginx.conf` HTTP (port 80) `server` block contains only the `return 301` directive — no `location /api/` block — ensuring API traffic is not reachable over plain HTTP from outside the Docker network (AC-002)
- [x] `ssl_protocols TLSv1.2 TLSv1.3;` is the sole `ssl_protocols` directive in the HTTPS server block; no `TLSv1` or `TLSv1.1` entries exist in the entire config file (AC-003)
- [x] `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;` is present in the HTTPS `server` block (NFR-001 HIPAA transport, OWASP A05)
