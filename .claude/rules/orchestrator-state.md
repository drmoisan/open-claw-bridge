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

## Complexity-Assessment Scope and Backward Compatibility

These invariants apply only when the checkpoint contains a top-level `complexity_assessments` array. A checkpoint with no `complexity_assessments` key (the existing checkpoint shape) is unaffected: it validates exactly as before and produces no new errors. The invariants are additive and support the two-axis model-selection mechanism documented in `.claude/skills/orchestrate/SKILL.md` (`## Model Selection`). Enforcement is the Python validator, not an imported schema.

## Invariants (complexity_assessments array)

Each entry records one assessed phase with the shape `{ phase, band, floor, signals_present[], rationale, assessed_at }`.

1. **Band enum membership.** Each entry's `band` must be one of `C1`, `C2`, `C3`, `C4`. A missing or out-of-enum `band` is a malformed entry.

2. **Band at or above floor.** Each entry must satisfy `band >= floor` using the band ordering `C1 < C2 < C3 < C4`. The floor constrains the lower bound only; a `band` below its `floor` is a malformed entry.

3. **Floor equals the computed floor.** Each entry's `floor` must equal `compute_complexity_floor(signals_present)` (the reference implementation in `scripts/dev_tools/compute_complexity_floor.py`). A `floor` that does not equal the recomputed value is a malformed entry. C4 is never floor-forced; the floor never exceeds `C3`.

4. **Non-empty `rationale`.** Each entry's `rationale` must be a non-empty string. A missing, non-string, or empty/whitespace-only `rationale` is a malformed entry.

The validator never judges the merit of the assessed band; it checks shape, floor equality, and lower-bound ordering only.

## Model-Routing-Receipt Scope and Backward Compatibility

These invariants apply only when the checkpoint contains a top-level `model_routing_receipts` array. A checkpoint with no `model_routing_receipts` key (the existing checkpoint shape) is unaffected: it validates exactly as before and produces no new errors. The invariants are additive. Enforcement is the Python validator, not an imported schema.

## Invariants (model_routing_receipts array)

Each entry records one delegation with the shape `{ agent, phase, complexity_band, fable_policy, table_model, clamped_from | null, model }`.

1. **Model equals the resolved model.** Each entry's `model` must equal `resolve_delegation_model(agent, complexity_band, fable_policy)["model"]` (the reference implementation in `scripts/dev_tools/resolve_delegation_model.py`). A `complexity_band` outside `C1`..`C4` is a malformed entry. A `model` that does not equal the resolved model is a malformed entry.

2. **Disabled-mode clamp.** Under `fable_policy == "disabled"` no entry's `model` may equal `fable`; any entry whose `table_model == "fable"` must record `clamped_from == "fable"` and `model == "opus"`. A disabled-mode entry that resolves to `fable`, or a disabled-mode `fable` table cell that does not record the clamp, is a malformed entry.

## Model-Budget Contract

The session `model_budget.fable_policy` switch is a three-way enum `disabled | available | preferred` defined in `config/orchestration-routing.json`, defaulting to `disabled`. It governs only the delegation model tier and is not a route input. `disabled` removes `fable` from the consideration set and clamps `fable` cells to `opus`; `available` applies the base `complexity_to_model` table as-is; `preferred` applies the `preferred_overlay` (which redirects only the C3 cell to `fable` for the overlay agents `atomic-planner`, `prd-feature`, `feature-review`, `task-researcher`) and leaves `atomic-executor` and `pr-author` C3 cells at `opus`. `route` is never an input to model selection.

## Require-Model-Routing Mode Scope and Backward Compatibility

The complexity-assessment and model-routing-receipt invariants above are key-gated: they run only when their key is present, so a checkpoint that omits both arrays passes at every stage. The `require_model_routing` mode adds an existence gate that closes that gap without changing the default behavior. It is an opt-in keyword on `validate_orchestrator_state_text(..., require_model_routing=False)` (CLI flag `--require-model-routing`; MCP parameter `require_model_routing`), defaulting off. Plain, `require_complete`, and `require_pr_creation_ready` calls are unaffected and produce byte-identical results.

## Invariants (require_model_routing mode)

These invariants apply only when a caller passes `require_model_routing=True` and the checkpoint records at least one delegation. A checkpoint with zero delegations (no well-formed `delegation_receipts[]` entry and a `next_step` that names no delegating agent) imposes no requirement, so genuinely old, delegation-free checkpoints stay valid.

1. **Required routing receipt once delegated.** Once the checkpoint records a delegation, the set of `model_routing_receipts[].agent` must be a superset of the delegated-agent set (each well-formed `delegation_receipts[].agent_name` plus a `next_step` that names a delegating agent). A delegated agent with no matching receipt is a violation. The delegating agent set excludes `orchestrator` (the caller, not a delegated subagent).

2. **Required complexity assessment per matched phase.** Each phase named by a routing receipt whose agent is in the delegated-agent set must have a `complexity_assessments[]` entry for that phase.

3. **Per-entry consistency reused, not reimplemented.** Present receipts and assessments must satisfy the model-routing-receipt and complexity-assessment invariants above; the gate reuses `_validate_model_routing_receipts` and `_validate_complexity_assessments` and never reimplements `compute_complexity_floor` or `resolve_delegation_model`. The gate logic lives in `scripts/dev_tools/_orchestrator_state_model_routing_gate.py`; enforcement is the Python validator, not an imported schema.

The completion hook (`.claude/hooks/validate-orchestrator-output.ps1`) passes `--require-model-routing` alongside `--require-complete` and surfaces a gate failure as the `MODEL_ROUTING_BLOCKED:` block reason. The PreToolUse deterrent (`.claude/hooks/enforce-model-routing-receipt.ps1`) performs presence-only gating before a delegation. The MCP TypeScript surface performs the existence check only (delegated-agent set ⊆ routing-receipt-agent set); the Python validator remains authoritative for per-receipt correctness.

## Enforcement

- `scripts/dev_tools/validate_orchestrator_state.py` appends one error per violated invariant when a `remediation_loop` is present, using the existing validator message style (literal, checkpoint-context prefixed). The validator returns a list of error strings and does not mutate its input.
- `scripts/dev_tools/validate_orchestrator_state.py` likewise appends one error per violated `human_interaction` invariant when a `human_interaction` key is present, using the same literal, checkpoint-context-prefixed message style. The check does not import or read any schema file.
- `scripts/dev_tools/validate_orchestrator_state.py` appends one error per violated `complexity_assessments` invariant when a `complexity_assessments` key is present, delegating to `scripts/dev_tools/_orchestrator_state_complexity.py`, which recomputes the floor via `compute_complexity_floor`. The check does not import or read any schema file.
- `scripts/dev_tools/validate_orchestrator_state.py` appends one error per violated `model_routing_receipts` invariant when a `model_routing_receipts` key is present, delegating to `scripts/dev_tools/_orchestrator_state_model_routing.py`, which recomputes the resolved model via `resolve_delegation_model`. The check does not import or read any schema file.
- `scripts/dev_tools/validate_orchestrator_state.py` appends one error per violated `require_model_routing` invariant only when the caller passes `require_model_routing=True`, delegating to `scripts/dev_tools/_orchestrator_state_model_routing_gate.py`, which reuses the complexity and model-routing per-entry validators. When the flag is not passed the gate does not run, so existing calls are byte-identical.
- The validator is consumed by the MCP tool `validate_orchestration_artifacts`; backward compatibility for existing step-based checkpoints is preserved.
