# Bug Fix Task - bug_walkin_patient_search_500

## Bug Report Reference

- Bug ID: `bug_walkin_patient_search_500`
- Source: Staff-reported — typing in the patient search box on `/walkin/new` throws a 500 error

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: Walk-in patient search completely non-functional — staff cannot look up any existing patient during walk-in booking; entire walk-in flow is blocked
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: Development

### Steps to Reproduce

1. Log in as Staff (`staff@clinic.com` / `Staff@1234`) and navigate to `/queue`
2. Click **+ New Walk-In** → navigates to `/walkin/new`
3. Type 3 or more characters into the **Search patient** typeahead
4. **Expected**: Dropdown appears with matching patient names
5. **Actual**: API returns HTTP 500 — no results shown, typeahead silently clears

---

## Root Cause Analysis

### Bug — Duplicate route `GET /api/patients/search` causing `AmbiguousMatchException`

- **Layer**: Backend — ASP.NET Core routing
- **Cause**: Two separate controllers were both registered with the exact same route template `[Route("patients")][HttpGet("search")]`:

  | Controller | File | Added for |
  |---|---|---|
  | `PatientSearchController` | `src/api/Features/Patients/PatientSearchController.cs` | Walk-in typeahead (us_030/AC-002) |
  | `PatientsController.SearchAsync` | `src/api/Controllers/PatientsController.cs` | Patient search page (us_040/AC-001) |

  When ASP.NET Core's routing engine receives `GET /api/patients/search`, it finds two candidate action methods and cannot determine which to invoke. It throws `Microsoft.AspNetCore.Routing.Matching.AmbiguousMatchException`, which the framework converts to HTTP 500. This error fires on every keystroke in the walk-in typeahead after the 3-character debounce threshold.

- **Why it was missed**: The two controllers were added in separate user stories (us_030 and us_040). They also returned different DTO shapes — `PatientSearchController` returned `{ patientId, firstName, lastName, dateOfBirth }` while `PatientsController` returned `PatientSearchResultDto { id, fullName, dateOfBirth, patientCode }` — so the conflict was not detected during development.

---

## Impact Assessment

- **Affected Features**: Walk-in patient search typeahead at `/walkin/new`; `PatientSearchPage` at `/patients/search` (both routes hit the same broken endpoint)
- **User Impact**: Staff cannot search for existing patients during walk-in registration — forced to create a new patient record for every walk-in even when the patient already exists
- **Data Integrity Risk**: Medium — duplicate patient records would accumulate if staff worked around the error by always creating new patients
- **Security Implications**: None (route conflict; no auth bypass)

---

## Fix Applied

### Backend — `src/api/Features/Patients/PatientSearchController.cs`

**Deleted.** The controller was a duplicate of the search action already present in `PatientsController`. It had no external references (controllers are discovered by ASP.NET Core via reflection, not explicit imports).

### Backend — `src/api/Controllers/PatientsController.cs`

Updated the `Select` projection in `SearchAsync` to emit both sets of fields required by the two frontend consumers:

```csharp
.Select(p => new
{
    // PatientSearchPage (us_040/AC-001)
    id          = p.Id,
    fullName    = p.FirstName + " " + p.LastName,
    dateOfBirth = p.DateOfBirth,
    patientCode = $"P{p.Id:D6}",
    // Walk-in typeahead (us_030/AC-002)
    patientId   = p.Id,
    firstName   = p.FirstName,
    lastName    = p.LastName,
})
```

Added `OrderBy(LastName).ThenBy(FirstName)` for deterministic result ordering.

---

## Files Changed

| File | Change |
|---|---|
| `src/api/Features/Patients/PatientSearchController.cs` | **Deleted** — duplicate controller |
| `src/api/Controllers/PatientsController.cs` | Merged projection to serve both consumers |

---

## Verification

1. Restart the API (`dotnet run --launch-profile Api`)
2. Log in as Staff, navigate to `/walkin/new`
3. Type 3+ characters in the patient search box → dropdown shows matching patients
4. Log in as Staff, navigate to `/patients/search`
5. Search for a patient → results table populates correctly
