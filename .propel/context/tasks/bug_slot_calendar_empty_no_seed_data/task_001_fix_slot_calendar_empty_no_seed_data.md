# Bug Fix Task - bug_slot_calendar_empty_no_seed_data

## Bug Report Reference

- Bug ID: `slot_calendar_empty_no_seed_data`
- Source: Direct user-reported — `/slots` page shows no appointment slots after clicking "Book Appointment" from the patient portal

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Appointment booking workflow completely non-functional — patients see a blank calendar with no slots to select
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: Development — fresh database with no seed data

### Steps to Reproduce

1. Log in as a Patient and navigate to `/intake` (Patient Portal)
2. Click **Book Appointment** → navigates to `/slots`
3. **Expected**: A paginated calendar showing available appointment slots grouped by date, with provider names and time ranges
4. **Actual**: Calendar renders but displays no slots — empty state shown. API returns `200 {"slots":[],"pagination":{"total":0,...}}`

---

## Root Cause Analysis

### Bug 1 — Missing seed data in `appointment_slots` table

- **Layer**: Database
- **Cause**: The `appointment_slots` table was created (as part of an earlier manual schema bootstrap) but never populated. The `SlotsService` queries `appointment_slots WHERE is_available = true`, which returns an empty result set when the table has no rows. The frontend `SlotCalendar` component correctly renders an empty state when the API returns `slots: []` — the UI behaviour is correct; the data is absent.

  There is no EF Core migration seed or data seeder targeting `appointment_slots`, so on a fresh database the table is always empty.

### Bug 2 — Incorrect timestamp timezone on initial seed attempt

- **Layer**: Database — data quality
- **Cause**: On first seed attempt, slots were inserted using `TIMESTAMP '...'` (without time zone) into a `TIMESTAMPTZ` column. PostgreSQL's session timezone was IST (UTC+5:30), so `09:00 local` was stored as `03:30 UTC`. The `SlotsService` projects `TimeOnly.FromDateTime(s.SlotStart)` directly from the stored UTC value, causing the API to return `"startTime": "03:30:00"` instead of `"09:00:00"`. The frontend `SlotCell` displayed `03:30` to the user.

  **Fix**: Re-seeded using `SET timezone = 'UTC'` and explicit `TIMESTAMPTZ '... UTC'` literals so business hours (09:00–16:30) are stored and returned correctly.

---

## Impact Assessment

- **Affected Features**: Appointment slot calendar, slot selection, booking confirmation flow
- **User Impact**: Patients could not book any appointment — the entire booking workflow was blocked at the first step
- **Data Integrity Risk**: None
- **Security Implications**: None

---

## Fix Applied

Seeded 160 available appointment slots directly into the `appointment_slots` table:

```sql
SET timezone = 'UTC';
INSERT INTO appointment_slots (slot_start, slot_end, is_available, provider_name)
SELECT
  gs AS slot_start,
  gs + INTERVAL '30 minutes' AS slot_end,
  true AS is_available,
  (ARRAY['Dr. Sarah Chen', 'Dr. James Patel', 'Dr. Maria Lopez'])
    [((EXTRACT(DOW FROM gs)::int % 3) + 1)]
FROM generate_series(
  TIMESTAMPTZ '2026-05-28 09:00:00 UTC',
  TIMESTAMPTZ '2026-06-10 16:30:00 UTC',
  INTERVAL '30 minutes'
) AS gs
WHERE EXTRACT(DOW FROM gs) BETWEEN 1 AND 5
  AND EXTRACT(HOUR FROM gs) BETWEEN 9 AND 16;
```

**Seed coverage**: 160 slots, Mon–Fri, 09:00–16:30 UTC, 30-minute intervals, 28 May – 10 Jun 2026, across 3 providers (Dr. Sarah Chen, Dr. James Patel, Dr. Maria Lopez).

---

## Long-Term Recommendation

Add an EF Core `HasData` seed or a dedicated `SlotSeeder` (similar to `InsuranceRecordSeeder` at `src/seed/`) that generates a rolling window of available slots on first run. This ensures a fresh database is always functional without manual SQL intervention.

---

## Verification

```
GET http://localhost:8080/slots?available=true&page=1&pageSize=3
→  200 {"slots":[{"id":310,"date":"2026-05-28","startTime":"09:00:00",...}],"pagination":{"total":160,...}}
```

Patient portal `/slots` now renders the full two-week calendar with selectable slots.
