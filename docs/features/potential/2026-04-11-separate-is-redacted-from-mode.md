---
title: "separate-is-redacted-from-mode - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-37"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# separate-is-redacted-from-mode (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

`ResponseShaper.ShapeMessage` and `ResponseShaper.ShapeEvent` currently set `IsRedacted = true` whenever the bridge is operating in safe mode, regardless of whether the underlying item has a sensitive (`Sensitivity` value 2 = Private or 3 = Confidential) designation. Safe mode is the operational default; it controls *which fields are included in the response* as a privacy posture, not whether the item's content has been redacted due to its sensitivity classification.

This conflation (design audit deviation #17, High severity) means:

- Every safe-mode response carries `is_redacted = true`, which OpenClaw cannot use to distinguish "this item was private/confidential and its content was replaced" from "this is a normal item returned in safe mode with some fields suppressed."
- When sensitivity-based redaction is implemented (deviation #6), there is no clean signal for the caller to determine which kind of suppression was applied.
- The existing `ResponseShaperTests` tests assert `IsRedacted = true` for safe-mode items and `IsRedacted = false` for enhanced-mode items, cementing this incorrect coupling in the test suite.

The correct invariant: `is_redacted` signals only that an item's content was replaced due to its sensitivity classification. Mode-based field suppression is a separate, orthogonal concern that should not alter this flag.

## Proposed Behavior

`IsRedacted` must reflect only sensitivity-based redaction. Concretely:

- `IsRedacted = true` if and only if `Sensitivity` equals 2 (Private) or 3 (Confidential) and the sensitivity-based redaction logic has replaced the item's fields (subject replaced, sender/body fields nulled, per the spec). This logic lives in `NormalizeMessage`/`NormalizeEvent` (the sensitivity-redaction feature) and in any shaping pass.
- `IsRedacted = false` for all normal items in both safe and enhanced mode, regardless of which fields are omitted due to mode.
- `ResponseShaper.ShapeMessage` safe-mode path must null `BodyPreview`, `SenderName`, `SenderEmail`, `ToJson`, `CcJson`, and set `ProtectedFieldsAvailable = false` — but must **not** set `IsRedacted = true` on non-sensitive items.
- `ResponseShaper.ShapeEvent` safe-mode path must null `BodyPreview`, `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, and set `ProtectedFieldsAvailable = false` — but must **not** set `IsRedacted = true` on non-sensitive items.
- `ResponseShaper.ShapeMessage` and `ResponseShaper.ShapeEvent` enhanced-mode paths must preserve the `IsRedacted` value carried on the incoming DTO rather than forcing it to `false`. A sensitive item remains `IsRedacted = true` in enhanced mode.

This feature changes the contract of `ResponseShaper` specifically. The logic that sets `IsRedacted = true` for sensitive items is owned by the sensitivity-based redaction feature (deviation #6 / remediation #1); that feature is a prerequisite or co-implementation. This feature draws the boundary: `ResponseShaper` must not touch `IsRedacted` except to guarantee it is preserved from the incoming DTO; the setter for `is_redacted` belongs in the sensitivity-check layer.

## Acceptance Criteria (early draft)

- [ ] A non-sensitive mail item (Sensitivity != 2 and != 3) shaped in safe mode returns `is_redacted = false`.
- [ ] A non-sensitive calendar event shaped in safe mode returns `is_redacted = false`.
- [ ] A non-sensitive item shaped in enhanced mode returns `is_redacted = false`.
- [ ] A sensitive item (Sensitivity 2 or 3) shaped in safe mode returns `is_redacted = true`.
- [ ] A sensitive item shaped in enhanced mode returns `is_redacted = true` (sensitivity overrides mode).
- [ ] In safe mode, `body_preview`, `sender_name`, `sender_email`, `to_json`, `cc_json` are null and `protected_fields_available` is false for a non-sensitive item, confirming that field suppression still operates correctly without requiring `is_redacted = true`.
- [ ] In safe mode for events, `body_preview`, `organizer`, `required_attendees_json`, `optional_attendees_json`, `resources_json` are null and `protected_fields_available` is false for a non-sensitive event.
- [ ] `ResponseShaper.ShapeMessage` does not set `IsRedacted` to any fixed value; it preserves the value from the incoming `MessageDto` after the mode-based field suppression is applied.
- [ ] `ResponseShaper.ShapeEvent` follows the same preservation contract for `IsRedacted`.
- [ ] All existing `ResponseShaperTests` are updated to reflect the corrected semantics; no test asserts `IsRedacted = true` solely because the mode is safe.

## Constraints & Risks

- **Dependency on sensitivity-based redaction.** This feature corrects the `is_redacted` contract in `ResponseShaper`. For `is_redacted = true` to be observable in any realistic scenario, the sensitivity-based redaction feature (deviation #6) must also be implemented: it sets `IsRedacted = true` on the DTO during normalization for items with Sensitivity 2 or 3. These two features should be planned as a coordinated pair. Working this feature alone (without deviation #6) corrects the contract but leaves `is_redacted` always `false` until normalization-time sensitivity detection is added.
- **Breaking existing tests.** Three `ResponseShaperTests` tests currently assert `IsRedacted = true` for safe-mode items and `IsRedacted = false` for enhanced-mode items. All three must be updated. The existing assertions are the spec artifact for the incorrect behavior and must be replaced, not preserved.
- **`NormalizeMessage` currently sets `IsRedacted = false` unconditionally.** The cached value in SQLite will always be `false` until sensitivity-based redaction populates it. This feature does not change normalization; it only changes `ResponseShaper` to stop overwriting `IsRedacted`. The net effect before deviation #6 is implemented: `IsRedacted` remains `false` on all shaped responses, which is correct for non-sensitive items.
- **No schema change required.** The `is_redacted` column in both the `messages` and `events` SQLite tables already stores the value. The `MessageDto` and `EventDto` records already carry `IsRedacted`. No contract, schema, or serialization change is needed beyond the shaping logic.
- **OpenClaw client behavior.** Downstream consumers of the RPC response (OpenClaw via the client CLI) may currently rely on `is_redacted = true` to detect safe-mode responses. If so, this is a behavioral regression from their perspective and must be called out in the PR description. The correct signal for "safe mode active" is the `mode` field on the `status` RPC response, not `is_redacted` on individual items.

## Test Conditions to Consider

- [ ] **Unit — safe mode, non-sensitive message:** `ShapeMessage` given a `MessageDto` with `IsRedacted = false` (Sensitivity = 0 or 1) and safe-mode settings returns a shaped DTO with `IsRedacted = false` and all suppressed fields null.
- [ ] **Unit — safe mode, non-sensitive event:** Same pattern for `ShapeEvent`.
- [ ] **Unit — enhanced mode, non-sensitive message:** `ShapeMessage` with `IsRedacted = false` and enhanced-mode settings returns `IsRedacted = false` with fields preserved.
- [ ] **Unit — safe mode, sensitive message (pre-redacted):** `ShapeMessage` given a `MessageDto` with `IsRedacted = true` (as set by sensitivity-based redaction) and safe-mode settings returns a shaped DTO with `IsRedacted = true` still set.
- [ ] **Unit — enhanced mode, sensitive message (pre-redacted):** Same as above with enhanced-mode settings; `IsRedacted` must remain `true`.
- [ ] **Unit — ShapeEvent preserves IsRedacted:** Same pair of tests for `ShapeEvent` with `IsRedacted = true` input.
- [ ] **Unit — safe-mode field suppression is independent of IsRedacted:** Verify that `BodyPreview`, `SenderName`, `SenderEmail` are null after safe-mode shaping regardless of whether `IsRedacted` was true or false on input.
- [ ] **Regression — existing ResponseShaperTests updated:** All assertions that were `IsRedacted.Should().BeTrue()` solely because `Mode = "safe"` must be updated to `IsRedacted.Should().BeFalse()` for non-sensitive inputs.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/separate-is-redacted-from-mode/` folder from the template

