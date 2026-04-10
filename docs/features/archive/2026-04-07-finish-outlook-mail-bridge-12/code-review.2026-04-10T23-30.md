# Code Review

- Timestamp: 2026-04-10T23-30
- Feature: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/finish-outlook-mail-bridge-12`
- Head commit: `d344f1810acdd2e9f583e4e4f23110aff271312d`
- Feature folder: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`
- Prior review: `code-review.2026-04-10T22-00.md` (pre-remediation)

## Executive Summary

This is a post-remediation re-review of the feature branch. The feature completes the Outlook mail bridge from a partial stub implementation to a functionally complete, cache-backed RPC surface serving messages, meeting requests, and calendar events over named pipes. The architecture is clean: `OutlookScanner` enumerates Outlook on one STA thread, `CacheRepository` persists normalized rows in SQLite, `PipeRpcWorker` serves deterministic RPC responses, `ResponseShaper` applies privacy modes, and the client resolves pipe names from settings.

The remediation resolved all three Blocker findings from the prior review (file-size violations) and both Major findings (temp-file policy exception, evidence correction). No behavioral changes were made.

**Top 3 risks (post-remediation):**

1. **PowerShell coverage gap (78.7% < 80%)**: Pre-existing condition with documented cause. Not a functional risk; recommended for follow-up.
2. **BridgeStateStore concurrent access**: Properties read from async handlers and written from the STA thread without synchronization. Low risk for value types but technically a data race.
3. **Coverage tool discrepancy (89.4% vs 83.9%)**: Two different coverage tools produce different measurements. Both exceed 80% minimum. Recommend standardizing on one tool.

**Go/No-Go recommendation:** Go. All prior Blocker and Major findings are resolved. The remaining items are Minor or informational and do not block merge.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|----------|------|----------|---------|----------------|-----------|----------|
| ~~Blocker~~ | ~~`OutlookScanner.cs`~~ | ~~Whole file~~ | ~~580 lines exceeded 500-line limit~~ | **RESOLVED** — 495 lines after COM helper extraction to `OutlookComHelpers.cs` (104 lines) | Remediation P1-T1/T2 | `(Get-Content ...).Count` verified this review |
| ~~Blocker~~ | ~~`MailBridgeRuntimeTests.cs`~~ | ~~Whole file~~ | ~~687 lines exceeded 500-line limit~~ | **RESOLVED** — 346 lines after pipe test extraction to `MailBridgeRuntimeTests.Pipe.cs` (356 lines) | Remediation P1-T4/T5/T6 | `(Get-Content ...).Count` verified this review |
| ~~Blocker~~ | ~~`MailBridgeRuntimeTests.OutlookScanner.cs`~~ | ~~Whole file~~ | ~~652 lines exceeded 500-line limit~~ | **RESOLVED** — 309 lines after calendar test extraction to `MailBridgeRuntimeTests.Calendar.cs` (357 lines) | Remediation P1-T7/T8/T9 | `(Get-Content ...).Count` verified this review |
| ~~Major~~ | ~~`CodexWebSetupScriptHarness.cs`~~ | ~~Line 16~~ | ~~Temp file usage without policy exception~~ | **RESOLVED** — Exception documented in `.github/instructions/general-unit-test.instructions.md` §4 for exactly two files | Remediation P1-T10 | Policy file inspection this review |
| ~~Major~~ | ~~`feature-completion.2026-04-10T17-35.md`~~ | ~~Test count section~~ | ~~Incorrect test count (95) and coverage (96.6%)~~ | **RESOLVED** — Corrected to 87 tests and 89.4% coverage | Remediation P1-T11 | Artifact inspection this review |
| Minor | `PipeRpcWorker.cs` | Lines ~237-244 (`WriteResponse`) | Response is serialized to bytes for length check, then re-serialized unconditionally when within limits. The first serialization result is discarded. | Cache the first serialization result and re-serialize only if the oversized branch is taken | Avoids redundant serialization on every response; low impact given typical payload sizes | Code inspection |
| Minor | `BridgeStateStore.cs` | Whole class | Properties read from `PipeRpcWorker` async handler and written from `ScanWorker` on the STA thread without synchronization | Add `volatile` to `State`, `CacheStale`, `OutlookConnected` fields, or document that stale reads are acceptable given the eventual-consistency contract | Concurrent access without synchronization; low risk for value types but technically a data race | Code inspection |
| Nit | `OutlookScanner.cs` | `NormalizeMessage` (~line 370) | `GetOptionalBool(item, "Attachments")` accesses a collection property as a bool. The fallback `GetOptionalBool(item, "HasAttachments")` is correct. | Add a brief comment clarifying the two-accessor pattern, or remove the first accessor if it never returns a meaningful value | COM property `Attachments` is a collection, not a boolean; `GetOptionalBool` handles this via try-catch | Code inspection |

