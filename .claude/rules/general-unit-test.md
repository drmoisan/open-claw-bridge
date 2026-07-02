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
- Type-only / interface-only modules with no executable behavior may be omitted from coverage measurement. Examples: Python `Protocol`-only modules consumed only under `TYPE_CHECKING`, TypeScript interface/type-only files, and C# interface-only files. Such modules legitimately report 0% executable coverage and may be excluded from measurement. This is a clarification only; it does not lower any coverage threshold.

## Coverage Exclusion Policy

No production file may be excluded from coverage measurement. Every production source file is in the denominator of the coverage metric, regardless of whether its lines are reachable in the test environment.

The correct response to a file that contains untestable lines is to refactor it — extract all logic into host-neutral, testable modules and leave only the thinnest possible wiring in the host-bound entry point. The entry point's uncovered lines then represent a real and visible cost in the coverage metric, which creates ongoing pressure to keep those files minimal.

**Permitted `exclude` entries** (non-production paths only):
- Build output directories: `dist/**`, `lib/**`, `lib-amd/**`.
- Test files and test infrastructure: `**/*.test.ts`, `tests/**`, `src/test-support/**`.
- Config files that are not production code: `vitest.config.ts`, `eslint.config.mjs`, `.dependency-cruiser.cjs`, `webpack.config.js`.
- `node_modules/**`.

**Prohibited `exclude` entries:**
- Any path under `src/` that contains production runtime code, regardless of whether it is auto-generated, host-bound, or difficult to test.

**Enforcement:** Feature-review agents must treat any `exclude` entry that matches a production source path as a **Blocking** finding.

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

## Test File Location

Test files must live in a `tests/` directory tree that mirrors the production source structure. The test for `src/foo/bar.ts` belongs at `tests/foo/bar.test.ts`; the test for `scripts/powershell/Foo.ps1` belongs at `tests/scripts/powershell/Foo.Tests.ps1`. Language-specific rules may add further naming conventions (framework suffix, file extension) on top of this universal layout requirement.

Colocation — placing test files alongside production source files in `src/` or equivalent — is not permitted. An agent that creates or moves a test file into the production source tree has violated this rule.

## Documentation

- Each test must clearly communicate its purpose via a descriptive name and/or a short docstring or comment summarizing the scenario and expected outcome.
- Group related tests logically within the same file or test class.

## Test Categories

The following test categories apply across the repository, with tier-dependent obligations per `.claude/rules/quality-tiers.md`:

- **Unit tests** — required for all tiers (T1–T4). Cover single units of behavior in isolation.
- **Property-based tests** — required for T1 and T2 modules: at least one property test per pure function. Use `fast-check` (TypeScript) or `hypothesis` (Python) where applicable.
- **Golden / snapshot tests** — required only for T1 classifier-output modules, tested against a versioned corpus. Snapshot tests are otherwise discouraged unless stable and intentional.
- **Contract / schema tests** — required at every host-service boundary (e.g., Office.js, Microsoft Graph, internal API contracts).
- **Mutation tests** — required for T1 modules: mutation score >= 75%. Run in pre-merge or nightly pipelines.
- **Integration tests** — required where adapters interact with external systems; scoped per tier in the gate matrix.

## Determinism Infrastructure

All test code must be deterministic. The following infrastructure requirements apply uniformly:

- **Controllable clock** — use a `Clock` interface (TypeScript) or `TimeProvider` (.NET) injected into code under test. Do not read wall-clock time directly in production code under test.
- **Seeded RNG** — randomness must be supplied via a seedable interface; on test failure the seed must be printed so the failure is reproducible.
- **Banned APIs in test code** — `setTimeout`, `Thread.Sleep`, `Task.Delay`, real wall-clock waits, and `Date.now()` outside the clock interface are prohibited in tests.
- **Virtual scheduler / fake timers / `FakeTimeProvider`** — async tests must use the framework's fake-timer facility (`vi.useFakeTimers()` for Vitest, `FakeTimeProvider` for .NET) to advance simulated time deterministically.
