---
name: feature-review
description: 'Review a feature branch relative to a base branch and write audit artifacts into the active feature folder. Use when Codex must produce policy, code, and feature audits and trigger remediation planning when needed.'
---

# Feature Review

Workflow skill for PR-style feature review in Codex.

## Required Shared Skills

Always apply:
- `policy-compliance-order`
- `evidence-and-timestamp-conventions`
- `policy-audit-template-usage`
- `pr-context-artifacts`
- `acceptance-criteria-tracking`
- `remediation-handoff-atomic-planner`

Use as needed:
- `pr-base-branch-merge-base`
- `repo-automation-adapter`

## Role

- Review the feature branch relative to the correct base.
- Produce audit-grade artifacts, not code fixes.
- Prefer deterministic evidence from PR-context artifacts and exact diff anchors.
- Trigger remediation planning when blockers or unmet acceptance criteria exist.

## Required Outputs

Write timestamped artifacts into the active feature folder:
- `policy-audit.<timestamp>.md`
- `code-review.<timestamp>.md`
- `feature-audit.<timestamp>.md`
- `remediation-inputs.<timestamp>.md` when remediation is required
- `remediation-plan.<timestamp>.md` when remediation is required

Each required review artifact MUST pass the matching validator command before review can be reported as complete:
- the `validate_orchestration_artifacts` MCP tool with `artifact_type: "policy-audit"` and `artifact_path: <path>`
- the `validate_orchestration_artifacts` MCP tool with `artifact_type: "code-review"` and `artifact_path: <path>`
- the `validate_orchestration_artifacts` MCP tool with `artifact_type: "feature-audit"` and `artifact_path: <path>`

## Review Flow

1. Resolve the base branch.
   - Use the supplied base when present.
  - If the supplied base is missing or ambiguous, use `pr-base-branch-merge-base`.
  - Do not default to the repository default branch unless merge-base resolution fails for all candidates.
2. Load PR context from the canonical artifacts defined by `pr-context-artifacts`.
3. If PR context is missing or stale, refresh it through `repo-automation-adapter` using the resolved base branch.
4. Determine the active feature folder deterministically from the scoping docs and PR context.
5. Create the policy audit, code review, and feature audit.
   - validate each artifact immediately after writing it
6. Check off passing acceptance criteria in the authoritative requirement sources per `acceptance-criteria-tracking`.
7. If remediation is required, create remediation inputs first and then hand off plan creation using `remediation-handoff-atomic-planner`.

### Enforced Remediation Handoff Contract

When remediation is required:

- create `remediation-inputs.<timestamp>.md` before any remediation planning handoff,
- create the remediation plan target file on disk before delegating plan creation,
- automatically delegate remediation planning to `atomic-planner`,
- treat `remediation-inputs.<timestamp>.md` as the primary requirements source,
- include the canonical PR-context summary and appendix, the review artifacts, and the original feature plan file(s) in the delegated context package,
- if the remediation planning handoff cannot be started or does not return a receipt, stop and report blocked state,
- do not claim review completion until the remediation plan file exists on disk.

## Required Artifact Shapes

- `policy-audit.<timestamp>.md`
  - MUST be copied from the canonical template and MUST NOT retain the template instruction block.
  - MUST contain the canonical major headings and Appendix B command reference.
- `code-review.<timestamp>.md`
  - MUST contain `## Executive Summary`.
  - MUST contain `## Findings Table`.
  - MUST contain a Markdown table header with `Severity | File | Location | Finding | Recommendation | Rationale | Evidence`.
- `feature-audit.<timestamp>.md`
  - MUST contain `## Scope and Baseline`.
  - MUST contain `## Acceptance Criteria Inventory`.
  - MUST contain `## Acceptance Criteria Evaluation`.
  - MUST contain `## Summary`.
  - MUST contain `## Acceptance Criteria Check-off`.

## Review Constraints

- Do not silently fix code during review.
- Prefer check-only commands.
- If a tool cannot be run, mark the related section as unverified or partial with a concrete reason.
- Do not claim completion until every required artifact exists on disk and its validator passes.
