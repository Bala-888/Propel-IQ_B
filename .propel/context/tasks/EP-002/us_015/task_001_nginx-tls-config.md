# Task - TASK_001

## Requirement Reference
- **User Story:** us_015
- **Story Location:** .propel/context/tasks/EP-002/us_015/us_015.md
- **Acceptance Criteria:**
  - AC-002: Nginx is configured with `ssl_protocols TLSv1.2 TLSv1.3` only; an `openssl s_client -tls1_1` connection attempt is refused with `SSL routines::no protocols available`
  - AC-004: All HTTP requests receive a permanent HTTP 301 redirect to the HTTPS equivalent URL; no API response body is served over plain HTTP
- **Edge Cases:**
  - Mixed-content SPA requests: the `Content-Security-Policy` response header must include the `upgrade-insecure-requests` directive to prevent the React SPA from issuing `http://` API calls when loaded over HTTPS
  - HTTP Strict Transport Security: the `Strict-Transport-Security` header must be set to enforce HTTPS at the browser level and prevent TLS-stripping attacks

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
| Infrastructure | Nginx | 1.25+ | TR-005 (TLS termination; HTTP→HTTPS redirect; security response headers; established in us_001 and us_002) |

---

## Task Overview

Harden the Nginx TLS configuration to satisfy HIPAA in-transit protection requirements. Remove TLS 1.0 and 1.1 from the accepted protocol list, enforce a strong ECDHE cipher suite, redirect all HTTP traffic to HTTPS with a permanent 301, and add the `Content-Security-Policy: upgrade-insecure-requests` and `Strict-Transport-Security` response headers. All changes are in `nginx/nginx.conf` (or the Docker Compose–mounted equivalent) and the Docker Compose service definition.

---

## Dependent Tasks
- task_001 (us_001) — Nginx service in Docker Compose must already be running on ports 80/443 with TLS certificates mounted; this task modifies the existing config rather than creating it

---

## Impacted Components
- `nginx/nginx.conf` — modified: `ssl_protocols`, `ssl_ciphers`, HTTP redirect server block, security response headers
- `docker-compose.yml` — verified: port 443 mapping and TLS certificate volume mounts remain intact

---

## Implementation Plan
1. In the HTTPS `server` block of `nginx/nginx.conf`, replace any existing `ssl_protocols` directive with `ssl_protocols TLSv1.2 TLSv1.3;` — remove `TLSv1` and `TLSv1.1` entirely; add `ssl_prefer_server_ciphers on;` (AC-002)
2. Add `ssl_ciphers` directive with a strong ECDHE/AES-GCM suite: `ssl_ciphers 'ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305';` — excludes RC4, 3DES, and DH suites below 2048-bit (AC-002 — cipher hardening; OWASP A02)
3. Add a dedicated HTTP `server` block on port 80: `server { listen 80; server_name _; return 301 https://$host$request_uri; }` — this block must appear before the HTTPS block; no `location` rules that would serve API content over HTTP (AC-004)
4. In the HTTPS `server` block, add `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains; preload" always;` — `always` ensures the header is sent for all response codes including 4xx and 5xx (Edge: HSTS; OWASP A05)
5. In the HTTPS `server` block, add `add_header Content-Security-Policy "default-src 'self'; upgrade-insecure-requests;" always;` — `upgrade-insecure-requests` instructs the browser to rewrite any `http://` resource requests to `https://` before sending; prevents SPA mixed-content downgrade (Edge: mixed-content SPA; OWASP A05)

---

## Current Project State
```
nginx/
└── nginx.conf                                       (MODIFY — ssl_protocols, ssl_ciphers, HTTP redirect block, security headers)
docker-compose.yml                                   (VERIFY — port 443 mapping + cert volume mounts unchanged)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | nginx/nginx.conf | ssl_protocols TLSv1.2 TLSv1.3; ssl_ciphers ECDHE suite; HTTP 301 redirect block; HSTS + CSP headers |
| VERIFY | docker-compose.yml | Confirm port 443 and TLS certificate volume mounts are present; no changes required |

---

## External References
- https://nginx.org/en/docs/http/ngx_http_ssl_module.html#ssl_protocols (Nginx 1.25 ssl_protocols directive)
- https://nginx.org/en/docs/http/ngx_http_ssl_module.html#ssl_ciphers (Nginx ssl_ciphers — cipher suite configuration)
- https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Strict-Transport-Security (HSTS header — max-age and preload)
- https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/upgrade-insecure-requests (upgrade-insecure-requests CSP directive)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Run `openssl s_client -connect localhost:443 -tls1_1`; verify the connection is refused with `SSL routines::no protocols available` and no TLS handshake completes (AC-002)
- [ ] Run `openssl s_client -connect localhost:443 -tls1_2`; verify the handshake succeeds and the server returns a valid certificate (AC-002 — TLS 1.2 still permitted)
- [ ] Run `curl -I http://localhost/api/health`; verify the response is `HTTP/1.1 301 Moved Permanently` with `Location: https://localhost/api/health` (AC-004)
- [ ] Run `curl -I https://localhost/api/health`; verify the response headers include `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload` (Edge: HSTS)
- [ ] Run `curl -I https://localhost/`; verify the response headers include `Content-Security-Policy: default-src 'self'; upgrade-insecure-requests;` (Edge: mixed-content SPA)
- [ ] Verify no API response body is returned for an HTTP request — the 301 response must have an empty or minimal body (AC-004 — no data over HTTP)

---

## Implementation Checklist
- [ ] `ssl_protocols TLSv1.2 TLSv1.3;` is the only `ssl_protocols` directive in `nginx.conf` — there must be no `TLSv1` or `TLSv1.1` token anywhere in the file (AC-002; OWASP A02)
- [ ] `ssl_prefer_server_ciphers on;` is present — prevents clients from negotiating a weaker cipher order than the server's preferred list (AC-002; cipher hardening)
- [ ] The HTTP `server` block on port 80 contains only the `return 301` directive; it has no `location /` block that could inadvertently serve content before the redirect fires (AC-004; OWASP A05)
- [ ] `add_header Strict-Transport-Security` uses the `always` parameter — HSTS must be sent on 4xx and 5xx responses, not only on 200 responses, to prevent HSTS bypass via error page requests (Edge: HSTS; OWASP A05)
- [ ] `add_header Content-Security-Policy` uses the `always` parameter — the `upgrade-insecure-requests` directive must be present even on error pages so the SPA cannot fall back to HTTP on error paths (Edge: mixed-content SPA)
