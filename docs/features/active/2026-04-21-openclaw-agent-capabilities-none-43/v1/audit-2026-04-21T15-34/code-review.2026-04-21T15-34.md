---
Timestamp: 2026-04-21T15-34
Purpose: Code-quality re-review of bug/openclaw-agent-capabilities-none vs development after remediation of the canonical PowerShell coverage artifact
Audit scope: full branch diff, merge-base 2397e6d0c5a81ae5c6fd87c5a897b039771c1028
---

# Code Review (Re-Review) — bug/openclaw-agent-capabilities-none vs development

- Branch: `bug/openclaw-agent-capabilities-none`
- Base branch: `development`
- Merge-base SHA: `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`
- Prior review under review: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/code-review.2026-04-21T15-30.md`
- Changed-file surface: 3 PowerShell production + 5 PowerShell test (1 deletion) + 2 Docker/shell + 1 documentation = 11 paths.

## Re-Review Scope

The prior remediation pass addressed a workflow-infrastructure finding (stale canonical coverage artifact) and did not modify source code. This re-review therefore re-verifies that:

1. No source code has regressed or drifted since the prior code-review.
2. The refreshed canonical coverage artifact corroborates the test-quality conclusions of the prior review.
3. All observations and non-blocking flags remain accurate against the current working-tree state.

The branch diff produced by `git diff 2397e6d0c5a81ae5c6fd87c5a897b039771c1028 -- .` at this timestamp is byte-identical to the diff inspected at `2026-04-21T15-30` (confirmed via file-level inspection of each changed path). Per-file conclusions are carried forward with spot-check verification.

## 1. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`

### Status

Unchanged from prior review. Re-verified diff:

- `Invoke-OpenClawDashboardAuthProbe` function body is absent at end-of-file.
- `'Invoke-OpenClawDashboardAuthProbe'` is absent from the `Export-ModuleMember -Function @(...)` array.
- Formatter-driven reflow of 8 constructs into the `}\n<ws>else {` form.
- Export list in `.psm1` matches manifest `.psd1` export list (both list 11 functions).

### Re-review notes

- The refreshed canonical coverage artifact reports `<counter type="LINE" missed="9" covered="117"/>` on this file (class entry at line 1668 of the XML). Line coverage 92.86% — above the 90% changed-module threshold. The prior code review noted that the module's exported surface is exercised uniformly; the coverage artifact corroborates this.
- No blocking issues. Minor observation from the prior review about the top-of-file docblock saying "the four new probes introduced by issue #38" remains a factual historical statement; not remediation-required.

## 2. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1`

### Status

Unchanged from prior review. Re-verified diff: `'Invoke-OpenClawDashboardAuthProbe'` removed from `FunctionsToExport`; trailing comma trimmed from the preceding entry.

### Re-review notes

- PowerShell module manifests are declarative hashtables with no executable control-flow. The refreshed canonical coverage artifact (JaCoCo-for-Pester) correctly does not emit a `<class>` entry for this file. This is expected, not a gap.
- No issues.

## 3. `scripts/Invoke-OpenClawContainerPathValidation.ps1`

### Status

Unchanged from prior review. Re-verified diff:

- `.PARAMETER DashboardAuthPath` docblock removed.
- `[string]$DashboardAuthPath = '/auth/verify'` parameter removed from `param(...)`.
- `$dashboardAuth = Invoke-OpenClawDashboardAuthProbe ...` call site removed.
- `$dashboardAuth` removed from `$endpointDiagnostics = @(...)`.
- `DashboardAuth = $dashboardAuth` removed from the result `[pscustomobject]`.
- `.DESCRIPTION` phrase updated from "readiness (/readyz) and dashboard auth on openclaw-agent" to "readiness (/readyz) on openclaw-agent".
- Formatter reflow of 2 `} else {` constructs.

### Re-review notes

- The refreshed canonical coverage artifact reports `<counter type="LINE" missed="14" covered="138"/>` on this file (class entry at line 241 of the XML). Line coverage 90.79% — above the 80% modified-file threshold by 10.79 pp. The 14 missed lines correspond to pre-existing early-return branches in `Get-OpenClawContainerInspection` and `Get-OpenClawCoreBaseUrl` and the environment-variable-driven defaulting in the top-level script, not newly introduced uncovered code.
- Post-edit line count: 298 (below 500).
- No orphan references to `$DashboardAuthPath` or `$dashboardAuth` anywhere in the file.
- No blocking issues.

## 4. `deploy/docker/openclaw-agent.Dockerfile`

### Status

Unchanged from prior review. Re-verified diff:

```dockerfile
USER root
RUN npm install -g @zed-industries/codex-acp@0.11.1 \
    && npm cache clean --force \
    && rm -rf /root/.npm /tmp/.npm /tmp/npm-* 2>/dev/null || true
USER 1654

ENV CODEX_HOME=/workspace/.codex
ENV NPM_CONFIG_CACHE=/workspace/.npm-cache
```

### Re-review notes

- Dockerfile is not a PowerShell file, so the canonical PowerShell coverage artifact does not apply. Correct exclusion.
- Pinned package version (`@0.11.1`), single-`RUN` install-and-clean pattern, tight `USER root` → `USER 1654` scope, and comment blocks documenting root cause all remain accurate.
- No blocking issues.

