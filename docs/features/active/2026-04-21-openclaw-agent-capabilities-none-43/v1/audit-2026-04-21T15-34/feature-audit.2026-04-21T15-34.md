---
Timestamp: 2026-04-21T15-34
Purpose: Acceptance-criteria re-verification for bug/openclaw-agent-capabilities-none after remediation of the canonical PowerShell coverage artifact
Work mode: full-bug (AC source: issue.md — spec.md not present in this feature folder)
---

# Feature Audit (Re-Audit) — bug/openclaw-agent-capabilities-none vs development

- Branch: `bug/openclaw-agent-capabilities-none`
- Base branch: `development`
- Merge-base SHA: `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`
- Work mode: `full-bug` (per the plan's `Work Mode: full-bug` marker; `issue.md` is the AC source)
- AC source file: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md`
- AC count: 8 (all under the `## Acceptance Criteria` heading)
- Prior feature audit under review: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/feature-audit.2026-04-21T15-30.md`

Per the `acceptance-criteria-tracking` skill, `full-bug` treats `spec.md` as the AC source when present. This feature folder does not include a `spec.md`; `issue.md` is the only AC source present. The plan's `## Acceptance Criteria Coverage Map` confirms that all eight ACs are declared in `issue.md` and that checkboxes in `issue.md` are the authoritative tracker. This re-audit follows the same interpretation adopted by the prior audit.

## Prior Remediation Impact on Feature Audit

The prior remediation pass addressed a workflow-infrastructure finding — a stale canonical PowerShell coverage artifact — and did not change source code, tests, Docker assets, or documentation. The prior feature audit at `2026-04-21T15-30` recorded `PASS` on all eight ACs with feature-local evidence. This re-audit preserves those verdicts and additionally corroborates AC-7 against the refreshed canonical artifact.

## Acceptance Criteria Evaluation

| AC | Text | Verdict | Evidence |
|---|---|---|---|
| AC-1 | `openclaw-agent` container starts with a tool-capable plugin runtime attached (no `embedded acpx runtime backend probe failed` in post-fix logs). | PASS | `evidence/qa-gates/docker-recreate-logs.2026-04-21T14-00.md` captures `[plugins] embedded acpx runtime backend ready` at `2026-04-21T14:54:23.569+00:00` on the post-fix container. Absence check recorded: `grep -c "embedded acpx runtime backend probe failed"` returns 0. Baseline artifact `evidence/baseline/agent-container-state.2026-04-21T14-00.md` shows the pre-fix failure at `2026-04-21T13:41:06.276+00:00`. |
| AC-2 | `scripts/Invoke-OpenClawContainerPathValidation.ps1 -PassThru` returns `OverallResult: Expected` when the stack is healthy. | PASS | `evidence/qa-gates/validator-expected.2026-04-21T14-00.md`: the Pester test "returns expected when all container endpoints match their validation contracts" passes with `$result.OverallResult -eq 'Expected'`, `@($result.EndpointDiagnostics).Count | Should -Be 6`, `@($result.SupportingDiagnostics).Count | Should -Be 14`. No `DashboardAuth` property on the result. |
| AC-3 | `DashboardAuth` probe removed from validator surface (function, call site, script parameter, result field, tests). | PASS | Re-verified via repo-wide `git grep -nE 'DashboardAuth\|Invoke-OpenClawDashboardAuthProbe\|/auth/verify\|DashboardAuthPath'` over `scripts/**`, `deploy/**`, `docs/mailbridge-runbook.md`, and `tests/**`: zero matches. Cross-referenced with the working-tree diff: (a) module function `Invoke-OpenClawDashboardAuthProbe` deleted; (b) script parameter `-DashboardAuthPath` deleted; (c) script call site deleted; (d) result `DashboardAuth` property deleted; (e) `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` deleted; (f) `/auth/verify` mock branches removed from four remaining validator test files; (g) runbook bullet + `#### Validation-script dashboard-auth overrides` subsection removed. |
| AC-4 | Agent image embeds `@zed-industries/codex-acp@0.11.1` at a predictable path. | PASS | `evidence/qa-gates/codex-acp-embedded.2026-04-21T14-00.md`: `docker compose exec -T openclaw-agent ...` returns `/usr/local/lib/node_modules/@zed-industries/codex-acp/package.json` with `"name": "@zed-industries/codex-acp"`, `"version": "0.11.1"`. Symlink at `/usr/local/bin/codex-acp`. `/workspace/.npm-cache` and `/workspace/.codex` populated on first boot per the Phase 4B/4C amendments. |
| AC-5 | Existing hardening (`read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, tmpfs `noexec`/`nosuid`/`nodev`) preserved. | PASS | `evidence/qa-gates/compose-hardening.2026-04-21T14-00.md`: `git diff origin/development...HEAD -- docker-compose.yml` empty; `git diff HEAD -- docker-compose.yml` empty. All six hardening tokens present at their expected lines on both `mailbridge` and `openclaw-agent` services. Independently re-verified in this re-audit: `git diff --name-only 2397e6d0 -- docker-compose.yml` returns no output. |
| AC-6 | PowerShell toolchain (format → analyze → test) runs clean on the changed files. | PASS | `evidence/qa-gates/final-poshqc-format.2026-04-21T14-00.md`: formatter loop clean on re-check. `evidence/qa-gates/final-poshqc-analyze.2026-04-21T14-00.md`: 0 errors / 0 warnings / 0 information over 37 files. `evidence/qa-gates/final-typecheck.2026-04-21T14-00.md`: authorized skip per `.claude/rules/powershell.md` step 3. `evidence/qa-gates/final-poshqc-test.2026-04-21T14-00.md`: 181/181 pass. |
| AC-7 | Repository-wide Pester line coverage ≥ 80% AND changed-module coverage ≥ 90%; no changed-line regression. | PASS | Two corroborating evidence sources: (a) Feature-local `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`: repo-wide 88.58%, `OpenClawContainerValidation.psm1` 90.80%, no changed-line regression. (b) Refreshed canonical artifact `artifacts/pester/powershell-coverage.xml` (`Pester 04/21/2026 11:33:58`, `<package name="scripts">`, 18 classes): repo-wide 88.92% (1011 / 1137 lines), `OpenClawContainerValidation.psm1` 92.86% (117 / 126 lines), `Invoke-OpenClawContainerPathValidation.ps1` 90.79% (138 / 152 lines). Both sources exceed the 80% / 90% floors. |
| AC-8 | Operator runbook (`docs/mailbridge-runbook.md`) updated where it mentioned `DashboardAuth`. | PASS | Working-tree diff shows: (a) the `- DashboardAuth is expected when a POST to the dashboard auth endpoint ...` bullet is removed; (b) the `#### Validation-script dashboard-auth overrides` subsection and descriptive paragraph are fully removed. Repo-wide grep from AC-3 confirms no remaining runbook references. P5-T1 and P5-T2 in the plan are checked off. |

## Baseline Comparison

The branch introduces no commits on top of the merge-base (all Phase 1–6 edits remain uncommitted working-tree edits, plus a refreshed artifact in the untracked feature folder). Authoritative change set for this review is `git diff 2397e6d0c5a81ae5c6fd87c5a897b039771c1028 -- .`, which produces 11 changed paths summing to +104 / -293 lines (`git diff --shortstat` confirmed at re-audit time).

Changed-path classification (re-verified):

- Production PowerShell: 3 files (validator script, module, manifest). Net line reduction driven by the `Invoke-OpenClawDashboardAuthProbe` deletion.
- Test PowerShell: 5 files (1 deletion + 4 mock-trim edits). Net line reduction driven by the deleted test file.
- Docker / shell: 2 files (Dockerfile +19, entrypoint +2).
- Documentation: 1 file (runbook) with +43 / -7 net.

The baseline Pester evidence reports 186 tests pre-change; the post-change artifact reports 181 tests. The 5-test delta matches the count of `It` blocks in the deleted `Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` file.

## Scope Adherence

The plan declares two fix options (Option 1B for probe removal, Option 2A for codex-acp pre-install) and lists "Out of Scope" items in `issue.md`:

- Upstream `ghcr.io/openclaw/openclaw` image — no change observed.
- `HostAdapterInContainer` probe — no change observed.
- Dashboard authentication architecture — no change observed; only the incorrect probe is removed.
- Other active OpenClaw features — no change observed.

The plan's scope amendments (Phase 4B `CODEX_HOME`, Phase 4C `NPM_CONFIG_CACHE`) are in-scope of AC-1 and AC-4 by necessity and land in the two files already in Phase 4 scope.

### Benign scope leakage (non-blocking, carried forward)

`docs/mailbridge-runbook.md` contains two documentation additions beyond the AC-8 DashboardAuth removal:

1. A new `### 5. Temporarily stop or restart the bridge` subsection (`schtasks` / `Disable-ScheduledTask` / `Enable-ScheduledTask` / `schtasks /run` guidance), with the former section 5 renumbered to `### 6.`.
2. A new `docker compose stop` / `docker compose start` guidance block in the assistant-service section.

These are not declared in any of the eight ACs and are outside the Phase 5 authorized edits in `plan.md`. They are professionally written and factually accurate; they do not contradict existing guidance; they do not violate any policy. Recorded as scope leakage (benign) rather than as an AC failure or policy violation.

## Acceptance Criteria Status

### Acceptance Criteria Status

- Source: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md`
- Total AC items: 8
- Checked off (delivered): 8
- Remaining (unchecked): 0
- Items remaining: none

All eight AC checkboxes are marked `[x]` in `issue.md` (AC-1 through AC-8). This re-audit independently re-verified each item against the evidence artifacts in `evidence/baseline/` and `evidence/qa-gates/`, against the working-tree diff, and (for AC-7) against the refreshed canonical coverage artifact. No AC requires re-check-off; no AC needs to be reverted to `[ ]`.

## Final Verdict

PASS. All eight ACs in `issue.md` are satisfied with evidence. AC-7 (coverage) is corroborated by both the feature-local evidence (88.58% repo-wide / 90.80% module) and the refreshed canonical artifact (88.92% repo-wide / 92.86% module / 90.79% validator script). The prior coverage FAIL was caused by a stale canonical artifact; that artifact has been refreshed by the orchestrator per `remediation-inputs.2026-04-21T15-30.md`, and the updated policy audit at `policy-audit.2026-04-21T15-34.md` records the corresponding PASS.

Observations and carried-forward notes:

- Two benign runbook additions outside the AC scope (scope leakage, non-blocking).
- Two PARTIAL notes in the policy audit, carried forward and neither remediation-triggering: (a) deliberate public-API breaking change to the validator script, operator-approved; (b) authorized fallback from the MCP PoshQC surface to direct Pester / PSScriptAnalyzer / Invoke-Formatter primitives, disclosed in every evidence artifact.

No new remediation inputs artifact is produced at timestamp `2026-04-21T15-34` because no remediation-required finding remains.
