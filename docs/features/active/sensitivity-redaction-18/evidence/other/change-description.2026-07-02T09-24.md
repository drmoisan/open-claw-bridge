# Change Description Draft — sensitivity-redaction (#18, co-delivers #20)

Timestamp: 2026-07-02T09-24

Source draft for the PR body. Implements normalization-time sensitivity redaction (#18) and safe-mode field-suppression completion (#20) as one coordinated change in `OpenClaw.MailBridge`. No schema changes, no Contracts changes, no dependency changes, no feature flag (rollback path is revert).

## 1. Deliberate breaking behavioral changes (RPC consumers)

1. **`is_redacted` no longer signals safe mode.** The flag is now written exclusively by normalization-time sensitivity redaction (Outlook `Sensitivity` 2 = Private, 3 = Confidential) and is never mutated by response shaping in either mode. Consumers that used `is_redacted: true` to detect safe-mode suppression must switch to `protected_fields_available: false`.
2. **Safe mode now suppresses the complete protected field set.** In addition to the existing `body_preview`/`sender_name`/`sender_email` (messages) and `body_preview`/`body_full`/attendee-JSON (events) suppression, safe-mode responses now return `null` for `to_json`, `cc_json`, `sender_email_resolved`, and `from_email_address` on messages and for `organizer` on events, return `categories` as an empty array on events, and set `protected_fields_available: false` on both DTOs. `location` is retained in safe mode (decided behavior); only sensitivity redaction removes it. Wire shape (field names/types) is unchanged.

## 2. Deployment note

Redaction is applied at cache-write time. Previously cached unredacted Private/Confidential rows are corrected only when a subsequent scan re-upserts them; until then, stale rows may serve unredacted protected fields in enhanced mode. A cache flush or forced re-scan is recommended after deployment.

## 3. Logging addition

Each redaction emits exactly one Information-level log line recording the bridge id only — never subject, sender, body, attendee, location, or category data (master §2.4 busy-only logging). Template: `"Sensitivity redaction applied; item {BridgeId} retained as busy-only."`

## Supporting detail

- New pure partial `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` holds `IsSensitive` (true only for 2 and 3; 0, 1, null, and out-of-range values are non-sensitive), the message/event redaction transforms, and the never-ingest sensitive-path construction. `OutlookScanner.cs` stays within its 465-line baseline (462 lines post-change).
- Never-ingest ordering: `Sensitivity` is read before any protected member; for sensitive items the scanner performs no `ShapePreview` call, no COM `Body` read, no recipient/attendee enumeration, and no sender SMTP resolution.
- Redacted items remain usable as busy blocks: placeholder subject (`"Private message"` / `"Private appointment"`) plus retained scheduling-mechanical fields (times, busy status, recurrence, ids, `sensitivity`, `sensitivity_label`).
- Documentation updated: `docs/api-reference.md`, `docs/architecture-diagrams.md` (see `docs-review.2026-07-02T09-24.md`).
