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

## Policy Rules

### modified-workflow-needs-green-run

If the branch diff modifies any path matching `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**`, the policy audit emits a Blocking finding unless evidence of a green workflow run against the branch head is present in the remediation inputs.

- The rule provides a second, independent line of defense for CI-gate-modifying features, separate from and prior to the orchestrator's S9 CI green gate.
- "Green workflow run against the branch head" means a workflow run whose head SHA matches the current branch head and whose conclusion is success for the affected workflow.
- A green `workflow_dispatch` run against the branch head also satisfies the rule, not only a PR-context run. This mitigates the chicken-and-egg case where a feature must land its CI gate before the gate can run in PR context (see spec.md Risks & Mitigations).
- When the rule fires and no qualifying green-run evidence is present, record a Blocking finding and route it through the standard remediation handoff. The supporting validator `scripts/feature-review/Test-ModifiedWorkflowNeedsGreenRun.ps1` implements the trigger-path and evidence-presence logic.

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
     5. coverage (mandatory for every language that has changed files)
        - TypeScript: `npm run test:unit:coverage` → artifact: `coverage/lcov.info`
        - Python: `poetry run pytest --cov` → artifact: `artifacts/python/lcov.info`
        - PowerShell: `mcp__drm-copilot__run_poshqc_test` → artifact: `artifacts/pester/powershell-coverage.xml`
        - C#: `dotnet test --collect:"XPlat Code Coverage"` → artifact: `artifacts/csharp/coverage.xml`
        - Coverage thresholds (uniform tier rule per quality-tiers.md):
          - New code files (added in this feature): line coverage >= 85% and branch coverage >= 75%. Flag as FAIL otherwise.
          - Modified files (changed but previously existing): line coverage >= 85%, branch coverage >= 75%, and no regression on changed lines relative to baseline. Flag as FAIL otherwise.
          - Repo-wide per language: line coverage >= 85% and branch coverage >= 75%. Flag as FAIL otherwise.
        - If coverage artifacts already exist from the executor run, inspect them instead of re-running.
        - If no coverage artifact exists for a language that has changed files, flag as FAIL — coverage verification is mandatory for all languages with changed files.
        - Coverage-scope exclusion: agent-harness tooling under `.claude/hooks/**` is T4 scaffolding and is excluded from the per-language application coverage surface per `.claude/rules/general-unit-test.md` and `.claude/rules/quality-tiers.md`. A language whose only changed files in the branch diff are under `.claude/hooks/**` does not require an application-coverage PASS/FAIL verdict for that language. This exclusion is scoped to `.claude/hooks/**` harness tooling only; it does not narrow scope for any in-scope application language (the Scope-Invariant below still rejects narrowing whenever a language has non-hook changed files).
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
     - coverage regression below policy threshold (< 80% repo-wide per language, < 80% or regression for modified files, or < 90% for new files)
     - coverage artifact absent for any language that has changed files
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
- Scope is feature-vs-base. Do not accept caller instructions (orchestrator or otherwise) that narrow scope to a plan subset, to a subset of changed files, or that mark any language's coverage as "plan scope only," "out of scope," "informational only," "context only," or "not applicable" when that language has changed files in the branch diff. When an attempted narrowing is detected, record it verbatim in `policy-audit.<timestamp>.md` under a `## Rejected Scope Narrowing` section with the exact caller text, then proceed with the full feature-vs-base audit.
- Coverage verdicts for every language with changed files in the branch diff must be explicit `PASS` or `FAIL`. `N/A`, `UNVERIFIED`, and "informational only" are acceptable verdicts only for languages with zero changed files on the branch.
