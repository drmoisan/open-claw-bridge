# Feature (Acceptance Criteria) Audit — Issue #142 (installer-docker-images-not-bundled)

- Reviewed: 2026-07-10T20-01
- Work mode: `full-bug`
- AC source (authoritative, per the persisted `- Work Mode: full-bug` marker in `issue.md`): `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/spec.md`, `## Acceptance Criteria` (12 checkbox items AC1–AC12). `user-story.md` is intentionally absent in full-bug mode.

## Scope and Baseline

- Base branch: `main` (merge-base `ca53297a558cd0fd8d3f13e8994d2637bef6740a`)
- Head: `bug/installer-docker-images-not-bundled-142` @ `79c6a3b21fbd9e59344b33e1b8b99b2295be790d`
- Diff scope: `git diff --stat ca53297..79c6a3b` — 40 files changed (2,163 insertions / 48 deletions), of which 12 are PowerShell production/test files (5 production: `scripts/Publish.Docker.psm1` new, `scripts/Install.Docker.psm1` new, `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, `scripts/Install.ps1` modified; 7 test files) and the remainder are Markdown feature-folder documentation/evidence artifacts and agent-memory notes.
- PR-context artifacts (`artifacts/pr_context.summary.txt`, `artifacts/pr_context.appendix.txt`) verified fresh (head SHA matches exactly) and used as the primary/appendix evidence sources.

## Acceptance Criteria Inventory

`spec.md` `## Acceptance Criteria` carries 12 checkbox items, all already marked `- [x]` by the executor at the start of this review (criterion text unmodified, cross-referenced against `evidence/qa-gates/ac-summary.2026-07-10T19-10.md`):

| # | Criterion (abbreviated) |
|---|---|
| AC1 | Image tar produced: `Publish.ps1` invokes `docker build` x2 and a single `docker save` writing `<BundleRoot>/docker/openclaw-images.tar` naming all four refs |
| AC2 | Build args mirror compose (core: `-f ... --target runtime --build-arg BUILD_CONFIGURATION=...`; agent: `-f ... --build-arg OPENCLAW_AGENT_IMAGE=...`; repo root as context) + cross-reference comment |
| AC3 | Bundle compose transformed: no `build:` key, `pull_policy: never` on both services, versioned `image:` refs, all other lines preserved |
| AC4 | Transform fails fast on drift (throws rather than emitting a partial compose) |
| AC5 | Dev compose path unaffected (no diff to the tracked `docker-compose.yml`) |
| AC6 | Manifest correct at any size: tar manifested with SHA-256; `New-ManifestEntry` size is `[long]`, tested with a value > 2,147,483,647 |
| AC7 | Install loads before compose up (Stage 9, `<DestDockerDir>/openclaw-images.tar`, skipped under `-SkipDocker`) |
| AC8 | Load failure modes explicit (missing-tar throw with remediation text; non-zero `docker load` exit throw) |
| AC9 | Wrapper seam in place: both new modules < 500 lines with module-scoped `Invoke-DockerExe -DockerArgs`; all new docker calls via seam; four pre-existing `Install.Helpers.psm1` sites unmodified |
| AC10 | Bundle staging complete: `Install.Docker.psm1` staged; `docker-compose.dev.yml` no longer copied |
| AC11 | Tests hermetic: mock only the seam, no real docker, no `global:docker` shim, no temp files; positive/negative/`-WhatIf` paths |
| AC12 | No regression, clean toolchain: full Pester suite passes; PoshQC format + analyze clean in a single full pass |

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|---|---|---|
| AC1 | **PASS** | `Invoke-PublishDockerStage` (`scripts/Publish.Docker.psm1:323-363`) builds core then agent, then one `Save-OpenClawDockerImage` writing `Join-Path $BundleDockerDir 'openclaw-images.tar'`; the save vector names all four refs in order (`save`, `-o <path>`, `openclaw/core:<v>`, `openclaw/core:pre-mvp`, `openclaw/agent:<v>`, `openclaw/agent:pre-mvp`, lines 192-199). Exact-vector test `'composes the exact save vector naming all four refs in order'` and facade-ordering test `'runs core-build, agent-build, save, then writes the bundle compose'` independently re-run and passing; `Publish.ps1` Stage 3b call site confirmed at line 214, pinned by the updated `Publish.Tests.ps1` stage-order assertion. |
| AC2 | **PASS** | Core vector (`Publish.Docker.psm1:141-149`): `build -f deploy/docker/openclaw-core.Dockerfile --target runtime --build-arg BUILD_CONFIGURATION=<Configuration> -t openclaw/core:<v> -t openclaw/core:pre-mvp <RepoRoot>`. Agent vector (lines 154-161): `build -f deploy/docker/openclaw-agent.Dockerfile --build-arg OPENCLAW_AGENT_IMAGE=<resolved> -t ... <RepoRoot>`. Independently cross-checked against the tracked compose `build:` blocks (lines 5-10 core: dockerfile/target runtime/BUILD_CONFIGURATION; lines 53-57 agent: dockerfile/OPENCLAW_AGENT_IMAGE) — mirrored exactly. Cross-reference comment present in the module header (lines 11-20). `OPENCLAW_AGENT_IMAGE` resolved from the repo-root `.env` via existing `Get-EnvFileMap` (`Publish.ps1:214`), defaulting to `ghcr.io/openclaw/openclaw:latest` (`Resolve-OpenClawAgentBaseImage`, matching the Dockerfile ARG default). Exact core/agent build-vector tests and all three resolution tests independently re-run and passing. |
| AC3 | **PASS** | `Convert-ComposeToBundleCompose` (pure, `Publish.Docker.psm1:209-290`): removes the 4-space `build:` key + deeper-indented block, rewrites `image:` to `openclaw/<name>:<Version>`, inserts `pull_policy: never` at image indentation, preserves all other lines. Positive fixture test asserts full expected-output equality line-by-line plus zero `build:` lines and exactly two `pull_policy: never` lines; the real-file drift-guard test runs the transform against the actual tracked `docker-compose.yml` and asserts the same invariants with versioned image refs. Both independently re-run and passing. Install-time `.env` interpolation preserved (line-targeted transform; `${VAR:-default}` expressions untouched). |
| AC4 | **PASS** | Count-based guard throws naming the drifted service when either service lacks exactly one `build:`/`image:` key (`Publish.Docker.psm1:283-287`), before any output is returned. Two negative-path tests (`'throws naming openclaw-core when the core build key is absent'`, `'throws naming openclaw-agent when the agent image key is absent'`) independently re-run and passing. |
| AC5 | **PASS** | Tracked `docker-compose.yml` absent from `git diff --name-status ca53297..79c6a3b`; independently confirmed `git diff ca53297..79c6a3b -- docker-compose.yml docker-compose.dev.yml deploy/docker scripts/Install.Helpers.psm1` produces zero output. The tracked file retains both `build:` blocks and the `openclaw/*:pre-mvp` tags (source read). Matches the branch's own `evidence/qa-gates/invariant-diffs.2026-07-10T19-10.md`. |
| AC6 | **PASS** | `New-ManifestEntry` returns `size = [long]$fileInfo.Length` (`scripts/Publish.Helpers.psm1:156`, confirmed via diff). Updated test `'returns size as long and supports lengths above [int]::MaxValue'` uses 3,000,000,000 and asserts `Should -BeOfType [long]`; fail-before evidence (`evidence/regression-testing/ps-expect-fail-manifest-size.2026-07-10T19-10.md`, `OverflowException`/`PSInvalidCastException` at the `[int]` cast site) independently reviewed and consistent. Manifest inclusion of the tar is structural: Stage 3b (`Publish.ps1:214`) runs before Stage 6 `Write-PublishManifest`, whose existing recursive enumeration + SHA-256 hashing covers everything in the bundle (unchanged code path); the stage-order test pins this ordering. |
| AC7 | **PASS** | `scripts/Install.ps1:430-434`: inside `if (-not $SkipDocker)`, `$ImageTarPath = Join-Path $DestDockerDir 'openclaw-images.tar'` then `Invoke-DockerImageLoad -ImageTarPath $ImageTarPath` immediately before `Invoke-ComposeUp`. Stage-sequence tests (`Install.DockerStage.Tests.ps1`): exact tar path, load after `Copy-BundleContents` and before `Invoke-ComposeUp`, load on `-Force` reinstall, and `'does NOT invoke Invoke-DockerImageLoad under -SkipDocker'` — all independently re-run and passing. Fail-before evidence (`evidence/regression-testing/ps-expect-fail-install-load.2026-07-10T19-10.md`, 3 Its failing pre-wiring) independently reviewed and consistent. |
| AC8 | **PASS** | `Invoke-DockerImageLoad` (`scripts/Install.Docker.psm1:71-98`) throws naming the tar path plus the re-publish remediation when `Test-Path` fails, and throws with docker output on non-zero exit. Tests assert the message contains both the path and `Publish.ps1`, that the seam is never invoked on the missing-tar path, and the non-zero-exit throw — independently re-run and passing. |
| AC9 | **PASS** | Both modules exist, 373 and 103 lines (< 500, independently counted). Each defines a module-scoped `Invoke-DockerExe -DockerArgs <string[]>` seam; the only executable invocation in either module is `& docker @DockerArgs 2>&1` inside the seam (independently confirmed via grep — all other matches are comment-help text). `scripts/Install.Helpers.psm1` is byte-identical in this diff (empty `git diff`), so the four pre-existing direct docker call sites are unmodified. |
| AC10 | **PASS** | `Copy-InstallScriptsIntoBundle` staging list now includes `Install.Docker.psm1` as the fifth file (`scripts/Publish.Helpers.psm1:189`); `Copy-DockerArtifact` no longer references or copies `docker-compose.dev.yml` (diff removes both the path variable and the `Copy-Item`). Updated tests (`'copies ... and Install.Docker.psm1 in order'` asserting all 5 src/dst pairs; `'copies docker-compose.yml and never copies docker-compose.dev.yml'` asserting the negative) independently re-run and passing. |
| AC11 | **PASS** | Direct read of all three new test files: mocks target only `Invoke-DockerExe` (via `Mock -ModuleName`) and standard harness helpers; no `global:docker` shim; no `New-TemporaryFile`/`GetTempPath`/`$env:TEMP` usage; fixtures are in-memory string arrays; positive, negative, and `-WhatIf` paths all present for both modules. Matches the branch's own `evidence/qa-gates/seam-hermeticity-checks.2026-07-10T19-10.md` (zero grep matches). |
| AC12 | **PASS** | Independently re-verified in this audit: repo-wide `Invoke-Pester` over `tests/scripts` — **406/406 passed, 0 failed** (reproducing `evidence/qa-gates/final-poshqc-test.2026-07-10T19-10.md` exactly); `Invoke-Formatter` idempotency — 0 diffs on all 12 changed PowerShell files; `Invoke-ScriptAnalyzer` — 0 findings on all 12 files. Coverage independently re-parsed from `artifacts/pester/powershell-coverage.xml`: repo-wide 89.91% (baseline 89.34%, no regression), all five changed/new production files above the uniform gates (98.02%/87.50%/97.56%/96.70%/88.57% line). One non-gating Minor coverage note (`Install.ps1:113` pattern-replicating catch arm) is recorded in the code review and does not affect this criterion. |

## Root-Cause / Non-Goal Verification

- Root cause matches the spec: install-time image acquisition was impossible (tags in no registry → `pull access denied`; bundle build context lacks `src/` → MSB1009), so the fix relocates acquisition to publish time and makes the bundle compose structurally unable to pull or build (`pull_policy: never`, no `build:` keys). The latent `[int]` size-cast defect became reachable via the multi-GiB tar and is fixed on the write side only (the reader already parsed `[long]`).
- Non-goal: tracked `docker-compose.yml` unchanged — honored (empty diff; dev `--build` workflow intact).
- Non-goal: no retrofit of the four `Install.Helpers.psm1` docker call sites — honored (file byte-identical); recorded as a follow-up in the spec.
- Non-goal: no general process-runner framework — honored (two narrowly-scoped per-module seams).
- Non-goal: no registry publishing/signing/`docker login`, no `Uninstall.ps1` `docker rmi`, no `Wait-ComposeHealthy` timeout changes — honored (no such diffs anywhere in the branch).
- Excluded systems untouched: MSIX pipeline stages, HostAdapter stages, `.env`/secrets handling logic (Stage 3b performs read-only reuse of the already-loaded `$envContent`).

## Summary

All 12 acceptance criteria are independently verified **PASS** against direct source review, independently re-run toolchain checks (406/406 repo-wide Pester, 0 analyzer findings, 0 format diffs), independently re-parsed coverage data (all figures matching the executor's evidence exactly), and independently reviewed fail-before/pass-after regression evidence for both enabling fixes. All non-goals and boundary invariants are honored. No remediation is required.

## Acceptance Criteria Check-off

All 12 checkboxes in `spec.md` `## Acceptance Criteria` were already changed from `- [ ]` to `- [x]` by the executor prior to this review (cross-referenced in `evidence/qa-gates/ac-summary.2026-07-10T19-10.md`), with criterion text unchanged. Independent re-verification against each item's supporting evidence (table above) confirms every check-off is warranted; no reviewer-side check-off changes were required.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/spec.md`
- Total AC items: 12
- Checked off (delivered): 12
- Remaining (unchecked): 0
- Items remaining: none

## Overall Feature Audit Verdict

**PASS.** All 12 acceptance criteria are verified PASS in the authoritative AC source file against independently re-derived evidence, all non-goals are honored, and the root cause matches the spec's analysis. No remediation is required.
