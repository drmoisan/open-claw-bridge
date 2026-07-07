# Remediation Inputs — modified-workflow-needs-green-run (cycle 1)

- Timestamp: 2026-07-07T05-30
- Feature: F16 azure-bicep-iac (issue #125)
- Branch: feature/azure-bicep-iac-125
- Head SHA: b3a252b
- Base: epic/openclaw-vision-integration @ merge-base 7a29286b687f00c6a10809efa41102c78f009c36
- Source audit: `docs/features/active/2026-07-07-azure-bicep-iac-125/policy-audit.2026-07-07T05-30.md` (Section 8, Section 10), `docs/features/active/2026-07-07-azure-bicep-iac-125/feature-audit.2026-07-07T05-30.md` (Summary)

## Blocking finding

- Severity: Blocking
- Rule: `modified-workflow-needs-green-run` (`.claude/skills/feature-review-workflow/SKILL.md`)
- Trigger: the branch diff modifies `.github/workflows/ci.yml` (adds a `bicep-validate` job) and adds `.github/workflows/_bicep-validate.yml` — both paths match the rule's trigger glob `.github/workflows/**`.
- Gap: no workflow run whose head SHA matches the branch head `b3a252b` exists with a `success` (or any) conclusion.

## Verification performed (evidence of the gap, not of a fix)

```
gh run list --branch feature/azure-bicep-iac-125 --limit 10
```
returned zero rows.

```
gh run list --limit 100 | grep -i "azure-bicep"
```
returned zero rows (exit 1 / no match) against the 100 most recent runs across all branches in this repository.

`gh auth status` confirms `gh` is authenticated (`drmoisan`, `repo`/`workflow` scopes) in this environment, so the absence is a genuine data fact, not a tooling gap.

## Enumerated fix list

1. **Dispatch the CI gate against the branch head (or the eventual PR head).**
   - File/target: `.github/workflows/ci.yml` (which now includes the `bicep-validate` job referencing `.github/workflows/_bicep-validate.yml`).
   - Expected behavior: `gh workflow run ci.yml --ref feature/azure-bicep-iac-125` (or a dispatch against the final PR head, if the branch advances before merge) triggers a run that includes the new `bicep-validate` job.
   - Verification command: `gh run list --branch feature/azure-bicep-iac-125 --limit 5` after dispatch; confirm the newest run's head SHA matches the target commit and its `bicep-validate` job (and the run overall) conclude `success`.
   - Acceptable alternative: a direct `gh workflow run _bicep-validate.yml --ref feature/azure-bicep-iac-125` dispatch also satisfies the rule, since `_bicep-validate.yml` itself declares `workflow_dispatch` — the rule explicitly permits a green `workflow_dispatch` run against the branch head, not only a PR-context run (mitigating the chicken-and-egg case where a feature must land its own CI gate before that gate can run in PR context).
2. **Capture the green-run evidence in this feature's canonical evidence tree.**
   - Target: `docs/features/active/2026-07-07-azure-bicep-iac-125/evidence/qa-gates/ci-green-run.<ts>.md` (new evidence artifact; canonical path per the Evidence Location Invariant — do not write to any `artifacts/` sub-path).
   - Expected content: `Timestamp:`, `Command:` (the `gh workflow run`/`gh run list` invocations used), `EXIT_CODE:`, `Output Summary:` stating the run ID, run URL, head SHA (must equal the dispatched commit), and `success` conclusion for both the overall run and the `bicep-validate` job specifically.
3. **If the dispatched run fails**, diagnose before re-dispatching:
   - Confirm the failure is not a defect in `deploy/azure/main.bicep` or its modules (the reviewer's structural review found no defect, but only a real `bicep build` on the runner exercises this authoritatively for the first time).
   - Confirm the failure is not a defect in `scripts/Test-OpenClawBicepParameterSecrets.ps1` against the real `deploy/azure/parameters/` directory contents (the script was only tested against mocked filesystem state, never a real directory scan, prior to this dispatch).
   - If the failure is environmental/transient (e.g. a runner fault unrelated to this branch's diff, per the `#120` precedent for diagnosing transient CI faults), a single re-dispatch is an acceptable first probe before authoring a code-level remediation plan.
   - If the failure reveals a real defect in the Bicep templates or the PowerShell script, route to a full remediation cycle (`atomic-planner` -> `atomic-executor` -> `feature-review`) per `.claude/skills/remediation-handoff-atomic-planner/SKILL.md`, scoped strictly to the failing gate — do not expand scope beyond what the failure demonstrates.

## Do-not-do list

- Do not mark this finding closed based on the structural-review evidence already on file (Phases 1-4, consolidated Phase 6) — that evidence satisfies AC-4's structural-validation intent but does not substitute for the distinct, CI-gate-level `modified-workflow-needs-green-run` requirement.
- Do not fabricate or assume a green-run result; the only acceptable evidence is a `gh run list`/`gh run view` output showing a `success` conclusion at a head SHA matching the dispatched commit.
- Do not expand this remediation cycle's scope to unrelated code changes. If the dispatched run is green on the first attempt, this cycle closes with no code change required.
- Do not weaken or remove the `bicep-validate` job from `ci.yml`, and do not alter `_bicep-validate.yml`'s steps, as a way of avoiding the gate — the rule exists specifically to require this evidence for workflow-file changes.
- Do not skip capturing the evidence artifact even if the dispatch succeeds on the first attempt; the rule requires the evidence to be present, not merely for the run to have happened.

## Pointer to audit artifacts

- `docs/features/active/2026-07-07-azure-bicep-iac-125/policy-audit.2026-07-07T05-30.md` (Section 8 "Gaps and Exceptions", Section 10 "Compliance Verdict")
- `docs/features/active/2026-07-07-azure-bicep-iac-125/code-review.2026-07-07T05-30.md` (Verdict: no code-quality blocker; this finding is CI-gate-level, not a code defect)
- `docs/features/active/2026-07-07-azure-bicep-iac-125/feature-audit.2026-07-07T05-30.md` (Summary: all 6 acceptance criteria PASS; this finding is outside the AC set)
