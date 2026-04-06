# refactor-and-test (Issue #9)

- Date captured: 2026-04-05
<<<<<<< ours
- Author: drmoisan
- Status: Promoted -> docs/features/active/refactor-and-test/ (Issue #9)
- Issue: #9
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/9
- Last Updated: 2026-04-06

- Work Mode: full-feature

## Problem / Why

The bridge host currently concentrates multiple production classes inside `src/OpenClaw.MailBridge/Program.cs`, which puts the file well over the repository's 500-line guidance and makes targeted changes harder to review, test, and reason about. The test project is also out of policy alignment because it uses NUnit instead of MSTest and does not provide focused regression coverage for the host bootstrapping, state management, and request-handling logic that now lives in the monolithic file.

This work is needed to bring the codebase back toward repo compliance: smaller, cohesive files; explicit ownership boundaries; and deterministic unit coverage for the extracted behaviors. The goal is to reduce maintenance risk without changing the bridge's external behavior.

## Proposed Behavior

Refactor `src/OpenClaw.MailBridge/Program.cs` so that each production class moves into its own dedicated source file, using a folder structure that mirrors responsibilities such as hosting, Outlook integration, RPC, and persistence when that improves readability. Keep the public and internal behavior unchanged while extracting logic into smaller units that can be tested directly.

After the refactor, add or update unit tests in `tests/OpenClaw.MailBridge.Tests/` so they follow repository policy: MSTest test attributes, FluentAssertions assertions where practical, deterministic scenarios, and no reliance on external services. Coverage should specifically target the extracted logic that was previously trapped inside the monolithic `Program.cs` file.

## Acceptance Criteria (early draft)

- [ ] `src/OpenClaw.MailBridge/Program.cs` contains only the `Program` entry-point type and any minimal bootstrapping code that must remain colocated with `Main`.
- [ ] Every additional production type currently declared in `src/OpenClaw.MailBridge/Program.cs` is moved into its own dedicated `.cs` file under `src/OpenClaw.MailBridge/`, using a responsibility-based folder structure when it improves cohesion.
- [ ] The refactor preserves existing runtime behavior for settings loading, host startup, Outlook scanning, cache persistence, and named-pipe RPC handling.
- [ ] Unit tests covering the extracted logic are added or updated in `tests/OpenClaw.MailBridge.Tests/` using MSTest and FluentAssertions, with scenarios for positive flow, invalid input or failure handling, and relevant state transitions.
- [ ] The final change set passes the required C# toolchain loop: `csharpier`, analyzer-enabled build, nullable/type-check build, and test execution.

## Constraints & Risks

- The request exceeds the repository's direct small-change budget because splitting the current file will touch more than three production C# files and likely multiple tests, so it should follow the repo's large-path orchestration workflow before implementation.
- The refactor must avoid behavior drift in background service registration, named-pipe ACL construction, COM initialization, and SQLite state persistence.
- Existing tests currently use NUnit, so bringing the area into policy compliance may require test-project migration work in addition to adding new coverage.
- Tests must remain deterministic and cannot use temporary files or external processes unless an approved exception is introduced.

## Test Conditions to Consider

- [ ] Unit coverage areas: settings-path resolution, argument parsing, bridge state transitions, RPC request validation, oversized request rejection, and response-size fallback behavior.
- [ ] Integration scenarios: host startup with valid settings, startup failure with invalid settings, and status RPC responses that reflect persisted scan-state timestamps.
- [ ] CLI/API examples: `--config <path>` handling, `bridge.getStatus` request/response shape, and unsupported RPC method failures.

## Next Step

- [ ] Promote to GitHub issue (feature request template) and mark the work as a large-path C# refactor/testing effort.
- [ ] Create `docs/features/active/refactor-and-test/` folder from the template, then prepare the required orchestration artifacts (`issue.md`, implementation plan, and QA evidence tasks) before changing production code.
=======
- Author: codex
- Status: Active

- Issue: #9
- Last Updated: 2026-04-06
- Work Mode: full-feature

## Summary

Refactor `OpenClaw.MailBridge` so each runtime class has a dedicated production file and add policy-compliant unit tests to improve maintainability and confidence.

## Acceptance Criteria

- [x] Runtime classes currently co-located in `src/OpenClaw.MailBridge/Program.cs` are split so each class has its own dedicated production file.
- [x] Existing MailBridge runtime behavior remains intact after the refactor (no API contract regressions for bridge startup and internal runtime components).
- [x] Unit tests are added for the refactored runtime files in `src/OpenClaw.MailBridge`.
- [ ] Coverage evidence is produced showing at least 80% line coverage for each newly tested runtime file.
- [x] Solution tests pass after refactor and test additions.
>>>>>>> theirs
