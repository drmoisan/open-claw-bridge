---
name: atomic-plan-contract
description: 'Atomic plan format and QA contract shared by planning and execution skills. Use when generating, validating, or executing plans with Phase 0, baseline evidence, and final QA loops.'
---

# Atomic Plan Contract

Shared rules for atomic plan formatting, Phase 0 requirements, baseline evidence, and final QA loops.

## Canonical Plan Format

- Phase headings must be `### Phase N — <Title>`.
- Tasks must start with `- [ ] [P#-T#]` or `- [x] [P#-T#]`.
- Task IDs must match the phase number and be sequential within the phase.

## Phase 0 Requirements

Phase 0 must:
- read repository policy in the order defined by `policy-compliance-order`,
- capture baseline toolchain state for each language touched,
- write baseline artifacts using the conventions in `evidence-and-timestamp-conventions`.

Baseline command-step artifacts must include:
- `Timestamp:`
- `Command:`
- `EXIT_CODE:`
- `Output Summary:`

## Final QA Loop

For plans that change code or tests, the final QA phase must run the applicable toolchain loop in order:

1. Formatting
2. Linting
3. Type checking when applicable
4. Testing

If any step fails or changes files, restart from step 1 until the final pass is clean.

## Minor-Audit Gate

`minor-audit` plans must include:
- baseline evidence tasks,
- targeted verification evidence tasks,
- end-state evidence tasks.

They must not depend on `spec.md` or `user-story.md` when those documents are intentionally absent.

## Expect-Fail Tasks

Any regression-test task expected to fail before a later fix must:
- include the exact `[expect-fail]` tag in the task title,
- identify the exact command to run,
- record auditable evidence in the canonical regression-testing location.

## Preflight Signals

Use these exact signals for executor preflight validation:
- `PREFLIGHT: ALL CLEAR`
- `PREFLIGHT: REVISIONS REQUIRED`
