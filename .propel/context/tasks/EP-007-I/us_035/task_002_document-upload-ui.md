# Task - TASK_002

## Requirement Reference
- **User Story:** us_035
- **Story Location:** .propel/context/tasks/EP-007-I/us_035/us_035.md
- **Acceptance Criteria:**
  - AC-001: Upload form rejects unsupported file types at the server; the frontend surfaces the 400 error message with icon + text
  - AC-002: Files > 25 MB surface the 413 error message with icon + text; client-side size check provides a UX hint before submission
  - AC-004: On successful 201 response, the form displays the `documentId` and a "Document uploaded successfully." confirmation with icon + text
  - AC-005: `<input type="file" accept=".pdf,.docx" capture="environment">` enables direct camera capture as fallback on mobile devices (UXR-303)
- **Edge Cases:**
  - Client-side size hint: `file.size > 25 * 1024 * 1024` check before submit shows an inline error immediately — server 413 is the authoritative rejection and is also handled if the client check is bypassed

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-009 (Document Upload) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-009-document-upload.html |
| **Screen Spec** | SCR-009 — file input, submit button, upload status area, error messages |
| **UXR Requirements** | UXR-303 (mobile file input with camera capture fallback), UXR-105 (upload status and errors use icon + text, not colour alone) |
| **Design Tokens** | Upload status success colour, error colour, progress indicator (icon+text required per UXR-105) |

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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `DocumentUploadPage` (SCR-009); controlled file input; fetch FormData upload; status state (AC-001, AC-002, AC-004, AC-005) |
| HTTP Client | Fetch API (browser built-in) | Web platform | `POST /api/documents/upload` with `FormData`; reads response body for error messages (AC-001, AC-002, AC-004) |
| Routing | React Router | v6 | `/documents/upload` Patient-only route guard (AC-004; OWASP A01) |

---

## Task Overview

Build `DocumentUploadPage` (SCR-009) at `/documents/upload` with a Patient role guard. The file `<input>` uses `accept=".pdf,.docx"` and `capture="environment"` for mobile camera fallback. Before submitting, the handler checks `file.size` against 25 MB and shows an inline error without a server round-trip. On submit, `FormData` is posted to `POST /api/documents/upload`. The component handles 400 (unsupported type), 413 (size exceeded), and 201 (success), displaying each state with an icon and text label per UXR-105.

---

## Dependent Tasks
- task_001 (us_035) — `POST /api/documents/upload` endpoint must be available
- task_001 (us_009) — JWT context and role guard utilities for Patient-only route

---

## Impacted Components
- `src/web/src/pages/DocumentUploadPage.tsx` — new: SCR-009 document upload form with status handling
- `src/web/src/api/documentApi.ts` — new: `uploadDocument(file: File): Promise<UploadResult>` fetch wrapper

---

