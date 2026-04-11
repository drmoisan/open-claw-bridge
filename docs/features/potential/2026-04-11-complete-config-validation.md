---
title: "complete-config-validation - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-43"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# complete-config-validation (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

`BridgeSettingsValidator.Validate` in `src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs` (line 121) validates six of the ten `BridgeSettings` fields: `Mode`, `InboxPollSeconds`, `CalendarPollSeconds`, `MaxItemsPerScan`, `BodyPreviewMaxChars`, and `PipeName`. Four fields are not validated at all (design audit deviation #20, Low severity):

- `InboxOverlapMinutes` — controls how far back each inbox scan overlaps with the previous one. An out-of-range value (e.g., zero or a very large number) would cause the inbox query to scan an incorrect window without any startup-time indication.
- `CalendarPastDays` — controls the lookback window for the calendar query. A zero or negative value would generate a nonsensical date filter; a value larger than the allowed maximum (not currently defined) could cause very large Outlook queries.
- `CalendarFutureDays` — same concern, forward-looking.
- `LogLevel` — consumed as a string to configure the logging subsystem. An unrecognized value (e.g., `"Verbose"` instead of `"Trace"`, or a misspelling) would silently fall back to a default or produce no structured logging output, making production debugging harder.

`BridgeApplication.ValidateSettings` at line 23 calls `Validate` and terminates on any returned error, so this is the correct enforcement point — it just needs the four additional checks added to the validator.

## Proposed Behavior

Add four new validation rules to `BridgeSettingsValidator.Validate`:

1. **`InboxOverlapMinutes`** — must be `>= 1`. Rationale: a value of zero disables overlap entirely, losing the safety margin against missed messages during poll boundaries. No defined upper bound is needed at this stage; a positive floor is the meaningful constraint.

2. **`CalendarPastDays`** — must be `>= 1`. Rationale: a zero or negative value would produce a date window with `start >= end`, which the Outlook DASL filter would reject or return no results silently.

3. **`CalendarFutureDays`** — must be `>= 1`. Same rationale as `CalendarPastDays`.

4. **`LogLevel`** — must be one of the Microsoft.Extensions.Logging level strings: `"Trace"`, `"Debug"`, `"Information"`, `"Warning"`, `"Error"`, `"Critical"`, `"None"` (case-insensitive). Rationale: the field is read as a string and used to configure the log level; any unrecognized value would silently misconfigure logging. The allowed set is a fixed, closed enumeration drawn from `Microsoft.Extensions.Logging.LogLevel`.

Error message style must match the existing messages in `Validate` (lowercase, descriptive, no punctuation inconsistency). For example: `"inboxOverlapMinutes must be >= 1"`, `"calendarPastDays must be >= 1"`, `"calendarFutureDays must be >= 1"`, `"logLevel must be one of: Trace|Debug|Information|Warning|Error|Critical|None"`.

No changes to `BridgeSettings` itself, no new fields, no changes to `BridgeSettings.Default` (its defaults — `5`, `14`, `60`, `"Information"` — all satisfy the new rules). No changes to call sites.

## Acceptance Criteria (early draft)

- [ ] `BridgeSettingsValidator.Validate` returns an error containing `"inboxOverlapMinutes"` when `InboxOverlapMinutes` is `0` or negative.
- [ ] `BridgeSettingsValidator.Validate` returns an error containing `"calendarPastDays"` when `CalendarPastDays` is `0` or negative.
- [ ] `BridgeSettingsValidator.Validate` returns an error containing `"calendarFutureDays"` when `CalendarFutureDays` is `0` or negative.
- [ ] `BridgeSettingsValidator.Validate` returns an error containing `"logLevel"` when `LogLevel` is an unrecognized string (e.g., `"Verbose"`, `"verbose"`, empty string, whitespace-only).
- [ ] `BridgeSettingsValidator.Validate` accepts all seven valid `LogLevel` strings case-insensitively: `"Trace"`, `"Debug"`, `"Information"`, `"Warning"`, `"Error"`, `"Critical"`, `"None"`.
- [ ] `BridgeSettings.Default` continues to pass `Validate` with no errors after the new rules are added.
- [ ] All existing `BridgeSettingsValidator` tests continue to pass without modification.

## Constraints & Risks

- **`LogLevel` allowed set is closed and fixed.** The seven values align with `Microsoft.Extensions.Logging.LogLevel` enum members. Do not use `Enum.Parse` against `LogLevel` directly in the validator, because the `Contracts` assembly should not take a dependency on `Microsoft.Extensions.Logging` — use a static `HashSet<string>` of the known string values instead.
- **Lower bounds only — no upper bounds required at this time.** The audit identifies missing bounds checks; the spec does not define an upper bound for `InboxOverlapMinutes`, `CalendarPastDays`, or `CalendarFutureDays`. Adding only a lower-bound (`>= 1`) check is correct scope for this feature.
- **Error message format must be consistent.** The existing messages are lowercase with no trailing punctuation and no extraneous detail. New messages must follow the same style so that callers that pattern-match on the error strings continue to work.
- **`BridgeSettings.Default` passes.** The defaults (`InboxOverlapMinutes = 5`, `CalendarPastDays = 14`, `CalendarFutureDays = 60`, `LogLevel = "Information"`) all satisfy the new rules. The existing `Bridge_settings_default_should_satisfy_validator` test will catch any regression.
- **No config file parsing changes.** Validation runs after deserialization. This feature adds only validator logic; it does not alter how settings are read from disk.

## Test Conditions to Consider

- [ ] **Boundary — `InboxOverlapMinutes = 0`:** `Validate` returns an error message containing `"inboxOverlapMinutes"`.
- [ ] **Boundary — `InboxOverlapMinutes = 1`:** `Validate` does not return an error for this field.
- [ ] **Boundary — `CalendarPastDays = 0`:** `Validate` returns an error message containing `"calendarPastDays"`.
- [ ] **Boundary — `CalendarPastDays = 1`:** `Validate` does not return an error for this field.
- [ ] **Boundary — `CalendarFutureDays = 0`:** `Validate` returns an error message containing `"calendarFutureDays"`.
- [ ] **Boundary — `CalendarFutureDays = 1`:** `Validate` does not return an error for this field.
- [ ] **Negative values:** `InboxOverlapMinutes = -1`, `CalendarPastDays = -1`, `CalendarFutureDays = -1` each produce their respective error.
- [ ] **`LogLevel` — unrecognized string `"Verbose"`:** Error containing `"logLevel"` returned.
- [ ] **`LogLevel` — empty string:** Error containing `"logLevel"` returned.
- [ ] **`LogLevel` — each of the seven valid values:** No error returned for any of them, both in the declared casing and in all-lowercase.
- [ ] **Default settings pass:** `BridgeSettingsValidator.Validate(BridgeSettings.Default)` returns an empty collection (regression guard for `Bridge_settings_default_should_satisfy_validator`).
- [ ] **Multiple violations accumulate:** A settings object with both `CalendarPastDays = 0` and `LogLevel = "bad"` returns two errors, one for each field (confirming the validator collects all errors rather than short-circuiting).

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/complete-config-validation/` folder from the template

