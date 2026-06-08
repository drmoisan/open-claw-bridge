---
name: feature-review-workflow
description: 'Feature-branch review workflow for base-branch resolution, PR-context refresh, active feature folder selection, review artifact generation, validator gates, acceptance-criteria check-off, and remediation triggers. Use when authoring or executing PR-style feature reviews.'
---

# Feature Review Workflow

Reusable workflow guidance for PR-style feature review.

## Required Shared Skills

Always apply:
- `policy-compliance-order`
- `evidence-and-timestamp-conventions`
- `policy-audit-template-usage`
- `pr-context-artifacts`
- `pr-base-branch-merge-base`
- `acceptance-criteria-tracking`
- `remediation-handoff-atomic-planner`

## Role

- Review a feature branch relative to the correct base branch.
- Produce audit artifacts, not implementation fixes.
- Prefer deterministic evidence from canonical PR-context artifacts and exact diff anchors.
- Trigger remediation planning when blockers or unmet acceptance criteria remain.

## Workflow Contract

### Baseline and evidence

- Treat the review as a feature-vs-base audit, not an isolated file inspection.
- Use `artifacts/pr_context.summary.txt` as the primary evidence source.
- Use `artifacts/pr_context.appendix.txt` as the baseline-diff appendix for raw evidence and exact anchors.
- If PR-context artifacts are missing or stale, refresh them per `pr-context-artifacts` using the resolved base branch.

### Review artifact templates

- When creating review artifacts from templates, use the MCP-exposed bundled assets instead of copying directly from repo template paths.
- Resolve the templates through the MCP server tool `resolve_policy_audit_template_asset` with these selectors:
  - `template` for `policy-audit.<timestamp>.md`
  - `code-review-template` for `code-review.<timestamp>.md`
  - `feature-audit-template` for `feature-audit.<timestamp>.md`
- The MCP-resolved asset is the authoritative template source for review artifacts in this workflow.

### No silent fixes

- Do not clean up code during review.
- If checks fail, document the failure and include exact remediation guidance.

### Work-mode acceptance-criteria contract

- Read the persisted marker from `issue.md` using one of:
  - `- Work Mode: minor-audit`
  - `- Work Mode: full-feature`
  - `- Work Mode: full-bug`
- Legacy compatibility: interpret `- Work Mode: full` as `full-feature`.
- Acceptance-criteria sources by marker:
  - `minor-audit`: only the explicit `## Acceptance Criteria` section in `issue.md`
  - `full-feature`: `spec.md` and `user-story.md`
  - `full-bug`: `spec.md`
- Fail closed:
  - if the marker is missing or malformed, use `full-feature`
  - if `minor-audit` is selected and `issue.md` lacks `## Acceptance Criteria`, require remediation

## Ordered Procedure

1. **Resolve the base branch**
   - Use the supplied base branch when it is present.
   - Otherwise resolve `PRBaseBranch` with `pr-base-branch-merge-base`.
   - Record the resolved branch, merge-base SHA, merge-base timestamp, and top competing candidates when available.

2. **Load or refresh PR context**
   - Load the canonical PR-context summary and appendix per `pr-context-artifacts`.
   - Refresh only when the artifacts are missing or stale relative to the current branch state.

3. **Determine the active feature folder**
   - Prefer the active feature folder that corresponds to the primary changed scoping docs.
   - If multiple active folders are present, prefer the one whose suffix matches the issue number in the branch name.
   - Otherwise choose the folder with the most material scoping-doc changes.
   - If no active feature folder exists, create `docs/features/active/<today>-feature-review/` and document the assumption.

4. **Produce the policy audit**
  - Use `policy-audit-template-usage` and the MCP `template` asset to drive the artifact structure.
   - Include exact command references for the checks that were run.
   - Validate the resulting `policy-audit.<timestamp>.md` immediately after writing it.

5. **Run required checks**
   - Prefer repo-defined, check-only commands.
   - Default order:
     1. formatting check
     2. lint check
     3. type check
     4. tests
   - Run the smallest relevant subset first when the repo policy permits it.
   - If a tool cannot run in the environment, mark the affected section unverified or partial with a concrete reason.

6. **Produce the code review**
  - Create the artifact from the MCP `code-review-template` asset.
   - Write `code-review.<timestamp>.md` with:
     - `## Executive Summary`
     - `## Findings Table`
     - a Markdown findings table header containing `Severity | File | Location | Finding | Recommendation | Rationale | Evidence`
   - Include typed-Python review where Python files changed.
   - Validate the artifact immediately after writing it.

7. **Produce the feature audit**
  - Create the artifact from the MCP `feature-audit-template` asset.
   - Write `feature-audit.<timestamp>.md` with:
     - `## Scope and Baseline`
     - `## Acceptance Criteria Inventory`
     - `## Acceptance Criteria Evaluation`
     - `## Summary`
     - `## Acceptance Criteria Check-off`
   - Use the resolved base branch in the baseline section.
   - Evaluate each criterion as PASS, PARTIAL, FAIL, or UNVERIFIED.
   - Check off passing criteria in the authoritative source files per `acceptance-criteria-tracking`.
   - Validate the artifact immediately after writing it.

8. **Trigger remediation when required**
   - Remediation is required when any of the following apply:
     - the policy audit contains meaningful FAIL or PARTIAL results
     - toolchain checks fail
     - the code review contains blockers
     - required acceptance criteria are FAIL or PARTIAL
   - Create `remediation-inputs.<timestamp>.md` first.
   - Create the target remediation plan file from the canonical plan template.
   - Hand off plan creation through `remediation-handoff-atomic-planner`.
   - Do not report completion unless the remediation plan file exists when remediation was triggered.

9. **Finalize the review**
   - Verify every reported artifact exists on disk before reporting completion.
   - Report artifact paths and a concise go/no-go recommendation for PR readiness.

## Required Artifact Shapes

- `policy-audit.<timestamp>.md`
  - copied from the canonical policy-audit template
  - template instruction block removed
  - includes the canonical major headings and Appendix B command reference
- `code-review.<timestamp>.md`
  - contains `## Executive Summary`
  - contains `## Findings Table`
  - contains the required findings table header
- `feature-audit.<timestamp>.md`
  - contains the five required major sections listed above

## Constraints

- Prefer check-only commands.
- Do not claim completion until every required artifact exists and its validator passes.
- Use shared skills as the source of truth for policy order, base-branch resolution, PR-context handling, acceptance-criteria tracking, template usage, and remediation handoff.