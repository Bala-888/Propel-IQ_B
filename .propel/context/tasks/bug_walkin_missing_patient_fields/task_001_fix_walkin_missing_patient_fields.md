# Bug Fix Task - bug_walkin_missing_patient_fields

## Bug Report Reference

- Bug ID: `bug_walkin_missing_patient_fields`
- Source: Staff-reported — `/walkin/new` page is missing Phone and Insurance fields present in the original `/walkin` form

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Staff cannot capture essential patient contact (phone) and billing (insurance) information during the walk-in booking flow — critical for patient follow-up and billing
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: Development

### Steps to Reproduce

1. Log in as Staff (`staff@clinic.com` / `Staff@1234`) and navigate to `/queue`
2. Click **+ New Walk-In** → navigates to `/walkin/new`
3. **Expected**: Form contains Phone number and Insurance fields (present in original `/walkin` form)
4. **Actual**: Form only shows: patient typeahead, appointment slot dropdown, reason for visit, and priority — Phone and Insurance fields are absent

---

## Root Cause Analysis

### Bug — Missing Phone and Insurance fields in WalkinBookingPage

- **Layer**: Frontend UI + Backend DTO
- **Cause**: `WalkinBookingPage` (`/walkin/new`) was built as a lean booking flow implementing `PatientId → SlotId → ReasonForVisit → Priority` but omitted the patient contact detail fields (`Phone`, `Insurance`) that existed in the original `WalkInBookingForm` at `/walkin`. The `Patient` entity already has `Phone`, `InsuranceProvider`, and `InsuranceId` columns in the DB schema, but the new walk-in booking page provides no UI mechanism to capture or display them. The backend `WalkinBookingRequest` DTO likewise had no `Phone` or `InsuranceProvider` fields, so even if the frontend sent them they would be ignored.

---

## Impact Assessment

- **Affected Features**: Walk-in booking form at `/walkin/new`
- **User Impact**: Front-desk staff cannot record patient phone numbers or insurance plans during walk-in registration — this information is required for appointment reminders and billing
- **Data Integrity Risk**: Low (patient record exists; phone/insurance columns remain null rather than being populated)
- **Security Implications**: None

---

## Fix Applied

### Backend — `src/api/DTOs/WalkinBookingRequest.cs`

Added two optional fields to the walk-in booking request DTO:
- `Phone` (max 30 chars) — patient phone number captured at the desk
- `InsuranceProvider` (max 100 chars) — insurance plan name selected by staff

### Backend — `src/api/Features/Bookings/WalkinBookingService.cs`

After the serializable transaction commits, the service now applies a supplemental patient record update when `Phone` or `InsuranceProvider` is present in the request. This update runs outside the slot-locking transaction so it does not extend the lock window, and is best-effort (failure to update patient contact info does not roll back the committed booking).

### Frontend — `frontend/src/api/walkInApi.ts`

Added `phone?: string` and `insuranceProvider?: string` to the `WalkinBookingRequest` TypeScript interface to match the expanded backend DTO.

### Frontend — `frontend/src/pages/WalkinBookingPage.tsx`

Added a **"Patient contact"** section between the Patient and Appointment Slot sections:
- **Phone number** — `<input type="tel">`, optional, passed to `createWalkinBooking`
- **Insurance provider** — `<select>` dropdown with standard payer options, optional, passed to `createWalkinBooking`

Both fields are sent to the backend on submission and written to the `patients` table via the supplemental update in `WalkinBookingService`.

---

## Files Changed

| File | Change |
|---|---|
| `src/api/DTOs/WalkinBookingRequest.cs` | Added `Phone?` and `InsuranceProvider?` fields |
| `src/api/Features/Bookings/WalkinBookingService.cs` | Patch patient record with phone/insurance after booking commit |
| `frontend/src/api/walkInApi.ts` | Extended `WalkinBookingRequest` type |
| `frontend/src/pages/WalkinBookingPage.tsx` | Added Patient contact section (phone + insurance) |

---

## Verification

1. Navigate to `/walkin/new` as Staff
2. Confirm "Patient contact" section is visible with Phone and Insurance fields
3. Search for a patient, select a slot, enter reason, fill phone + insurance, click **Create walk-in**
4. In PostgreSQL, verify `SELECT phone, insurance_provider FROM patients WHERE id = <patient_id>` shows the entered values
