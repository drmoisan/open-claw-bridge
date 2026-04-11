---
title: "populate-to-json-cc-json-from-outlook - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-34"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# populate-to-json-cc-json-from-outlook (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

`NormalizeMessage` in `OutlookScanner.cs` constructs a `MessageDto` with `null` for both `ToJson` and `CcJson` (lines 399–400 per the design audit). The `MessageDto` record, SQLite schema, cache upsert, and read-back paths all support these fields, so the data pipeline is already wired end-to-end — only the COM read step at normalization time is missing. As a result, every message stored in the cache and returned to OpenClaw via the RPC pipe carries empty recipient lists regardless of the actual message content. The design audit classifies this as a High-severity deviation (deviation #12).

## Proposed Behavior

During `NormalizeMessage`, read the `Recipients` collection from the Outlook `MailItem` via the existing late-bound COM helpers (`OutlookComHelpers.GetMemberValue` / `GetOptionalMemberValue`). Separate recipients by `Type`: `1` = To, `2` = CC (BCC is not stored). For each qualifying recipient, extract `Name` and `Address` properties. Serialize each group as a JSON array of objects with `name` and `email` keys using `System.Text.Json.JsonSerializer`. Store the resulting strings in `ToJson` and `CcJson` on the `MessageDto`.

Concretely:

- Access `item.Recipients` via `OutlookComHelpers.GetMemberValue(item, "Recipients")`.
- Read `Count` from the collection.
- For each 1-based index, call `InvokeMember(recipients, "Item", index)` to retrieve each recipient COM object.
- Read `Type` (int), `Name` (string), and `Address` (string) from each recipient.
- Place type-1 entries into the `ToJson` array and type-2 entries into the `CcJson` array.
- If the resulting list for a given type is empty, store `null` rather than `"[]"` to keep the null-means-absent convention used throughout the existing schema.
- Release each recipient COM object after reading using `OutlookComHelpers.Release` (or the COM Marshal pattern already used elsewhere), consistent with the broader COM hygiene remediation.

The `ResponseShaper.ShapeMessage` safe-mode path (addressed in a separate feature) is responsible for nulling `ToJson` and `CcJson` before the response is returned to callers. This feature only addresses populating them from the COM item.

## Acceptance Criteria (early draft)

- [ ] After `NormalizeMessage` executes on a mail item that has at least one To recipient, `MessageDto.ToJson` contains a valid JSON array of objects with `name` and `email` string fields.
- [ ] After `NormalizeMessage` executes on a mail item that has at least one CC recipient, `MessageDto.CcJson` contains a valid JSON array with the same shape.
- [ ] Recipients with `Type != 1` are excluded from `ToJson`; recipients with `Type != 2` are excluded from `CcJson`.
- [ ] When there are no To recipients, `ToJson` is `null` (not `"[]"`).
- [ ] When there are no CC recipients, `CcJson` is `null` (not `"[]"`).
- [ ] COM exceptions thrown while reading the `Recipients` collection do not propagate from `NormalizeMessage`; the method returns the DTO with `null` for the affected field instead.
- [ ] BCC recipients (Type 3) are not stored in any JSON field.
- [ ] Each recipient COM object is released after its properties are read.
- [ ] The `CacheRepository` upsert correctly persists the populated `ToJson` and `CcJson` values (existing parameter mapping at lines 367–368 requires no change; this criterion verifies end-to-end flow).
- [ ] The RPC response for `get_message` in enhanced mode returns non-null `to_json` and `cc_json` when the underlying message has recipients (integration-level check).

## Constraints & Risks

- **Late-bound COM access only.** The codebase uses `OutlookComHelpers` with reflection-based property/method invocation throughout. The `Recipients` collection and individual recipient objects must be accessed the same way. Early-bound PIA is a separate remediation item (deviation #2) and must not be conflated with this feature.
- **1-based indexing.** The Outlook `Recipients` collection uses 1-based indexing. `Item(1)` through `Item(Count)` must be used; `Item(0)` will throw.
- **Address field may be an X.400 or SMTP string.** For Exchange internal senders, `Address` may contain an `EX:` or `X500:` value rather than an SMTP address. The feature should store whatever `Address` returns without transformation; address normalization (if desired) is out of scope.
- **COM release discipline.** Each recipient object retrieved via `Item(index)` must be released. Until the broader COM hygiene fix (deviation #14) is applied to `EnumerateItems`, this feature should be consistent with that planned approach and release each recipient after reading.
- **Safe-mode suppression is a separate concern.** This feature populates the fields in the cache. Nulling them in the shaped response is handled by `ResponseShaper.ShapeMessage` (deviation #7 / safe-mode field suppression feature). Both features may be worked in parallel or sequenced, but must not be conflated.
- **No new dependencies.** `System.Text.Json` is already available via `Microsoft.Extensions.Hosting`. No additional NuGet packages are required.
- **Performance.** Reading the `Recipients` collection adds a small number of COM calls per message. For large inbox scans this is proportional to total recipients across all scanned items, not a fixed overhead. No O(N²) pattern is introduced.

## Test Conditions to Consider

- [ ] **Unit — To recipients populated:** Given a mock COM mail item whose `Recipients` collection contains two type-1 entries, verify `NormalizeMessage` returns a `MessageDto` with `ToJson` deserializing to an array of `{name, email}` with the expected values.
- [ ] **Unit — CC recipients populated:** Same setup with type-2 entries; verify `CcJson` is populated correctly.
- [ ] **Unit — No recipients:** A mail item with `Count = 0` on the `Recipients` collection should yield `null` for both `ToJson` and `CcJson`.
- [ ] **Unit — Mixed types filtered correctly:** A Recipients collection containing type-1, type-2, and type-3 entries; verify type-3 entries appear in neither field and each field contains only its expected type.
- [ ] **Unit — COM exception on Recipients access:** When `GetMemberValue(item, "Recipients")` throws, verify `NormalizeMessage` returns a valid DTO with `ToJson = null` and `CcJson = null` rather than propagating the exception.
- [ ] **Unit — Empty list produces null not array:** A mail item where all recipients are type-3 (BCC only) should yield `null` for both `ToJson` and `CcJson`.
- [ ] **Integration — end-to-end cache round-trip:** Populated `ToJson`/`CcJson` values survive the upsert and read-back path in `CacheRepository` without truncation or corruption.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/populate-to-json-cc-json-from-outlook/` folder from the template

