---
paths:
  - "**"
description: Cross-language unit test policy. Applies to all files.
---

# General Unit Test Policy

This rule file summarizes the cross-language unit test policy for this repository.

## Core Principles

Every unit test must satisfy all five of these properties:

1. **Independence** — Tests must be able to run in any order without impacting each other.
2. **Isolation** — Each unit test targets a single function, method, or unit of behavior so failures clearly identify the faulty unit.
3. **Fast execution** — Tests must be fast enough to support frequent runs and rapid feedback loops.
4. **Determinism** — Given the same inputs and environment, tests must produce the same results. Avoid flakiness.
5. **Readability and maintainability** — Test names, structure, and assertions must be clear and easy to understand.

## Coverage Requirements

- **Line coverage must remain >= 85% across all tiers (T1–T4).**
- **Branch coverage must remain >= 75% across all tiers (T1–T4).**
- Code changes or refactors must not reduce coverage for the lines that were changed.
- Tier-specific lower coverage thresholds are not used in this repository. See `.claude/rules/quality-tiers.md` for the full tier system.
- Coverage is a supporting metric, not the sole quality gate. Untested critical behavior is not acceptable even if the overall percentage looks good.
- Configure coverage tooling to exclude test files (e.g., `tests/`) so metrics reflect application code, not tests.
- Agent-harness tooling under `.claude/hooks/**` is classified as T4 scaffolding (see `.claude/rules/quality-tiers.md`) and is excluded from the application coverage surface, consistent with excluding test files and dev `scripts/` tooling. A language whose only changed files on a branch are under `.claude/hooks/**` does not require an application-coverage PASS/FAIL verdict for that language. This scope exclusion does not lower the product-code thresholds: line coverage >= 85% and branch coverage >= 75% remain unchanged for application code.

## Scenario Completeness

For each unit or behavior, tests must cover:

- Positive flows with valid inputs
- Negative flows for invalid or missing inputs
- Edge cases and boundary conditions
- Error-handling behavior
- Concurrency behavior when relevant
- State transitions for stateful components

## Test Structure — Arrange–Act–Assert

Organize each test into three sections:

- **Arrange** — set up inputs, environment, and dependencies
- **Act** — execute the behavior under test
- **Assert** — verify outcomes via assertions

Assertions must produce clear, actionable failure messages.

## External Dependencies

- Unit tests must not depend on external services (databases, networks, remote APIs, external processes).
- Use mocks, stubs, or fakes to isolate the unit under test when code interacts with external systems.
- **Creation and use of temporary files in tests is strictly prohibited.**
- Tests must not rely on mutable global state or external configuration that can change between runs.

## Documentation

- Each test must clearly communicate its purpose via a descriptive name and/or a short docstring or comment summarizing the scenario and expected outcome.
- Group related tests logically within the same file or test class.

## Test Categories

The following test categories apply across the repository, with tier-dependent obligations per `.claude/rules/quality-tiers.md`:

- **Unit tests** — required for all tiers (T1–T4). Cover single units of behavior in isolation.
- **Property-based tests** — required for T1 and T2 modules: at least one property test per pure function. Use `CsCheck` or `FsCheck` (C#) where applicable.
- **Golden / snapshot tests** — required only where a T1 module produces a stable serialized output tested against a versioned corpus. Snapshot tests are otherwise discouraged unless stable and intentional.
- **Contract / schema tests** — required at every host-service boundary (e.g., the Outlook COM surface confined to `OpenClaw.MailBridge`, the host-adapter contract, internal API contracts).
- **Mutation tests** — required for T1 modules: mutation score >= 75%. Run in pre-merge or nightly pipelines.
- **Integration tests** — required where adapters interact with external systems; scoped per tier in the gate matrix.

## Determinism Infrastructure

All test code must be deterministic. The following infrastructure requirements apply uniformly:

- **Controllable clock** — use a `TimeProvider` (.NET) injected into code under test. Do not read wall-clock time directly in production code under test.
- **Seeded RNG** — randomness must be supplied via a seedable interface; on test failure the seed must be printed so the failure is reproducible.
- **Banned APIs in test code** — `Thread.Sleep`, `Task.Delay`, and real wall-clock waits are prohibited in tests; in PowerShell tests `Start-Sleep` is likewise prohibited.
- **Virtual scheduler / fake timers / `FakeTimeProvider`** — async tests must use the framework's fake-timer facility (`FakeTimeProvider` for .NET) to advance simulated time deterministically.