## Implementation Plan
1. `documentApi.ts`: `uploadDocument(file: File): Promise<UploadResult>` builds a `FormData` with `formData.append("file", file)`, posts to `POST /api/documents/upload`; on 201 returns `{ documentId: string; status: "Uploaded" }`; on 400 throws `ApiError { status: 400, message: string }` with the server error message; on 413 throws `ApiError { status: 413, message: string }` (AC-001, AC-002, AC-004)
2. `DocumentUploadPage` (SCR-009): Patient-only route guard; renders a `<form>` with a single `<label>` + `<input type="file" id="docFile" accept=".pdf,.docx" capture="environment">` — the `accept` attribute constrains the file picker; `capture="environment"` triggers the camera app as a fallback on mobile (AC-005; UXR-303)
3. Client-side size hint in `handleFileChange`: when the user selects a file, check `file.size > 25 * 1024 * 1024`; if true, set `clientError = "File exceeds 25 MB limit."` and prevent form submission until replaced — this is a convenience check only; the server 413 is also handled (AC-002; Edge: client hint; UXR-105)
4. Upload submit handler: set `status = "uploading"` and clear previous errors; call `uploadDocument(selectedFile)`; on success set `status = "success"` with returned `documentId`; on `ApiError` set `status = "error"` and `errorMessage = err.message` (AC-001, AC-002, AC-004)
5. Status display: `"uploading"` → spinner icon + `"Uploading document…"` text; `"success"` → checkmark icon + `"Document uploaded successfully. ID: {documentId}"`; `"error"` → error icon + `errorMessage` text; all states use `<p role="alert">` for errors and `<p role="status">` for success/progress — never colour-only indicators (UXR-105; WCAG 2.1)
6. After successful upload, clear the file input (`inputRef.current.value = ""`) and reset `selectedFile` to null so the user can upload a subsequent document without refreshing the page (AC-004 — allows repeat uploads in the same session)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── (documentApi.ts                  — CREATE)
        └── pages/
            └── (DocumentUploadPage.tsx           — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/documentApi.ts | uploadDocument FormData fetch wrapper |
| CREATE | src/web/src/pages/DocumentUploadPage.tsx | SCR-009 file input, upload handler, 400/413/201 status display |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-009-document-upload.html (SCR-009 HTML wireframe — file input layout, status area position, error message styling treatment)
- https://developer.mozilla.org/en-US/docs/Web/HTML/Attributes/capture (MDN — `capture="environment"` attribute on `<input type="file">` for device camera fallback on mobile; AC-005; UXR-303)
- https://developer.mozilla.org/en-US/docs/Web/API/FormData/Using_FormData_Objects (MDN FormData — `formData.append("file", file)` multipart/form-data upload; AC-004)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Select a valid PDF under 25 MB; submit; verify loading state shows spinner + "Uploading document…"; verify success state shows checkmark icon + "Document uploaded successfully." and the returned `documentId` (AC-004; UXR-105)
- [ ] Select a PNG file; submit; verify 400 error message renders with error icon + "Unsupported file type. Only PDF and DOCX are accepted." — no red border alone (AC-001; UXR-105)
- [ ] Select a file > 25 MB; verify the client-side error appears before submission; also stub the server to return 413 and verify the 413 error message renders with error icon (AC-002; Edge: client hint; UXR-105)
- [ ] Verify `<input>` element has `accept=".pdf,.docx"` and `capture="environment"` attributes in the rendered DOM (AC-005; UXR-303)
- [ ] After a successful upload, verify the file input is cleared and a second upload can be submitted without refreshing the page (AC-004 — repeat uploads)
- [ ] Navigate to `/documents/upload` as Staff role; verify redirect to unauthorised page (OWASP A01)
- [ ] Verify all error messages use `<p role="alert">` and success messages use `<p role="status">` (WCAG 2.1 accessibility)

---

## Implementation Checklist
- [ ] `<input type="file" accept=".pdf,.docx" capture="environment">` is paired with an associated `<label>` — the input is never unlabelled; `capture="environment"` is present to enable the camera fallback on mobile without a separate code path (AC-005; UXR-303; WCAG 2.1 SC 1.3.1)
- [ ] Client-side size check sets a validation error and returns early from `handleSubmit` — it does not make a network call for an oversized file; server 413 is still handled as a catch path if the client check is bypassed (AC-002; Edge: client hint)
- [ ] The upload `status` state machine uses the values `"idle" | "uploading" | "success" | "error"`; the spinner, checkmark, and error icon are each distinct SVG icons — status is never conveyed by colour change alone (UXR-105; WCAG 2.1 SC 1.4.1)
- [ ] Error messages from 400 and 413 responses are rendered from the server's `error` field in the JSON response body — the frontend does not hard-code the error strings, allowing the API message to be authoritative (AC-001, AC-002)
- [ ] File input is cleared via `ref.current.value = ""` after a successful upload; `selectedFile` state is reset to `null`; the submit button is disabled when `selectedFile === null` to prevent empty submissions (AC-004; OWASP A03 — no empty form submission)
- [ ] `documentApi.ts` does not attach any additional headers for authentication beyond what the global fetch interceptor provides (Bearer token from in-memory auth context) — no token duplication or token leakage via custom headers (OWASP A01; A02)
