---
Timestamp: 2026-04-21T15-30
Purpose: Acceptance-criteria verification for bug/openclaw-agent-capabilities-none vs development
Work mode: full-bug (AC source: issue.md only per acceptance-criteria-tracking skill)
---

# Feature Audit — bug/openclaw-agent-capabilities-none vs development

- Branch: `bug/openclaw-agent-capabilities-none`
- Base branch: `development`
- Merge-base SHA: `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`
- Work mode: `full-bug` (per `issue.md`'s `- Work Mode: full-bug` marker in `plan.2026-04-21T14-00.md`)
- AC source file: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md`
- AC count: 8 (all under the `## Acceptance Criteria` heading in `issue.md`)

Per the acceptance-criteria-tracking skill, `full-bug` treats `spec.md` as the AC source when present. This feature folder does not include a `spec.md`; `issue.md` is the only AC source present. The plan's `## Acceptance Criteria Coverage Map` confirms that all eight ACs are declared in `issue.md` and that checkboxes in `issue.md` are the authoritative tracker. That matches the "full-bug with spec.md absent → fall back to issue.md AC section" interpretation adopted by the plan; this audit follows the same interpretation.

## Acceptance Criteria Evaluation

| AC | Text | Verdict | Evidence |
|---|---|---|---|
| AC-1 | `openclaw-agent` container starts with a tool-capable plugin runtime attached (no `embedded acpx runtime backend probe failed` in post-fix logs). | PASS | `evidence/qa-gates/docker-recreate-logs.2026-04-21T14-00.md` captures `[plugins] embedded acpx runtime backend ready` at `2026-04-21T14:54:23.569+00:00` on the post-fix container. Absence check recorded: `grep -c "embedded acpx runtime backend probe failed" /tmp/agent-logs-post.log` → 0. Baseline artifact `evidence/baseline/agent-container-state.2026-04-21T14-00.md` shows the pre-fix failure line at `2026-04-21T13:41:06.276+00:00`, so pre- and post-fix state are cleanly contrasted. |
| AC-2 | `scripts/Invoke-OpenClawContainerPathValidation.ps1 -PassThru` returns `OverallResult: Expected` when the stack is healthy. | PASS | `evidence/qa-gates/validator-expected.2026-04-21T14-00.md`: filtered Pester run executes the `returns expected when all container endpoints match their validation contracts` test and confirms `$result.OverallResult | Should -Be 'Expected'`, `@($result.EndpointDiagnostics).Count | Should -Be 6`, `@($result.SupportingDiagnostics).Count | Should -Be 14`. The test passes in 369 ms with EXIT_CODE 0. No `DashboardAuth` property on `$result`. |
| AC-3 | `DashboardAuth` probe removed from validator surface (function, call site, script parameter, result field, tests). | PASS | `evidence/qa-gates/dashboard-auth-grep.2026-04-21T14-00.md` — zero production-code matches across the repo (after excluding the archived 2026-04-20 audit folder, the active feature folder itself, research artifacts, and build outputs) for `DashboardAuth`, `Invoke-OpenClawDashboardAuthProbe`, `/auth/verify`, `DashboardAuthPath`. Cross-referenced with the working-tree diff: (a) module function `Invoke-OpenClawDashboardAuthProbe` deleted; (b) script parameter `-DashboardAuthPath` deleted; (c) script call site `$dashboardAuth = Invoke-OpenClawDashboardAuthProbe ...` deleted; (d) result object `DashboardAuth` property deleted; (e) `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` deleted; (f) mock branches for `/auth/verify` removed from all four remaining validator test files; (g) runbook bullet and the `#### Validation-script dashboard-auth overrides` subsection removed from `docs/mailbridge-runbook.md`. |
| AC-4 | Agent image embeds `@zed-industries/codex-acp@0.11.1` at a predictable path. | PASS | `evidence/qa-gates/codex-acp-embedded.2026-04-21T14-00.md`: `docker compose exec -T openclaw-agent sh -c 'ls /usr/local/lib/node_modules/@zed-industries/codex-acp/package.json && cat ... | grep -E "version|name"'` returns the path plus `"name": "@zed-industries/codex-acp"`, `"version": "0.11.1"`. Binary symlink at `/usr/local/bin/codex-acp`. Supporting evidence shows `/workspace/.npm-cache` and `/workspace/.codex` populated on first boot, consistent with the amendments. |
| AC-5 | Existing hardening (`read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, tmpfs `noexec`/`nosuid`/`nodev`) preserved. | PASS | `evidence/qa-gates/compose-hardening.2026-04-21T14-00.md`: `git diff origin/development...HEAD -- docker-compose.yml` empty; `git diff HEAD -- docker-compose.yml` empty. All six hardening tokens present at their expected lines on both the `mailbridge` and `openclaw-agent` services. Additionally cross-checked via `git diff --name-only 2397e6d0 -- .`: `docker-compose.yml` is not in the diff list. |
| AC-6 | PowerShell toolchain (format → analyze → test) runs clean on the changed files. | PASS | `evidence/qa-gates/final-poshqc-format.2026-04-21T14-00.md`: formatter loop terminated clean on pass 3 re-check. `evidence/qa-gates/final-poshqc-analyze.2026-04-21T14-00.md`: 0 errors, 0 warnings, 0 information across 37 files; zero delta vs the `evidence/baseline/poshqc-analyze.2026-04-21T14-00.md` snapshot. `evidence/qa-gates/final-typecheck.2026-04-21T14-00.md`: authorized skip per `.claude/rules/powershell.md` step 3. `evidence/qa-gates/final-poshqc-test.2026-04-21T14-00.md`: 181/181 pass, 0 fail, 0 skip. |
| AC-7 | Repository-wide Pester line coverage ≥ 80% AND changed-module coverage ≥ 90%; no changed-line regression. | PASS | `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`: repo-wide 88.58% (>= 80% with 8.58 pp margin). `OpenClawContainerValidation.psm1` module coverage 90.80% (>= 90% with 0.80 pp margin). Baseline → post-change deltas: repo 89.02% → 88.58% (-0.44 pp), module 93.78% → 90.80% (-2.98 pp). Both drops are fully explained by the removal of `Invoke-OpenClawDashboardAuthProbe` and its dedicated test file (numerator and denominator moved together). No newly introduced uncovered line on the three edited production files. |
| AC-8 | Operator runbook (`docs/mailbridge-runbook.md`) updated where it mentioned `DashboardAuth`. | PASS | Working-tree diff shows: (a) the `- DashboardAuth is expected when a POST to the dashboard auth endpoint ...` bullet is removed from the "Expected behavior" section near line 472; (b) the `#### Validation-script dashboard-auth overrides` subsection and its descriptive paragraph are fully removed. The repo-wide grep from AC-3 confirms no remaining references in the runbook. P5-T1 and P5-T2 tasks in the plan are checked off. |

## Baseline Comparison

The branch introduces no commits (all Phase 1-6 edits are uncommitted in the working tree). The authoritative change set for this review is therefore `git diff 2397e6d0c5a81ae5c6fd87c5a897b039771c1028 -- .`, which produces 11 changed paths (including one deletion) summing to +104 / -293 lines.

Changed path classification:
- Production PowerShell: 3 files (validator script, module, manifest). Net reduction of ~90 lines driven by the `Invoke-OpenClawDashboardAuthProbe` deletion.
- Test PowerShell: 5 files (1 deletion + 4 mock-trim edits). Net reduction of ~180 lines driven by the DashboardAuth test file deletion.
- Docker/shell: 2 files (Dockerfile +19 lines, entrypoint +2 lines).
- Documentation: 1 file (`docs/mailbridge-runbook.md`) with ~50 lines net addition — two DashboardAuth removals plus two unrelated runbook expansions.

The baseline PoshQC evidence (`evidence/baseline/poshqc-*.2026-04-21T14-00.md`) was captured via a `git stash push -u` / replay / `git stash pop` sequence against the same merge-base SHA, so baseline and post-change metrics are apples-to-apples. The baseline artifact for Pester reports 186 tests pass (no failures); the post-change artifact reports 181 tests pass. The 5-test delta is the exact size of the deleted `Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` file.

## Scope Adherence

The plan declares two fix options (Option 1B, Option 2A) and explicitly lists "Out of Scope" items in `issue.md`:
- Upstream `ghcr.io/openclaw/openclaw` image — no change observed.
- `HostAdapterInContainer` probe — no change observed.
- Dashboard authentication architecture — no change observed; only the incorrect probe against it is removed.
- Other active OpenClaw features — no change observed.

The plan's scope amendments (Phase 4B for `CODEX_HOME`, Phase 4C for `NPM_CONFIG_CACHE`) are in-scope of AC-1 and AC-4 by necessity: without them, AC-1 cannot be satisfied even with the codex-acp package pre-installed. Both amendments land in the two files already in Phase 4 scope (`deploy/docker/openclaw-agent.Dockerfile`, `deploy/docker/openclaw-agent-entrypoint.sh`) and do not touch `docker-compose.yml`. The amendments are documented in `plan.md` and in the "Scope amendments during execution" section of `issue.md`.

### Benign scope leakage (non-blocking)

`docs/mailbridge-runbook.md` contains two documentation additions beyond the AC-8 DashboardAuth removal:

1. A new `### 5. Temporarily stop or restart the bridge` subsection (schtasks / Disable-ScheduledTask / Enable-ScheduledTask guidance) with a renumbering of the former section 5 to section 6.
2. A new `docker compose stop` / `docker compose start` guidance block appended to the assistant-service section.

These additions are not declared in any of the eight ACs in `issue.md` and are not explicitly authorized by Phase 5 of `plan.md` (which scoped only the two DashboardAuth removals on the runbook). They are benign documentation additions — factually accurate, idiomatic, professionally written, and do not contradict existing guidance — but strictly speaking they exceed the plan scope. This is noted as an observation rather than a failure. No AC fails because of these additions; no policy is violated; no remediation is triggered. A cleanup option for the operator is to either (a) accept the additions as incidental improvements or (b) stage the additions to a separate small edit for reviewer clarity. Either choice is consistent with policy.

## Acceptance Criteria Status

### Acceptance Criteria Status

- Source: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md`
- Total AC items: 8
- Checked off (delivered): 8
- Remaining (unchecked): 0
- Items remaining: none

All eight checkboxes in the `## Acceptance Criteria` section of `issue.md` were already marked `[x]` by the executor prior to this review. This audit independently verified each item against the evidence artifacts in `evidence/baseline/` and `evidence/qa-gates/` and against the working-tree diff. No AC requires re-check-off; no AC needs to be reverted to `[ ]`.

## Final Verdict

The feature branch satisfies all eight acceptance criteria declared in `issue.md`, each verified against feature-local evidence artifacts under `evidence/baseline/` and `evidence/qa-gates/`. AC-7 (coverage) is recorded PASS here because the feature-local post-change coverage artifacts (`TestResults/coverage-post.xml` and `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`) measure repo-wide coverage at 88.58% and `OpenClawContainerValidation.psm1` at 90.80%, with no changed-line regression.

The policy audit (`policy-audit.2026-04-21T15-30.md`) separately records FAIL on coverage verification against the canonical artifact `artifacts/pester/powershell-coverage.xml`. That canonical artifact is stale — produced by an earlier Pester run scoped to `.claude/hooks/*.ps1` and reporting 0 / 256 lines covered (0.00%). The canonical-artifact FAIL is a workflow-infrastructure issue (stale artifact at the hook-parsed path), not an AC deficiency in the code changes. The remediation action is to refresh the canonical artifact; no source-code change is required.

Observations and partial notes:

- Two runbook additions outside the AC scope (benign, non-blocking).
- Two PARTIAL notes in the policy audit: (a) intentional public-API breaking change to the validator script, authorized by operator; (b) authorized fallback from the MCP PoshQC surface to direct PowerShell primitive invocation, disclosed in every evidence artifact.

Remediation artifact: `remediation-inputs.2026-04-21T15-30.md` has been written alongside this audit. It identifies two remediation-required findings (repo-wide coverage FAIL and per-changed-file coverage FAIL, both against the canonical artifact) with a single corrective-action sequence: refresh `artifacts/pester/powershell-coverage.xml` by running Pester with `CodeCoverage.Path` populated from `scripts/**`, overwriting the stale file, then re-run the coverage-verification hook.
