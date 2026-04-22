---
Timestamp: 2026-04-21T15-30
Purpose: Remediation inputs for policy-audit FAIL on canonical PowerShell coverage artifact
Audit scope: full branch diff, merge-base 2397e6d0c5a81ae5c6fd87c5a897b039771c1028
---

# Remediation Inputs — bug/openclaw-agent-capabilities-none vs development

- Branch: `bug/openclaw-agent-capabilities-none`
- Base branch: `development`
- Merge-base SHA: `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`
- Source policy audit: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/policy-audit.2026-04-21T15-30.md`

## Summary

The feature-review-workflow coverage gate FAILs against the canonical PowerShell coverage artifact `artifacts/pester/powershell-coverage.xml`. The canonical artifact reports 0 covered lines across 256 analyzed lines (0.00% repo-wide) and its scope is limited to `.claude/hooks/*.ps1` — it does not include any file under `scripts/`, `scripts/powershell/modules/`, or `tests/`. The artifact is dated `Pester (04/21/2026 08:24:04)`, which predates this feature's Phase 6 Pester run at `2026-04-21T15:09:00Z`.

The code changes on the branch are not at fault. The feature-local post-change coverage evidence (`TestResults/coverage-post.xml`, consolidated in `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`) shows repo-wide 88.58% and `OpenClawContainerValidation.psm1` at 90.80%, both above their respective thresholds, with no changed-line regression. The feature-review-workflow hook nonetheless verifies against the canonical artifact path, which must be refreshed.

## Remediation-Required Findings

### Finding 1 — PowerShell repo-wide coverage FAIL in canonical artifact

- Canonical artifact: `artifacts/pester/powershell-coverage.xml`
- Reported coverage: 0 / 256 lines covered (0.00%)
- Threshold: >= 80% repo-wide per `.claude/rules/general-unit-test.md` and per the feature-review-workflow coverage verification procedure.
- Root cause: the canonical artifact was produced by a prior Pester run whose `CodeCoverage.Path` was scoped to `.claude/hooks/*.ps1` only. The artifact was not refreshed when the feature branch's Phase 6 Pester run completed against the full `scripts/**` tree.
- Corrective action: run Pester with coverage enabled against the canonical production scope and write the resulting JaCoCo XML to `artifacts/pester/powershell-coverage.xml`.

### Finding 2 — PowerShell changed-file coverage FAIL in canonical artifact (x3)

- Canonical artifact: `artifacts/pester/powershell-coverage.xml`
- Changed production paths not covered by the canonical artifact:
  1. `scripts/Invoke-OpenClawContainerPathValidation.ps1`
  2. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`
  3. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1`
- Threshold per path: >= 80% line coverage with no baseline regression; the `.psm1` additionally carries the >= 90% "changed module" threshold.
- Root cause: same as Finding 1 — the canonical artifact's scope excludes the `scripts/` tree entirely.
- Corrective action: same as Finding 1. Once the canonical artifact is refreshed to include the `scripts/` tree, the changed-file gates can be re-evaluated. The feature-local evidence in `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md` gives high confidence that each changed-file gate will PASS after the refresh (validator script at 100% of retained lines, module at 90.80%, manifest with zero executable statements).

## Corrective-Action Sequence

1. Invoke the repository's canonical PowerShell coverage pipeline. The project-standard path per `.claude/rules/powershell.md` step 4 is the MCP PoshQC test surface `mcp__drmCopilotExtension__run_poshqc_test`. When that surface is unreachable, the authorized fallback (already used to produce `evidence/qa-gates/final-poshqc-test.2026-04-21T14-00.md`) is direct `Invoke-Pester` with `CodeCoverage.Enabled = $true` and `CodeCoverage.Path` populated from every `*.ps1`/`*.psm1` under `scripts/` (recursive).
2. Emit the JaCoCo XML output to `artifacts/pester/powershell-coverage.xml` (overwriting the stale file). The skill contract reads this path literally; writing to any other path leaves the contract unsatisfied.
3. Re-run the feature-review coverage verification hook. Expected outcome after refresh: repo-wide coverage reports 88.58% (>= 80%), the three changed paths appear in the `<sourcefile>` entries with line-coverage percentages that meet their per-file thresholds, and the hook allows the review to pass.
4. Re-open the policy audit and update the coverage verdicts from FAIL to PASS, referencing the refreshed artifact. No source-code changes are required.

## Artifact References

- Policy audit (this remediation is derived from): `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/policy-audit.2026-04-21T15-30.md`
- Feature-local post-change coverage (context only — shows the branch is not at fault): `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`
- Feature-local Phase 6 Pester evidence: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/final-poshqc-test.2026-04-21T14-00.md`
- Feature-local baseline Pester evidence: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/baseline/poshqc-test.2026-04-21T14-00.md`
- Stale canonical artifact (target of remediation): `artifacts/pester/powershell-coverage.xml` (Pester header timestamp `04/21/2026 08:24:04`, scope `.claude/hooks/*.ps1` only, totals `<counter type="LINE" missed="256" covered="0"/>`).
- Current post-change feature-local JaCoCo (source data for the refreshed artifact): `TestResults/coverage-post.xml` (1287 / 1453 commands covered → 88.58% repo-wide).

## Languages with Zero Findings

- TypeScript: zero changed files on branch; canonical artifact `coverage/lcov.info` not consulted for this branch.
- Python: zero changed files on branch; canonical artifact `artifacts/python/lcov.info` not consulted for this branch.
- C#: zero changed files on branch; canonical artifact `artifacts/csharp/coverage.xml` not consulted for this branch.

No remediation is required for TypeScript, Python, or C#.
