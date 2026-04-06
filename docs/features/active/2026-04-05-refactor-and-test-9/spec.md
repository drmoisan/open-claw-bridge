# Specification: refactor-and-test

## Scope
- Refactor runtime classes from the monolithic `Program.cs` into dedicated files under `src/OpenClaw.MailBridge/`.
- Preserve public/internal behaviors and dependency registration patterns.
- Expand test coverage in `tests/OpenClaw.MailBridge.Tests` to cover refactored runtime classes.

## Non-goals
- No protocol or contract changes in `OpenClaw.MailBridge.Contracts`.
- No behavioral feature additions beyond testability/structure improvements.

## Validation
- Build and test the solution.
- Produce code coverage artifact with per-file percentages and verify ≥80% for targeted runtime files.
