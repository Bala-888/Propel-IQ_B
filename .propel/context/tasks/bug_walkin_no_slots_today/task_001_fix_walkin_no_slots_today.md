# Bug Fix Task - bug_walkin_no_slots_today

## Bug Report Reference

- Bug ID: `walkin_no_slots_today`
- Source: User-reported — Walk-in booking form shows no appointment slots and does not allow creating a new walk-in registration

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: Staff cannot create any walk-in bookings — the "Create walk-in" button is permanently disabled and the slot dropdown shows "No same-day slots available"
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: All environments. Reproducible whenever no slots are seeded for the current date.

### Steps to Reproduce

1. Log in as `staff@clinic.com` / `Staff@1234`
2. Click **+ New Walk-In** from the Queue page
3. Navigate to `/walkin/new`
4. **Expected**: Slot dropdown shows today's available appointment slots; "Create walk-in" button is enabled after selecting a patient, slot, and reason
5. **Actual**: Slot dropdown shows "No same-day slots available"; "Create walk-in" button is `disabled` and unclickable; an info banner reads "All same-day slots are full. Consider adding the patient to a wait list."

---

## Root Cause Analysis

### Two compounding issues

#### Issue 1 — No appointment slots seeded for today

- **Table**: `appointment_slots`
- **Cause**: The database had 158 available slots but all were for dates **2026-05-28 and later**. No slots existed for the current date `2026-05-27`. The `WalkinBookingPage` filters to same-day slots, finding none.

```sql
-- Before fix: 0 rows for today
SELECT COUNT(*) FROM appointment_slots
WHERE slot_start::date = CURRENT_DATE AND is_available = true;
-- Result: 0
```

#### Issue 2 — Client-side date filtering with a fixed page size

- **File**: `frontend/src/pages/WalkinBookingPage.tsx`
- **Cause**: The page called `getSlots({ page: 1, pageSize: 100 })` (no date filter) and then filtered the response client-side:

```typescript
const res = await getSlots(accessToken!, { page: 1, pageSize: 100 })
const today = todayIso()
setSlots(res.slots.filter(s => s.date === today))  // BUG: date filter after fetch
```

This has a secondary timing bomb: as slots accumulate over time, today's slots would eventually fall beyond position 100 in the `ORDER BY slot_start ASC` result set and become permanently invisible to the walk-in page — even after being seeded.

The API had no `date` query parameter, forcing all date filtering to happen in application code.

---

## Fix

### 1. Seed today's slots (data fix)

12 appointment slots inserted for `2026-05-27`:
- 8 AM – 11 AM (Dr. Smith + Dr. Jones, 30-min intervals)
- 1 PM – 3 PM (Dr. Smith + Dr. Jones, 30-min intervals)

### 2. Add `date` filter to `GET /slots` API

**`src/api/DTOs/GetSlotsQuery.cs`** — added `DateOnly? Date = null` parameter:
```csharp
public sealed record GetSlotsQuery(
    bool      Available = true,
    DateOnly? Date      = null,   // new
    int       Page      = 1,
    int       PageSize  = 10
);
```

**`src/api/Services/SlotsService.cs`** — apply UTC day boundary filter when `Date` is provided:
```csharp
if (query.Date.HasValue)
{
    var startUtc = query.Date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var endUtc   = startUtc.AddDays(1);
    baseQuery = baseQuery.Where(s => s.SlotStart >= startUtc && s.SlotStart < endUtc);
}
```

### 3. Use server-side date filter in WalkinBookingPage

**`frontend/src/api/slotsApi.ts`** — added optional `date` param:
```typescript
export async function getSlots(
  accessToken: string,
  { page, pageSize, date }: { page: number; pageSize: number; date?: string },
)
```

**`frontend/src/pages/WalkinBookingPage.tsx`** — pass today's date; remove client-side filter:
```typescript
// Before (broken)
const res = await getSlots(accessToken!, { page: 1, pageSize: 100 })
setSlots(res.slots.filter(s => s.date === today))

// After (fixed)
const res = await getSlots(accessToken!, { page: 1, pageSize: 100, date: todayIso() })
setSlots(res.slots)
```

---

## Impact Assessment

- **Affected Features**: Walk-in patient registration — complete loss of functionality for Staff role
- **User Impact**: Staff could not book any walk-in patients; the form appeared functional but the submit button was always disabled
- **Data Integrity**: No data corruption — no partial writes occurred

---

## Fix Summary

| File | Change |
|---|---|
| `appointment_slots` (DB) | Seeded 12 slots for 2026-05-27 |
| `src/api/DTOs/GetSlotsQuery.cs` | Added `DateOnly? Date` filter param |
| `src/api/Services/SlotsService.cs` | Apply UTC day boundary filter in EF query |
| `frontend/src/api/slotsApi.ts` | Added optional `date` param to `getSlots()` |
| `frontend/src/pages/WalkinBookingPage.tsx` | Pass `date=today` to server; removed client-side filter |
