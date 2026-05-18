# User Story - US_016-II

## Story ID
* ID Format: US_016-II

## Story Title
* AI conversational intake — Part II: structured summary review, field correction, and save

## Description
* As a **patient**, I want to review the structured summary of my AI-collected intake, correct any misunderstood fields in-place, and confirm the final record, so that I can be confident the data my care team receives is accurate.

## Acceptance Criteria

### AC-001: AI summary screen displays all collected fields for review
- **Given**: All 5 field groups have been collected in the AI intake session
- **When**: `GET /intake/ai/summary?sessionId=<id>` is called
- **Then**: The response includes a structured JSON summary with all collected demographics, medical history, medications, allergies, and chief complaint, displayed in a readable review panel on SCR-004

### AC-002: Patient edits a single intake field inline without restarting the session
- **Given**: The summary review screen is showing and the patient identifies an incorrect medication entry
- **When**: `PATCH /intake/ai/field` is called with `{"fieldPath": "medications[0].name", "value": "Metformin 500mg"}`
- **Then**: The API updates the session state and returns the corrected summary with the new value immediately visible without a page reload

### AC-003: Confirming the summary saves an encrypted IntakeRecord with status Complete
- **Given**: The patient has reviewed and is satisfied with the summary
- **When**: `POST /intake/ai/confirm` is called with the `sessionId`
- **Then**: The API returns HTTP 201, an `IntakeRecord` with `status = "Complete"` and `mode = "AI"` is persisted in the database with the intake data encrypted via pgcrypto JSONB, and the audit log records `ActionType: IntakeCompleted`

### AC-004: Confirmed intake redirects patient to the home dashboard
- **Given**: `POST /intake/ai/confirm` returns HTTP 201
- **When**: The React SPA processes the response
- **Then**: The patient is navigated to `/intake` confirmation screen or home dashboard (SCR-003) and a success toast "Your intake has been saved" is displayed

## Edge Cases
* **Session expired before confirmation**: If the AI intake `sessionId` has expired (e.g., patient took >30 minutes), `POST /intake/ai/confirm` must return HTTP 410 with `{"error": "Session expired. Your draft has been saved."}` and the patient must be offered the option to resume from the draft.
* **Partial correction leaves required field empty**: If a `PATCH /intake/ai/field` call sets a required field value to an empty string, the API must return HTTP 400 with `{"error": "Chief complaint cannot be empty"}` and not save the empty value.

## Traceability
### Parent Epic
* Epic: EP-003

### Requirement Tags
* FR-007, FR-010, UC-007

### Dependencies
* us_016-I — Decomposed — This is Part II of the AI conversational intake story; requires the multi-turn dialogue engine and session state from us_016-I

## Visual Design Context (UI Stories Only)
### Screen Specifications
- **Screen ID(s)**: SCR-004
- **Figma Spec Reference**: SCR-004 (AI Conversational Intake — summary review panel)

### Wireframe References
| Field | Value |
|-------|-------|
| **Status** | AVAILABLE |
| **Type** | HTML |
| **Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-004-ai-conversational-intake.html |

### UX Requirements
- **UXR Mappings**: UXR-101 (confidence indicators on collected fields), UXR-105 (no color-only validation states)