## Typed C# Audit

### Nullable and Type Safety

- [✅] No new nullable warnings. All projects build clean with `Nullable=enable` and `TreatWarningsAsErrors=true`.
- [✅] No use of `dynamic` except through the necessary COM interop reflection path in `OutlookScanner.cs` and `OutlookComHelpers.cs`, which is unavoidable given the late-bound COM model.
- [✅] Guard clauses present at public/internal boundaries (`req is null`, `bridgeId` validation, settings validation).
- [✅] The extracted `OutlookComHelpers` class maintains the same type contracts as the original inline methods.

### Error Handling

- [✅] `InvalidRequestException` is typed and caught specifically in `Handle()`.
- [✅] COM exceptions are caught by type or with `when` clauses in `EnsureOutlook()`.
- [✅] No naked `catch` blocks. All catch blocks either log with context, re-throw, or return a typed error.

### Logging

- [✅] Uses `ILogger<T>` throughout. Structured message templates with named placeholders.
- [✅] No message bodies, event bodies, or attachment content logged.
- [✅] Scan completion counts, state transitions, and error summaries logged at appropriate levels.

### Public API Clarity

- [✅] `internal sealed` used consistently for production classes.
- [✅] `OutlookComHelpers` is `internal static` — appropriate for extracted utility methods.
- [✅] Interfaces (`IBridgeRepository`, `IOutlookScanner`, `IOutlookStaExecutor`) define the testable surface.
- [✅] DTO records in Contracts project are public and immutable.

## Test Quality Audit

- [✅] Tests follow Arrange-Act-Assert pattern.
- [✅] Test doubles are well-organized in `MailBridgeRuntimeTestDoubles.cs` (397 lines, within limit).
- [✅] In-memory SQLite is used for repository tests (no filesystem I/O for bridge tests).
- [✅] COM boundary is tested via virtual method overrides on `ComActiveObject`, avoiding real COM calls.
- [✅] Client tests use injected `Func<string, RpcRequest, Task<RpcResponse>>` send delegate.
- [✅] `CodexWebSetupScriptHarness.cs` temp-file usage is now covered by an approved policy exception.
- [✅] The partial class split preserved all test methods and did not change test behavior (87 tests, same as pre-remediation).
- [✅] New partial class files (`MailBridgeRuntimeTests.Pipe.cs`, `MailBridgeRuntimeTests.Calendar.cs`) use correct namespace and class declarations.
- [✅] 87 tests, 0 failures, 1 platform-guarded skip. Execution time ~10 seconds.

## Security / Correctness Checks

- [✅] No secrets in code. Settings are loaded from local user profile paths.
- [✅] No unsafe subprocess usage. COM is accessed via marshalling, not `Process.Start`.
- [✅] Named-pipe ACL explicitly denies `NetworkSid` and grants only SYSTEM, Administrators, current user, and `openclaw-svc`.
- [✅] Pipe startup fails hard when SID resolution fails (no silent ACL downgrade).
- [✅] Request payload size is bounded (64KB input, 1MB output).
- [✅] SQL parameters used for all SQLite queries (no string interpolation into SQL).
- [✅] Client writes JSON only to stdout and diagnostics only to stderr.
- [✅] The `OutlookComHelpers` extraction did not introduce any new security surface; methods remain internal and static.
