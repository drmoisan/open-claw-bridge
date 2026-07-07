# Regression Evidence — ParseSubscription Fail-Fast Arms (Remediation Cycle 1, Issue #117, fix items 1-2)

Timestamp: 2026-07-03T09-12
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~GraphSubscriptionManagerTests"
EXIT_CODE: 0
Output Summary:
- Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6 (OpenClaw.Core.Tests.dll)
- The two new tests pass alongside the existing four:
  - `CreateAsync_missing_id_in_the_response_fails_fast_and_persists_nothing` (line-320 `IsNullOrWhiteSpace(wire.Id)` throw arm; asserts envelope code `INTERNAL_ERROR` and empty `FakeSubscriptionStore`)
  - `CreateAsync_body_deserializing_to_json_null_fails_fast_and_persists_nothing` (line-315 `?? throw new JsonException` arm; asserts envelope code `TRANSPORT_FAILURE` and empty store)
  - Existing: `CreateAsync_pins_the_exact_post_request_shape`, `CreateAsync_persists_the_created_record_in_the_store`, `RenewAsync_pins_the_exact_patch_request_shape_with_expiration_only`, `RenewAsync_updates_the_stored_record_expiration_and_status` — all unmodified, all passing.
