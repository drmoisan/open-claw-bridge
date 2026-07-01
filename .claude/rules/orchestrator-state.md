# Orchestrator-State Remediation-Cycle and Human-Interaction Invariants

This rule governs remediation-cycle records and the optional `human_interaction` block in the orchestrator-state checkpoint at `artifacts/orchestration/orchestrator-state.json`. It documents three invariants that must hold for each remediation cycle, plus three invariants for the `human_interaction` block, so that resume and review workflows do not depend on a structurally invalid checkpoint.

## Foreign Schema Warning (do not copy verbatim)

A hardened snapshot from another repository contains a JSON Schema for the orchestrator-state artifact whose `$id` references a foreign origin (`drmoisan.github.io/mix-calculator/`). That schema MUST NOT be copied verbatim into this repository: its `$id`, its top-level required-field set, and its cycle-level `additionalProperties: false` do not match this repository's checkpoint contract. The invariants below are re-expressed here as prose and enforced by validator logic in `scripts/dev_tools/validate_orchestrator_state.py`, not by importing a foreign schema file.

This prohibition is specific to the disqualified foreign schema identified by the `drmoisan.github.io/mix-calculator/` `$id`. A schema whose `$id` is repo-local and whose required-field set and `additionalProperties` policy match this repository's checkpoint contract is not the disqualified foreign artifact; even so, the repository's enforcement mechanism remains the Python validator prose-and-logic above, not an imported schema file.

## Scope and Backward Compatibility

These invariants apply only when the checkpoint contains a top-level `remediation_loop` with a `cycles` array. A checkpoint with no `remediation_loop` (the existing step-based checkpoint shape) is unaffected: it validates exactly as before and produces no new errors. The invariants are additive.

## Invariants (per remediation cycle)

1. **Non-empty `plan_path`.** Each cycle's `plan_path` must be a non-empty string. A missing value, a non-string value, or an empty/whitespace-only string is a malformed cycle.

2. **Execution requires cleared preflight.** A cycle's `execution_status` may be in `{in_progress, complete, failed}` only when that cycle's `preflight.final_status` is exactly `'clear'`. Any other preflight status with one of those execution statuses is a malformed cycle (execution was recorded before preflight cleared).

3. **Exit gate requires zero blocking findings.** When a cycle's `exit_condition_met == true`, its `blocking_count` must be `0`. A non-zero `blocking_count` with `exit_condition_met == true` is a malformed cycle (the exit gate was marked satisfied while blocking findings remained).

## Human-Interaction Scope and Backward Compatibility

These invariants apply only when the checkpoint contains a top-level `human_interaction` block. A checkpoint with no `human_interaction` key (the existing checkpoint shape) is unaffected: it validates exactly as before and produces no new errors. The invariants are additive and support the autonomous-execution mandate documented in `.claude/skills/orchestrate/SKILL.md`.

## Invariants (human_interaction block)

1. **Required `requirements` list.** When `human_interaction` is present, it must be an object containing a `requirements` list. A non-object `human_interaction`, or a `requirements` value that is not a list, is a malformed block.

2. **Per-requirement `response` enum membership.** Each requirement must be an object whose `response` value is one of `scope_change`, `exception`, or `halt`. A requirement that is not an object, or whose `response` is outside this enum, is a malformed requirement.

3. **Exception requires `runbook_path`.** A requirement whose `response == "exception"` must carry a non-empty `runbook_path` string. A missing, non-string, or empty/whitespace-only `runbook_path` on an `exception` requirement is a malformed requirement.

## Enforcement

- `scripts/dev_tools/validate_orchestrator_state.py` appends one error per violated invariant when a `remediation_loop` is present, using the existing validator message style (literal, checkpoint-context prefixed). The validator returns a list of error strings and does not mutate its input.
- `scripts/dev_tools/validate_orchestrator_state.py` likewise appends one error per violated `human_interaction` invariant when a `human_interaction` key is present, using the same literal, checkpoint-context-prefixed message style. The check does not import or read any schema file.
- The validator is consumed by the MCP tool `validate_orchestration_artifacts`; backward compatibility for existing step-based checkpoints is preserved.
