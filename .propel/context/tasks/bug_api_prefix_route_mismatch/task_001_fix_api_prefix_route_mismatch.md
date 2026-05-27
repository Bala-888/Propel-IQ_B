# Bug Fix Task - bug_api_prefix_route_mismatch

## Bug Report Reference

- Bug ID: `api_prefix_route_mismatch`
- Source: Direct user-reported error — API returning 404 from browser / Vite dev proxy

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: All API endpoints with the redundant `api/` route prefix return 404 — affected features are completely non-functional
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: Development (Vite dev server + ASP.NET Core Kestrel). Reproducible on every request to the affected controllers when routing through the Vite proxy.

### Affected Controllers (11 total)

| Controller | Broken Route | Fixed Route |
|---|---|---|
| `AdminMetricsController` | `api/admin/metrics` | `admin/metrics` |
| `QueueController` | `api/queue` | `queue` |
| `DocumentsController` | `api/documents` | `documents` |
| `DocumentUploadController` | `api/documents` | `documents` |
| `CalendarSyncController` | `api/calendar` | `calendar` |
| `PreferredSlotController` | `api/bookings` | `bookings` |
| `WalkinBookingController` | `api/bookings` | `bookings` |
| `PatientPreferencesController` | `api/patients/{id}/preferences` | `patients/{id}/preferences` |
| `WalkinPatientController` | `api/patients` | `patients` |
| `PatientSearchController` | `api/patients` | `patients` |
| `InsuranceController` | `api/insurance` | `insurance` |

### Steps to Reproduce

1. Start both servers: `dotnet run --launch-profile Api` (port 8080) and `npm run dev` (port 5173)
2. Log in as Admin (`admin@clinic.com` / `Admin@1234`)
3. Navigate to `/admin/kpi` (KPI Dashboard) or `/queue`
4. **Expected**: Page loads with data from the API
5. **Actual**: Browser network tab shows `404 Not Found` for `GET /api/admin/metrics?date=today` and `GET /api/queue?date=...`

**Error Output**:

```
STATUS: 404
```

### Root Cause Analysis

- **Layer**: ASP.NET Core routing
- **Cause**: The Vite dev server proxy (`vite.config.ts`) is configured to strip the `/api` prefix before forwarding requests to the backend Kestrel server:
  ```js
  '/api': {
    target: 'http://localhost:8080',
    changeOrigin: true,
    rewrite: (path) => path.replace(/^\/api/, ''),
  }
  ```
  This means a frontend call to `/api/queue` is forwarded to the backend as `/queue`. However, the affected controllers were decorated with `[Route("api/queue")]` (with the `api/` prefix included), so the backend registered them at `/api/queue` instead of `/queue`. The incoming `/queue` path had no matching route → 404.

  The correctly-working controllers (e.g., `AuthController` at `[Route("auth")]`, `AdminUsersController` at `[Route("admin/users")]`) do not include the `api/` prefix, which is why login and user management worked while queue, metrics, documents, and other endpoints did not.

- **Files Affected**:
  - `src/api/Features/Admin/AdminMetricsController.cs`
  - `src/api/Features/Queue/QueueController.cs`
  - `src/api/Controllers/DocumentsController.cs`
  - `src/api/Features/Documents/DocumentUploadController.cs`
  - `src/api/Controllers/CalendarSyncController.cs`
  - `src/api/Controllers/PreferredSlotController.cs`
  - `src/api/Features/Bookings/WalkinBookingController.cs`
  - `src/api/Controllers/PatientPreferencesController.cs`
  - `src/api/Features/Patients/WalkinPatientController.cs`
  - `src/api/Features/Patients/PatientSearchController.cs`
  - `src/api/Controllers/InsuranceController.cs`

### Impact Assessment

- **Affected Features**: KPI Dashboard, Queue management, Document upload/view, Calendar sync, Preferred slots, Walk-in booking, Patient preferences, Patient search, Insurance
- **User Impact**: Admin and Staff roles could not access the Queue or KPI Dashboard; Walk-in booking, document handling, and patient search were all broken
- **Data Integrity Risk**: None — no data corruption; requests simply failed before reaching service layer
- **Security Implications**: None introduced by this bug

---

## Fix Overview

Remove the `api/` prefix from the `[Route(...)]` attribute on all 11 affected controllers so their backend registration path matches what the Vite proxy forwards. No frontend changes required — frontend API clients already call `/api/<resource>` which correctly maps to `/<resource>` on the backend after proxy rewrite.

**Example change** (`QueueController.cs`):
```csharp
// Before (broken)
[Route("api/queue")]

// After (fixed)
[Route("queue")]
```

---

## Fix Dependencies

- None — purely a routing attribute change, no DB migrations or schema changes required.

---

## Verification

After fix, confirmed via direct PowerShell test through the Vite proxy:

```
GET http://localhost:5173/api/queue?date=2026-05-27  →  200 []
GET http://localhost:5173/api/admin/metrics?date=today  →  200 { totalBookings: 0, ... }
```
