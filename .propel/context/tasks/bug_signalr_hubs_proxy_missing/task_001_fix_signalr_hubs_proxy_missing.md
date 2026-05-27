# Bug Fix Task - bug_signalr_hubs_proxy_missing

## Bug Report Reference

- Bug ID: `signalr_hubs_proxy_missing`
- Source: Browser network tab — `POST /hubs/queue/negotiate?negotiateVersion=1` returns non-200

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Real-time queue updates broken — SignalR connection fails on every page load; live push events (new arrivals, status changes) are never received
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: Development (Vite dev server + ASP.NET Core Kestrel)

### Steps to Reproduce

1. Start both servers: `dotnet run --launch-profile Api` (port 8080) and `npm run dev` (port 5173)
2. Log in and navigate to `/queue`
3. Open browser DevTools → Network tab
4. **Expected**: `POST /hubs/queue/negotiate?negotiateVersion=1` → `200 OK`; WebSocket connection established
5. **Actual**: `POST /hubs/queue/negotiate?negotiateVersion=1` → non-200 (request never reaches the backend)

### Root Cause Analysis

- **File**: `frontend/vite.config.ts`
- **Cause**: The Vite dev server proxy only defined a rule for `/api/*`:
  ```js
  '/api': {
    target: 'http://localhost:8080',
    rewrite: (path) => path.replace(/^\/api/, ''),
  }
  ```
  The SignalR client connects to the relative URL `/hubs/queue` (no `/api` prefix — correctly matching the backend `app.MapHub<QueueHub>("/hubs/queue")` registration). Because there was no proxy rule for `/hubs`, the Vite dev server attempted to serve the path itself, found no matching static asset, and returned a non-200 response. The SignalR negotiate POST and subsequent WebSocket upgrade never reached the backend.

- **Files Affected**:
  - `frontend/vite.config.ts` — missing `/hubs` proxy entry

### Impact Assessment

- **Affected Features**: Queue dashboard real-time updates (new arrival toast/highlight, status change in-place update)
- **User Impact**: Staff/Admin on the Queue page see stale data; live push events via SignalR are silently dropped; no automatic row additions or status updates without manual page refresh
- **Data Integrity Risk**: None — queue data is still loaded correctly on initial HTTP fetch; only real-time push is affected
- **Security Implications**: None

---

## Fix Overview

Add a `/hubs` proxy entry to `vite.config.ts` with `ws: true` to forward both the HTTP negotiate handshake and the WebSocket upgrade to the backend Kestrel server. No path rewriting is needed because the backend hub is registered at `/hubs/queue` (without any `/api` prefix).

**Change** (`frontend/vite.config.ts`):
```js
// Before
'/api': {
  target: 'http://localhost:8080',
  changeOrigin: true,
  rewrite: (path) => path.replace(/^\/api/, ''),
},

// After — add /hubs entry below /api
'/hubs': {
  target: 'http://localhost:8080',
  changeOrigin: true,
  ws: true,   // required for WebSocket upgrade
},
```

---

## Fix Dependencies

- None — Vite config change only; no backend changes required.

---

## Verification

After fix, restart the Vite dev server. Navigate to `/queue` and confirm:
- `POST /hubs/queue/negotiate?negotiateVersion=1` → `200 OK`
- A WebSocket connection to `ws://localhost:5173/hubs/queue` is established in the Network tab
- Real-time queue updates are delivered without page refresh
