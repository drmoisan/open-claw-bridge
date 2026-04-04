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

## Review Flow

1. Resolve the base branch.
   - Use the supplied base when present.
   - If ambiguous, use `pr-base-branch-merge-base`.
2. Load PR context from the canonical artifacts defined by `pr-context-artifacts`.
3. If PR context is missing or stale, refresh it through `repo-automation-adapter`.
4. Determine the active feature folder deterministically from the scoping docs and PR context.
5. Create the policy audit, code review, and feature audit.
6. Check off passing acceptance criteria in the authoritative requirement sources per `acceptance-criteria-tracking`.
7. If remediation is required, create remediation inputs first and then hand off plan creation using `remediation-handoff-atomic-planner`.

## Review Constraints

- Do not silently fix code during review.
- Prefer check-only commands.
- If a tool cannot be run, mark the related section as unverified or partial with a concrete reason.
- Do not claim completion until every required artifact exists on disk.
