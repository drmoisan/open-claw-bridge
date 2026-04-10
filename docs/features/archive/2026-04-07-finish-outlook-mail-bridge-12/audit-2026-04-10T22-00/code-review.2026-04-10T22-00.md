# Code Review

- Timestamp: 2026-04-10T22-00
- Feature: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/finish-outlook-mail-bridge-12`
- Head commit: `d344f1810acdd2e9f583e4e4f23110aff271312d`
- Feature folder: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`

## Executive Summary

The feature branch completes the Outlook mail bridge from a partial stub implementation to a functionally complete, cache-backed RPC surface serving messages, meeting requests, and calendar events over named pipes. The architecture is clean: `OutlookScanner` enumerates Outlook on one STA thread, `CacheRepository` persists normalized rows in SQLite, `PipeRpcWorker` serves deterministic RPC responses, `ResponseShaper` applies privacy modes, and the client resolves pipe names from settings.

The C# toolchain passes cleanly (CSharpier, analyzers, nullable, 87 tests). The implementation is functionally correct and the code quality is generally strong. The primary concerns are structural:

**Top 3 risks:**

1. **File size violations**: Three files exceed the 500-line policy limit (`OutlookScanner.cs` at 580, two test files at 687 and 652). These must be split before merge.
2. **Test harness temp file usage**: `CodexWebSetupScriptHarness.cs` creates temporary directories, violating the no-temp-files test policy.
3. **Evidence accuracy**: The feature-completion artifact reports 95 C# tests at 96.6% coverage, but verified count is 87 tests at 89.4%.

**Go/No-Go recommendation:** No-Go until the three file-size violations are resolved. All functional acceptance criteria are met. The structural violations are mechanical refactors that do not require behavioral changes.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|----------|------|----------|---------|----------------|-----------|----------|
| Blocker | `OutlookScanner.cs` | Whole file (580 lines) | Exceeds 500-line limit by 80 lines | Extract COM helper methods (`GetOptionalString`, `GetOptionalInt`, `GetOptionalBool`, `GetOptionalDateTimeOffset`, `SetMemberValue`, `InvokeMember`, `GetMemberValue`, `GetOptionalMemberValue`) into a new `OutlookComHelpers.cs` internal static class | General code change policy §4.1 mandates max 500 lines | `wc -l` verified this review |
| Blocker | `MailBridgeRuntimeTests.cs` | Whole file (687 lines) | Exceeds 500-line limit by 187 lines | Split configuration validation tests or bridge state tests into a new partial class file (e.g., `MailBridgeRuntimeTests.Config.cs`) | General code change policy §4.1 mandates max 500 lines | `wc -l` verified this review |
| Blocker | `MailBridgeRuntimeTests.OutlookScanner.cs` | Whole file (652 lines) | Exceeds 500-line limit by 152 lines | Split calendar scan tests into a new partial class file (e.g., `MailBridgeRuntimeTests.Calendar.cs`) | General code change policy §4.1 mandates max 500 lines | `wc -l` verified this review |
| Major | `CodexWebSetupScriptHarness.cs` | Line 16 | Uses `Path.GetTempPath()` and creates temporary directories | Either request an explicit policy exception for this harness (document in `general-unit-test.instructions.md`) or refactor to use in-memory patterns | General unit test policy §4 prohibits temp files with no current exception | `grep` for `GetTempPath` verified this review |
| Major | `feature-completion.2026-04-10T17-35.md` | Test count section | Claims 95 C# tests at 96.6% coverage; verified count is 87 at 89.4% | Correct the feature-completion artifact to reflect the verified test count and coverage | Audit evidence must match verified state; overstated metrics undermine trust | `dotnet test` verified 87 total this review |
| Minor | `PipeRpcWorker.cs` | Lines ~237-244 (`WriteResponse`) | Response is serialized to bytes for length check, then re-serialized unconditionally when within limits. The first serialization result is discarded. | Cache the first serialization result and only re-serialize if the oversized branch is taken | Avoids redundant serialization on every response | Code inspection |
| Minor | `BridgeStateStore.cs` | Whole class | Properties read from `PipeRpcWorker` async handler and written from `ScanWorker` on the STA thread without synchronization | Add `volatile` to `State`, `CacheStale`, `OutlookConnected` fields, or document that stale reads are acceptable given the eventual-consistency contract | Concurrent access without synchronization; low risk for value types but technically a data race | Code inspection |
| Nit | `OutlookScanner.cs` | `NormalizeMessage` (~line 370) | `GetOptionalBool(item, "Attachments")` may not return a bool for the `Attachments` property (which is a collection, not a boolean). The fallback `GetOptionalBool(item, "HasAttachments")` is correct. | Verify that the first accessor returns `false` for non-bool COM properties rather than throwing. The `GetOptionalBool` implementation appears to handle this via try-catch, but the intent could be clearer with a comment. | COM property `Attachments` is a `MAPIFolder.Attachments` collection, not a boolean. | Code inspection |

## Typed C# Audit

### Nullable and Type Safety

- [✅] No new nullable warnings. All projects build clean with `Nullable=enable` and `TreatWarningsAsErrors=true`.
- [✅] No use of `dynamic` except through the necessary COM interop reflection path in `OutlookScanner.cs`, which is unavoidable given the late-bound COM model.
- [✅] Guard clauses present at public/internal boundaries (`req is null`, `bridgeId` validation, settings validation).

### Error Handling

- [✅] `InvalidRequestException` is typed and caught specifically in `Handle()`.
- [✅] COM exceptions are caught by type or with `when` clauses in `EnsureOutlook()`.
- [✅] No naked `catch` blocks. All catch blocks either log with context, re-throw, or return a typed error.

### Logging

- [✅] Uses `ILogger<T>` throughout. Structured message templates with named placeholders.
- [✅] No message bodies, event bodies, or attachment content logged (verified by searching for `Body` in log statements).
- [✅] Scan completion counts, state transitions, and error summaries logged at appropriate levels.

### Public API Clarity

- [✅] `internal sealed` used consistently for production classes.
- [✅] Interfaces (`IBridgeRepository`, `IOutlookScanner`, `IOutlookStaExecutor`) define the testable surface.
- [✅] DTO records in Contracts project are public and immutable.

## Test Quality Audit

- [✅] Tests follow Arrange-Act-Assert pattern.
- [✅] Test doubles are well-organized in `MailBridgeRuntimeTestDoubles.cs`.
- [✅] In-memory SQLite is used for repository tests (no filesystem I/O for bridge tests).
- [✅] COM boundary is tested via virtual method overrides on `ComActiveObject`, avoiding real COM calls.
- [✅] Client tests use injected `Func<string, RpcRequest, Task<RpcResponse>>` send delegate.
- [❌] `CodexWebSetupScriptHarness.cs` uses filesystem temp directories (see findings table).
- [✅] 87 tests, 0 failures, 1 platform-guarded skip. Execution time ~11 seconds.

## Security / Correctness Checks

- [✅] No secrets in code. Settings are loaded from local user profile paths.
- [✅] No unsafe subprocess usage. COM is accessed via marshalling, not `Process.Start`.
- [✅] Named-pipe ACL explicitly denies `NetworkSid` and grants only SYSTEM, Administrators, current user, and `openclaw-svc`.
- [✅] Pipe startup fails hard when SID resolution fails (no silent ACL downgrade).
- [✅] Request payload size is bounded (64KB input, 1MB output).
- [✅] SQL parameters used for all SQLite queries (no string interpolation into SQL).
- [✅] Client writes JSON only to stdout and diagnostics only to stderr.
