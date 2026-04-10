# 2026-04-05-refactor-and-test - Refactor Spec

- **Issue:** #9
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-06T14-25
- **Status:** Draft
- **Version:** 0.1

## Intent & Outcomes

The bridge host currently concentrates multiple production classes inside `src/OpenClaw.MailBridge/Program.cs`, which puts the file well over the repository's 500-line guidance and makes targeted changes harder to review, test, and reason about. The test project is also out of policy alignment because it uses NUnit instead of MSTest and does not provide focused regression coverage for the host bootstrapping, state management, and request-handling logic that now lives in the monolithic file.

This work is needed to bring the codebase back toward repo compliance: smaller, cohesive files; explicit ownership boundaries; and deterministic unit coverage for the extracted behaviors. The goal is to reduce maintenance risk without changing the bridge's external behavior.

## Invariants (must not change)

List the behaviors, contracts, and external surfaces that must remain identical (CLIs, APIs, outputs, data formats, paths).
- Performance characteristics to preserve (latency/throughput/memory):
- Compatibility guarantees (CLI flags, config schemas, versions):

## Scope (structural changes)

Refactor `src/OpenClaw.MailBridge/Program.cs` so that each production class moves into its own dedicated source file, using a folder structure that mirrors responsibilities such as hosting, Outlook integration, RPC, and persistence when that improves readability. Keep the public and internal behavior unchanged while extracting logic into smaller units that can be tested directly.

After the refactor, add or update unit tests in `tests/OpenClaw.MailBridge.Tests/` so they follow repository policy: MSTest test attributes, FluentAssertions assertions where practical, deterministic scenarios, and no reliance on external services. Coverage should specifically target the extracted logic that was previously trapped inside the monolithic `Program.cs` file.

## Non-Goals

What is explicitly out of scope (new behavior, perf changes, UX changes, flags).
- No protocol or contract changes in `OpenClaw.MailBridge.Contracts`.
- No behavioral feature additions beyond testability and structure improvements.

## Dependencies / Touchpoints

Upstream/downstream modules, CLIs, data paths, automation, or external consumers that rely on current structure.
- Required coordination (other teams, CI/CD, release tooling):

## Risks & Mitigations

- The request exceeds the repository's direct small-change budget because splitting the current file will touch more than three production C# files and likely multiple tests, so it should follow the repo's large-path orchestration workflow before implementation.
- The refactor must avoid behavior drift in background service registration, named-pipe ACL construction, COM initialization, and SQLite state persistence.
- Existing tests currently use NUnit, so bringing the area into policy compliance may require test-project migration work in addition to adding new coverage.
- Tests must remain deterministic and cannot use temporary files or external processes unless an approved exception is introduced.

## Technical Specifications

- Files/modules expected to change:
- Public interfaces/contracts affected (even if behavior is unchanged):
- Data flow or validation adjustments:
- Logging/telemetry updates (if any):
- Migration or backfill needs (if any):

## Test Strategy

- Regression tests to add or update:
- Invariant validation tests (ensuring outputs/behavior unchanged):
- Edge cases and negative scenarios (import/path stability, CLI flags):
- Error handling and logging verification:
- Coverage impact and targets for changed lines/modules: produce a coverage artifact with per-file percentages and verify at least `80%` for the targeted runtime files.
- Toolchain commands to run (format → lint → type-check → test): build and test the solution with the repo-approved C# command loop.
- Manual validation steps (if required):

## Validation

- Build and test the solution.
- Produce a code coverage artifact with per-file percentages and verify at least `80%` for the targeted runtime files.

## Definition of Done

- [ ] Structure matches this spec; legacy paths retired or redirected
- [ ] Invariants validated with tests or comparisons
- [ ] Imports/tooling/entry points updated
- [ ] Edge cases and error handling verified
- [x] Tests, linting, and type checks clean
- [ ] Docs updated (initiative/README/tasks as needed)
- [x] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Unit coverage areas: settings-path resolution, argument parsing, bridge state transitions, RPC request validation, oversized request rejection, and response-size fallback behavior.
- [ ] Integration scenarios: host startup with valid settings, startup failure with invalid settings, and status RPC responses that reflect persisted scan-state timestamps.
- [ ] CLI/API examples: `--config <path>` handling, `bridge.getStatus` request/response shape, and unsupported RPC method failures.
