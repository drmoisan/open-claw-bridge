---
name: remediation-handoff-atomic-planner
description: 'Reusable remediation trigger and atomic-planner handoff steps. Use when audits or validation workflows require remediation inputs and a delegated remediation plan.'
---

# Remediation Handoff To Atomic Planner

Shared remediation workflow for agents that must create remediation inputs and delegate plan creation.

## Trigger Conditions

Trigger remediation when any of these are true:
- audit artifacts contain `FAIL` or meaningful `PARTIAL` findings,
- toolchain checks fail,
- required acceptance criteria are unmet.

## Required Inputs

Create `remediation-inputs.<timestamp>.md` with:
- enumerated fixes,
- exact file paths and expected behavior,
- verification commands,
- a clear do-not-do list.

## Handoff

1. Create the remediation plan target file.
2. Delegate plan creation to `atomic-planner`.
3. Treat remediation inputs as the primary requirements source.
4. Preserve the authoritative target plan path across revisions.
