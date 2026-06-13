---
name: checkpoint-validator-contract
description: The bundled orchestrator-state validator's required-key and enum contract, which differs from the orchestrator prompt's described shape.
metadata:
  type: project
---

The authoritative `orchestrator-state.json` contract is enforced by `validate_orchestration_artifacts` (artifact_type `orchestrator-state`), whose validator lives in the bundled npm package `@danmoisan/drm-copilot-mcp` at `resources/scripts/dev_tools/validate_orchestration_artifacts.py` — NOT in the repo's `.claude/schemas/orchestrator-state.schema.json` (that JSON schema is looser and only governs the `remediation_loop` shape). Build the checkpoint to the validator from the start to avoid an end-of-run remediation.

Required keys (all must be present): `objective`, `change_budget_estimate`, `path_selected`, `promotion-type`, `short-name`, `relativeFile`, `long-name`, `issue-num`, `feature-folder`, `work-mode`, `plan-path`, `completed_steps`, `next_step`, `last_updated`, `step5_status`..`step10_status`, `delegation_receipts`, `blocked_reason`.

- `stepN_status` enum: `not-applicable | pending | delegated | verified | blocked` (use `verified` for completed steps — NOT `complete`/`passed`/`in_progress`).
- `blocked_reason` enum: `none | spawn_agent_unavailable | delegation_launch_failed | delegate_no_receipt | delegate_contract_incomplete | validator_failed | user_requested_stop` (or null).
- `delegation_receipts` MUST be a **list** of objects, each with keys: `step`, `agent_name`, `agent_id`, `skill_source`, `started_at`, `completed_at`, `result_signal`, `artifact_paths` (a list). This contradicts the orchestrator prompt, which describes `delegation_receipts.promotion.{potential_entry,issue,feature_folder}` as a nested object — that object shape FAILS the validator. Put promotion data under a different key (e.g. `promotion_receipts`).
- `work-mode` is hyphenated and must be `minor-audit | full-feature | full-bug` (normalize legacy `full` to `full-feature`).
- `require_complete=true` additionally rejects any step status of `pending` or `blocked`, and requires `blocked_reason` to be null/`none`.

**Why:** During issue #72 orchestration the DONE checkpoint failed validation because step statuses used `complete`/`passed` and `delegation_receipts` was a nested object per the prompt; the validator requires the enum values above and a receipt list.

**How to apply:** Author the checkpoint with these keys/enums on the first write of each step; keep `delegation_receipts` as an append-only list of per-delegation receipts. Run `validate_orchestration_artifacts` (orchestrator-state, `require_complete=true`) before declaring DONE. Related harness context: [[harness-governance]].