## 5. `deploy/docker/openclaw-agent-entrypoint.sh`

### Status

Unchanged from prior review. Re-verified diff:

```sh
mkdir -p "${CODEX_HOME:-$workspace_dir/.codex}"
mkdir -p "${NPM_CONFIG_CACHE:-$workspace_dir/.npm-cache}"
```

### Re-review notes

- `set -eu` still in effect; `mkdir -p` idempotent; fallback paths match the Dockerfile `ENV` defaults.
- No blocking issues.

## 6. `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`

### Status

Unchanged from prior review.

### Re-review notes

- Count arithmetic (`EndpointDiagnostics` 7 → 6; `SupportingDiagnostics` 15 → 14; failing-probe lower bound 6 → 5) remains consistent and verified by the feature-local `validator-expected.2026-04-21T14-00.md` artifact.
- 362 lines post-edit (below 500).
- All `/auth/verify` mock branches removed. No orphaned stubs.
- Tests are exercised in the refreshed canonical coverage run (the class entry for `scripts/Invoke-OpenClawContainerPathValidation` shows 138 covered lines, which requires the validator-script tests to run against the real code). No blocking issues.

## 7–9. `tests/scripts/Invoke-OpenClawContainerPathValidation.{HostAdapter,Readyz,TokenPresence}.Tests.ps1`

### Status

Unchanged from prior review. Each file had `/auth/verify` mock branches removed (3 occurrences, 3 occurrences, 1 occurrence respectively). No assertion text touched.

### Re-review notes

- Purely mechanical mock-branch removals. No blocking issues.

## 10. `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1`

### Status

Deleted. Confirmed via `ls` returning "No such file or directory" on the expected path.

### Re-review notes

- Deleting the test file is correct given the production surface was removed. Keeping it would produce false failures for a deliberately removed probe.
- No orphan fixtures imports or leftover helpers.

## 11. `docs/mailbridge-runbook.md`

### Status

Unchanged from prior review. Diff stat: +43 / -7, 50 lines changed total.

### Re-review notes

The runbook diff contains:

1. The `DashboardAuth` "Expected behavior" bullet is removed (AC-8).
2. The `#### Validation-script dashboard-auth overrides` subsection is fully removed (AC-8).
3. A new `### 5. Temporarily stop or restart the bridge` subsection is added with the prior section 5 renumbered to `### 6.`.
4. A new `docker compose stop` / `docker compose start` guidance block is appended to the assistant-service section.

Items 1 and 2 are AC-8 deliverables. Items 3 and 4 are documentation additions outside the AC scope and outside the Phase 5 authorized edits in `plan.md`. They are professionally written, factually accurate, and do not contradict any existing guidance. They are recorded in the feature audit as benign scope leakage. No code-review blocker.

## Cross-Cutting Themes

### Error-handling consistency

Validator now has fewer early-return stub paths (the DashboardAuth probe's "no gateway token" short-circuit branch is gone). Every remaining probe returns a structured `[pscustomobject]` with explicit `IsExpected`, `Summary`, and `HttpStatusCode` fields. The module's three remaining `catch` blocks propagate error context; the JSON-parse `catch` in `ConvertFrom-OpenClawJsonContent` returns `$null` as documented in the function synopsis.

### Test maintainability

The duplicated `Invoke-WebRequest` mock switch statement (one per `It` block) is a pre-existing pattern in the test file. The DashboardAuth removal reduced the duplication by one branch per block but did not introduce new duplication. A future-pass refactor could extract a shared URI-map fixture, but that is outside this bug fix's scope.

### Image-layer hygiene

The Dockerfile addition combines install and cache-clean in a single `RUN` so the intermediate `/root/.npm` directory is not preserved in any image layer. This is the correct pattern for multi-stage or single-stage `FROM`-based builds.

### Security hardening

`docker-compose.yml` is not in the branch diff (verified). All compose-level hardening tokens (`read_only: true`, `cap_drop`, `no-new-privileges`, `noexec`, `nosuid`, `nodev`) are preserved at the same lines. The Dockerfile change does not weaken any compose-level constraint; it shifts writable state onto the pre-existing `/workspace` named volume, which is already writable by design.

### Canonical coverage artifact corroboration

The refreshed canonical PowerShell coverage artifact (produced after the prior remediation pass) reports per-file coverage that is consistent with the feature-local post-change coverage evidence. The module at 92.86% and the validator script at 90.79% both clear their respective thresholds. This corroborates the test-quality conclusions of the prior review without requiring any test-code change.

## Overall Code-Quality Verdict

PASS.

No blocking issues. No source-code change is required. Minor observations from the prior review (documentation staleness in the top-of-module / top-of-script docblocks; scope-leaking runbook additions) remain factually accurate and continue to be recorded for visibility rather than as remediation triggers.

The prior-remediation corrective action (refresh canonical coverage artifact) produced the expected outcome: the refreshed artifact now covers the `scripts/**` tree, includes both changed production paths, and measures coverage above all applicable thresholds.
