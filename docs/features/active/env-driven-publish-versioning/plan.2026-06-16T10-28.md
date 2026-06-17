# env-driven-publish-versioning - Plan

- **Issue:** docs/features/active/env-driven-publish-versioning/issue.md
- **Work Mode:** minor-audit (per issue.md line 3)
- **Tier:** T4 PowerShell dev/build tooling (per `.claude/rules/quality-tiers.md`)
- **Last Updated:** 2026-06-16T16-05
- **Status:** Draft (Phase 3 executor-preflight deltas applied; pending re-validation)
- **Version:** 1.3.1

## Required References

This plan does not restate policy. It complies with and cites:

- `.claude/rules/general-code-change.md` (cross-language code change; 500-line file cap; I/O boundaries; design seams).
- `.claude/rules/general-unit-test.md` (coverage >= 85% line / >= 75% branch; no temp files in tests; determinism).
- `.claude/rules/powershell.md` (PoshQC format -> PSScriptAnalyzer analyze -> Pester; wrapper/adapter seam pattern; per-batch cap).
- `.claude/rules/quality-tiers.md` (T4 classification; uniform coverage thresholds).
- Requirements source for acceptance criteria: `docs/features/active/env-driven-publish-versioning/issue.md` section `## Acceptance Criteria` (AC-1..AC-9) and Design Decisions D1-D10.

**All work must comply with these policies; do not duplicate their content here.**

## Scope and Design Notes

Production PowerShell files in scope (issue.md Scope):

1. `scripts/Publish.Helpers.psm1` — extend `Resolve-CertThumbprint` (D7), update its export surface.
2. `scripts/Publish.ps1` — make `-Version` optional with read/increment/persist semantics (D3-D6) and `.env` thumbprint resolution (D7).
3. `scripts/New-MsixDevCert.ps1` — persist created thumbprint to `.env` `OPENCLAW_CERT_THUMBPRINT` (D8/AC-5).

**File-size extraction decision (general-code-change.md, 500-line cap).**
`scripts/Publish.Helpers.psm1` is already 581 lines (verified), which exceeds the 500-line cap before any change. Adding the new `.env` read/update/version-increment helpers into that module is not viable. The smallest extraction that honors D10 (small testable helpers behind a seam) and the cap is a new dedicated module `scripts/Publish.Env.psm1` that holds only the pure `.env` helpers (`Get-EnvFileMap`, `Set-EnvFileValue`, `Step-PackageVersion`) plus a thin file-I/O seam. Both `Publish.ps1` and `New-MsixDevCert.ps1` import this module. This adds one small scaffolding module rather than enlarging an already-over-cap file. The per-batch PowerShell file count (`powershell.md` Change Budget) is acknowledged: 3 named production files plus the extraction module; the extraction is mandated by the file-size policy, not optional refactor scope.

**Test-file split decision (general-code-change.md, 500-line cap).**
`tests/scripts/Publish.Helpers.Tests.ps1` is already 541 lines (verified), exceeding the 500-line cap before any change. Adding the extended `Resolve-CertThumbprint` `.env`-precedence `It` blocks would worsen the overrun. Before extending the cert-thumbprint tests, extract the `Resolve-CertThumbprint` context (and its shim/mock setup) from `tests/scripts/Publish.Helpers.Tests.ps1` into a new sibling file `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1` so both files land <= 500 lines. The new `.env`-precedence `It` blocks are placed in the new sibling file.

**Approved per-batch override (orchestrator-state.json `batch_override`).**
The orchestrator has APPROVED an explicit per-batch override for this plan. Basis: the 500-line cap forces both the `scripts/Publish.Env.psm1` extraction and the `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1` test-file split, so the batch necessarily touches more than 3 production/test files as a policy-driven structural necessity rather than optional refactor scope. This override is recorded here per the approval in `orchestrator-state.json` (`batch_override`). To stay within the executor's cap-respecting batch limits (at most 3 production + 3 test files per executor batch), Phase 1 tasks are organized into cap-respecting sub-batches (see the Phase 1 sub-batch grouping below); the executor processes each sub-batch independently.

Supporting (non-production) edits: `.env.example`, `README.md`, and Pester tests under `tests/scripts/`.

Helper contracts (D9/D10 — pure, no temp files; tests drive them with in-memory content arrays):
- `Get-EnvFileMap -Content <string[]>` -> ordered hashtable of key/value; ignores blank/comment lines; preserves first-wins on duplicate keys. Pure (no I/O).
- `Set-EnvFileValue -Content <string[]> -Key <string> -Value <string>` -> new `string[]` with the key updated in place when present (preserving surrounding comments, order, and unrelated keys) or appended when absent. Idempotent (D9/AC-6). Pure (no I/O).
- `Step-PackageVersion -Version <string>` -> new 4-part version with the 4th (revision) segment incremented by 1 (D3). Validates `^\d+\.\d+\.\d+\.\d+$`; throws on malformed input. Pure.
- A thin file seam (`Read-EnvFileContent` / `Write-EnvFileContent`) reads/writes the repo-root `.env` as a line array, so the pure helpers stay file-free and tests never touch disk (D10, general-unit-test.md no-temp-files rule).

## Evidence Locations (canonical, non-overridable)

All evidence under `docs/features/active/env-driven-publish-versioning/evidence/<kind>/` per `evidence-and-timestamp-conventions`. Timestamp format `yyyy-MM-ddTHH-mm`. Each command-step artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read policy files in required order and record the read.
  - Read `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`, `.claude/rules/quality-tiers.md`, and `docs/features/active/env-driven-publish-versioning/issue.md`.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/baseline/phase0-instructions-read.md` exists with `Timestamp:`, `Policy Order:`, and the explicit list of files read.

- [x] [P0-T2] Capture baseline PoshQC format state for the in-scope scripts.
  - Command: `mcp__drm-copilot__run_poshqc_format` over `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, `scripts/New-MsixDevCert.ps1`.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/baseline/format-baseline.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (formatting clean / files-that-would-change).

- [x] [P0-T3] Capture baseline PSScriptAnalyzer state for the in-scope scripts.
  - Command: `mcp__drm-copilot__run_poshqc_analyze` over the three in-scope scripts.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/baseline/analyze-baseline.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (analyzer finding count).

- [x] [P0-T4] Capture baseline Pester run with coverage for the existing script tests.
  - Command: `mcp__drm-copilot__run_poshqc_test` over `tests/scripts` in coverage mode.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/baseline/test-baseline.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` containing numeric pass count and baseline line/branch coverage headline values for the changed-script surface.

- [x] [P0-T5] Record the baseline line counts of the files near the cap.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/baseline/filesize-baseline.2026-06-16T10-28.md` records the current line counts of `scripts/Publish.Helpers.psm1` (581), `scripts/Publish.ps1` (213), and `scripts/New-MsixDevCert.ps1` (169), establishing the extraction rationale.

### Phase 1 — Constrained Small-Path Implementation (self-validating)

Each code task pairs with its Pester test task in this phase; no verification is deferred. Targeted-verification evidence is captured in the final Phase 1 task.

**Cap-respecting sub-batch grouping (approved per-batch override, `orchestrator-state.json` `batch_override`).**
The executor must process Phase 1 in the following sub-batches so no single batch exceeds 3 production + 3 test files:

- Sub-batch A (env module + its tests): production `scripts/Publish.Env.psm1`; test `tests/scripts/Publish.Env.Tests.ps1`. Tasks P1-T1..P1-T5.
- Sub-batch B (cert-thumbprint helper + test split + tests): production `scripts/Publish.Helpers.psm1`; tests `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1`. Tasks P1-T6..P1-T8.
- Sub-batch C (orchestrator + tests): production `scripts/Publish.ps1`; test `tests/scripts/Publish.Tests.ps1`. Tasks P1-T9..P1-T12.
- Sub-batch D (dev-cert persistence + tests): production `scripts/New-MsixDevCert.ps1`; test `tests/scripts/New-MsixDevCert.Tests.ps1`. Tasks P1-T13..P1-T14.
- Sub-batch E (docs): `.env.example`, `README.md`. Tasks P1-T15..P1-T16. Targeted-verification evidence: P1-T17.

- [x] [P1-T1] Create `scripts/Publish.Env.psm1` with `Get-EnvFileMap -Content <string[]>` returning an ordered map (ignores blanks/comments; first-wins on duplicates). Pure, no I/O. Export the function.
  - Acceptance: `Get-EnvFileMap` is defined and exported in `scripts/Publish.Env.psm1`; file is under 500 lines.

- [x] [P1-T2] Add `Set-EnvFileValue -Content <string[]> -Key -Value` to `scripts/Publish.Env.psm1` that updates a present key in place (preserving comments, order, unrelated keys) and appends when absent; idempotent on re-application. Pure, no I/O. Export it.
  - Acceptance: `Set-EnvFileValue` defined and exported in `scripts/Publish.Env.psm1`. Maps to AC-6, D9.

- [x] [P1-T3] Add `Step-PackageVersion -Version <string>` to `scripts/Publish.Env.psm1` that increments the 4th segment by 1 and validates `^\d+\.\d+\.\d+\.\d+$`, throwing on malformed input. Pure. Export it.
  - Acceptance: `Step-PackageVersion` defined and exported in `scripts/Publish.Env.psm1`. Maps to AC-1, D3.

- [x] [P1-T4] Add the file seam `Read-EnvFileContent -Path` and `Write-EnvFileContent -Path -Content` (with `SupportsShouldProcess`) to `scripts/Publish.Env.psm1` so the pure helpers stay file-free. Export both.
  - Acceptance: both seam functions defined and exported in `scripts/Publish.Env.psm1`; no pure helper performs file I/O. Maps to D10.

- [x] [P1-T5] Create `tests/scripts/Publish.Env.Tests.ps1` driving `Get-EnvFileMap`, `Set-EnvFileValue`, and `Step-PackageVersion` with in-memory `string[]` content only (no files on disk).
  - Cover: parse with comments/blanks; update-in-place preserves order/comments/unrelated keys; append-when-absent; idempotent re-apply (AC-6); revision increment `1.0.2.0 -> 1.0.2.1` (AC-1); malformed-version throw.
  - Acceptance: `tests/scripts/Publish.Env.Tests.ps1` exists; all `It` blocks pass via `mcp__drm-copilot__run_poshqc_test`; no temporary files created.

- [x] [P1-T6] Extend `Resolve-CertThumbprint` in `scripts/Publish.Helpers.psm1` to accept an injected `.env` thumbprint with precedence explicit `-CertThumbprint` > new `.env`-sourced `OPENCLAW_CERT_THUMBPRINT` parameter > dotnet user secret > existing lowest-precedence `-EnvThumbprint` (process env) value (D7). Do not duplicate the helper; preserve the empty-string-when-none contract. Add only the new `.env`-precedence parameter; do not alter the existing `-EnvThumbprint` parameter semantics.
  - Acceptance: `Resolve-CertThumbprint` signature in `scripts/Publish.Helpers.psm1` exposes a new `.env`-sourced parameter ordered above the user-secret source and above the existing `-EnvThumbprint` parameter; `Export-ModuleMember` remains unchanged because `Resolve-CertThumbprint` is already exported and only a parameter is added (no export edit needed). Maps to AC-4, D7.

- [x] [P1-T7] Extract the `Resolve-CertThumbprint` `Describe`/`Context` (and its shim/mock setup) out of `tests/scripts/Publish.Helpers.Tests.ps1` into a new sibling file `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1`, so both files land <= 500 lines (`Publish.Helpers.Tests.ps1` is currently 541, over the cap).
  - Acceptance: `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1` exists and contains the moved `Resolve-CertThumbprint` context with its shim setup; `tests/scripts/Publish.Helpers.Tests.ps1` no longer contains that context; both files are <= 500 lines; the moved tests pass via `mcp__drm-copilot__run_poshqc_test`.

- [x] [P1-T8] Add the extended precedence/parameter tests for `Resolve-CertThumbprint` in the new `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1` (explicit `-CertThumbprint` wins; `.env` `OPENCLAW_CERT_THUMBPRINT` beats user secret; user secret beats process-env `-EnvThumbprint`; empty when all absent), mocking only the `Invoke-DotnetExe` seam.
  - Acceptance: new/updated `It` blocks in `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1` pass; no real `dotnet` invoked; file remains <= 500 lines. Maps to AC-4.

- [x] [P1-T9] Make `-Version` optional in `scripts/Publish.ps1` (remove `Mandatory = $true`, keep the strict `ValidatePattern`). Import `scripts/Publish.Env.psm1`. When `-Version` is absent, read `OPENCLAW_PACKAGE_VERSION` from repo-root `.env`, increment via `Step-PackageVersion`, use the incremented value, and persist it back with `Set-EnvFileValue` + `Write-EnvFileContent` before the manifest stage. When `-Version` is supplied, use it verbatim and persist it (D5).
  - Acceptance: `scripts/Publish.ps1` `-Version` is optional; absent-path reads/increments/persists; supplied-path persists verbatim; file remains under 500 lines. Maps to AC-1, AC-2, D3, D4, D5.

- [x] [P1-T10] Add the fail-fast guard in `scripts/Publish.ps1`: when `-Version` is absent and `OPENCLAW_PACKAGE_VERSION` is missing/blank, throw a clear remediation error before any state-changing stage (do not invent a version).
  - Acceptance: the guard throws prior to bundle-root creation; error text names the missing key and remediation. Maps to AC-3, D6.

- [x] [P1-T11] Make the Stage 0a D7 thumbprint wiring explicit in `scripts/Publish.ps1` (lines ~106-112). Stage 0a must (a) read `OPENCLAW_CERT_THUMBPRINT` from repo-root `.env` via the new `Publish.Env.psm1` helpers and pass it to the NEW `.env`-precedence parameter of `Resolve-CertThumbprint`, and (b) continue passing `$env:OPENCLAW_CERT_THUMBPRINT` to the lowest-precedence `-EnvThumbprint` parameter. Net call-site precedence: explicit `-CertThumbprint` > `.env` `OPENCLAW_CERT_THUMBPRINT` > dotnet user secret > process env (`-EnvThumbprint`). Preserve the existing fail-fast contract (throw before any state-changing stage when neither `-SkipSign` nor any resolvable thumbprint exists).
  - Acceptance: Stage 0a in `scripts/Publish.ps1` passes the `.env`-sourced thumbprint to the new precedence parameter AND keeps `$env:OPENCLAW_CERT_THUMBPRINT` on `-EnvThumbprint`; the existing pre-stage throw is preserved; file remains under 500 lines. Maps to AC-4, D7.

- [x] [P1-T12] Update `tests/scripts/Publish.Tests.ps1` to cover the new orchestrator behavior using mocks/injected content (no disk `.env`): absent `-Version` reads/increments/persists (AC-1); supplied `-Version` used verbatim and persisted (AC-2); missing/blank version with no `-Version` throws before state change (AC-3); the Stage 0a `.env` thumbprint resolution path including an `It` asserting that, at the call-site level, `.env` `OPENCLAW_CERT_THUMBPRINT` beats both the dotnet user secret and the process-env `-EnvThumbprint` value (AC-4).
  - Acceptance: new `It` blocks in `tests/scripts/Publish.Tests.ps1` pass, including the call-site precedence `It` (`.env` beats user secret and process env); no temp files; signing fail-fast assertions retained. Maps to AC-4.

- [x] [P1-T13] Update `scripts/New-MsixDevCert.ps1`: import `scripts/Publish.Env.psm1` and define a script-scope testable helper `Save-CertThumbprintToEnv -Thumbprint <string> -EnvPath <string>` ABOVE the `Main` guard (`if ($MyInvocation.InvocationName -ne '.')`). The helper calls `Set-EnvFileValue` + the file-write seam (`Write-EnvFileContent`) to persist `OPENCLAW_CERT_THUMBPRINT` (preserving other keys/comments). The `Main` block calls `Save-CertThumbprintToEnv` on the success path after a certificate is created. The build still never creates a cert (D8 unchanged).
  - Acceptance: `scripts/New-MsixDevCert.ps1` defines `Save-CertThumbprintToEnv` above the `Main` guard; `Main` invokes it on the success path; the function is reachable when the script is dot-sourced (it is defined above the guard, so dot-sourcing loads it without executing `Main`); file remains under 500 lines. Maps to AC-5.

- [x] [P1-T14] Update `tests/scripts/New-MsixDevCert.Tests.ps1` to dot-source the script and call `Save-CertThumbprintToEnv` directly (the `Main` block does not run under dot-source), asserting the success path persists the thumbprint via the `.env` helpers: mock the `Write-EnvFileContent` write seam and assert `Set-EnvFileValue` is invoked with `OPENCLAW_CERT_THUMBPRINT` and the cert thumbprint, with no files written to disk.
  - Acceptance: new `It` block(s) in `tests/scripts/New-MsixDevCert.Tests.ps1` call `Save-CertThumbprintToEnv` directly and pass; the write seam is mocked; `Set-EnvFileValue` is asserted with `OPENCLAW_CERT_THUMBPRINT` and the thumbprint; no temp files; no disk write. Maps to AC-5.

- [x] [P1-T15] Update `.env.example` to document `OPENCLAW_PACKAGE_VERSION` (4-part, last-published) and `OPENCLAW_CERT_THUMBPRINT` (40-char hex SHA-1) with guidance comments, preserving the existing pending `OPENCLAW_AGENT_MODEL` last-line change.
  - Acceptance: `.env.example` contains both documented keys; the `OPENCLAW_AGENT_MODEL` working-tree change is intact. Maps to AC-7, D2.
  - Completed by operator edit (the path is permission-denied to all agent tool channels in this environment). Verified via `git diff .env.example`: both keys documented with guidance comments; `OPENCLAW_AGENT_MODEL=anthropic/claude-opus-4-8` preserved.

- [x] [P1-T16] Update `README.md` to remove host-specific absolute repository paths (use relative paths and a `$repoRoot` derived from the operator's own location) and document: (a) env-driven auto-incrementing publish (no `-Version` required); (b) the self-sign flow where `New-MsixDevCert.ps1` writes the thumbprint to `.env`; (c) storing an existing installed cert thumbprint into `.env` (locate via `Get-ChildItem Cert:\CurrentUser\My` code-signing certs, then write the thumbprint with the same helper or a manual `.env` edit).
  - Acceptance: `README.md` contains no host-specific absolute repo paths; all three flows documented. Maps to AC-8.

- [x] [P1-T17] Capture targeted-verification evidence for the changed surface.
  - Run the in-scope test files via `mcp__drm-copilot__run_poshqc_test` in coverage mode.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/regression-testing/targeted-verification.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` with pass counts and line/branch coverage for the new/changed code (`Publish.Env.psm1`, the `Resolve-CertThumbprint` change, `Publish.ps1`, `New-MsixDevCert.ps1`), each AC-1..AC-8 mapped to a passing `It`.

### Phase 2 — Final QC Loop

Run the full PowerShell toolchain in order (format -> analyze -> test). If any step changes files or fails, restart from format. Each command task is unconditional; `SKIPPED` is not a valid outcome.

- [x] [P2-T1] Run PoshQC format over all changed PowerShell files (`scripts/Publish.Env.psm1`, `scripts/Publish.Helpers.psm1`, `scripts/Publish.ps1`, `scripts/New-MsixDevCert.ps1`, and the changed/created test files `tests/scripts/Publish.Env.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1`).
  - Command: `mcp__drm-copilot__run_poshqc_format`.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/format-final.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (no files require reformatting).

- [x] [P2-T2] Run PSScriptAnalyzer over all changed PowerShell files.
  - Command: `mcp__drm-copilot__run_poshqc_analyze`.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/analyze-final.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (0 new analyzer findings; no new debt).

- [x] [P2-T3] Run the full Pester suite under `tests/scripts` with coverage enabled.
  - Command: `mcp__drm-copilot__run_poshqc_test`.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/test-final.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` with numeric pass count and post-change line/branch coverage values.

- [x] [P2-T4] Verify coverage thresholds and no-regression on changed code (line >= 85%, branch >= 75%).
  - Scope clarity: baseline-vs-post comparison (no-regression on changed lines) applies to the pre-existing changed files (`scripts/Publish.Helpers.psm1`, `scripts/Publish.ps1`, `scripts/New-MsixDevCert.ps1`), for which a baseline exists in P0-T4. The new module `scripts/Publish.Env.psm1` has no baseline (it did not exist), so it is evaluated as NEW code against the absolute thresholds (line >= 85%, branch >= 75%) with no baseline delta computed; absence of a baseline for `Publish.Env.psm1` is not a missing-baseline regression and must not be reported as one.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/coverage-delta.2026-06-16T10-28.md` reports baseline coverage (from P0-T4) and post-change coverage (from P2-T3) plus no-regression verdict for the pre-existing changed files, and new-code coverage for `scripts/Publish.Env.psm1` against the absolute thresholds; verdict PASS only when thresholds and no-regression hold, otherwise remediation-required. Maps to AC-9.

- [x] [P2-T5] Verify the 500-line cap holds for every changed/created file.
  - Resolved by Phase 3: `scripts/Publish.Helpers.psm1` reduced from 597 to 356 via the `Publish.Msix.psm1` extraction. All changed/created production and test files are now <= 500 (final counts recorded in the Phase 3 filesize-final evidence).
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/filesize-final.2026-06-16T10-28.md` lists post-change line counts for production files `scripts/Publish.Env.psm1`, `scripts/Publish.Helpers.psm1`, `scripts/Publish.ps1`, `scripts/New-MsixDevCert.ps1` and for every changed/created test file `tests/scripts/Publish.Env.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1`, each verified <= 500 (explicitly including the now-split `Publish.Helpers.Tests.ps1`, previously 541, and the new `Publish.Helpers.CertThumbprint.Tests.ps1`).

- [x] [P2-T6] Capture end-state evidence summarizing AC-1..AC-9 outcomes with pointers to the supporting artifacts.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/other/end-state.2026-06-16T10-28.md` maps each AC-1..AC-9 to its verifying artifact path and records overall PASS/remediation-required.

### Phase 3 — File-size cap remediation (Publish.Helpers.psm1 extraction)

This phase remediates a 500-line cap violation surfaced during execution: `scripts/Publish.Helpers.psm1` is 597 lines (over the cap). It was already 581 (over cap) at baseline; the parameter-only addition this branch made in P1-T6 pushed it to 597. P2-T5 already requires all changed production files <= 500; this phase finishes that requirement. The orchestrator approved this as a bounded extraction (`artifacts/orchestration/orchestrator-state.json` `open_items.item_2_publish_helpers_overcap`).

Pure relocation only: no function behavior or signatures change. The relocated cohesive set is the Windows SDK / MSIX tooling functions that occupy `scripts/Publish.Helpers.psm1` lines 23-256 (verified): `Find-WindowsSdkTool`, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, `Invoke-SignTool`. Moving these ~234 lines brings `Publish.Helpers.psm1` to approximately 360 lines (target <= ~480 with margin). The `.env`/version/cert-resolution helpers (`Invoke-DotnetPublish`, `Invoke-DotnetExe`, `Resolve-CertThumbprint`, `Copy-DockerArtifact`, `Copy-InstallScriptsIntoBundle`, `New-ManifestEntry`, `Write-PublishManifest`) stay in `Publish.Helpers.psm1`.

Per-batch cap (`powershell.md` Change Budget): this phase touches `scripts/Publish.Helpers.psm1`, `scripts/Publish.Msix.psm1`, `scripts/Publish.ps1` (3 production) and `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Msix.Tests.ps1` (2 test) — within the 3-production + 3-test cap; no override needed for this phase.

Run the full PowerShell toolchain in order (format -> analyze -> test). If any step changes files or fails, restart from format. Each command task is unconditional; `SKIPPED` is not a valid outcome.

- [x] [P3-T1] Create `scripts/Publish.Msix.psm1` and MOVE the cohesive Windows SDK / MSIX function set (`Find-WindowsSdkTool`, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, `Invoke-SignTool`) out of `scripts/Publish.Helpers.psm1` into it verbatim (no behavior or signature changes), including any header/license band the module requires to parse standalone.
  - Acceptance: `scripts/Publish.Msix.psm1` defines all seven functions and `scripts/Publish.Helpers.psm1` no longer defines any of them; both files parse; neither exceeds 500 lines (`Publish.Helpers.psm1` lands <= ~480 with margin; target approximately 360). Verified in the P3-T12 filesize-final artifact.

- [x] [P3-T2] Update `Export-ModuleMember` in `scripts/Publish.Helpers.psm1` to export exactly the functions it still defines (remove the seven relocated names; retain `Invoke-DotnetPublish`, `Invoke-DotnetExe`, `Resolve-CertThumbprint`, `Copy-DockerArtifact`, `Copy-InstallScriptsIntoBundle`, `New-ManifestEntry`, `Write-PublishManifest`).
  - Acceptance: `Export-ModuleMember` in `scripts/Publish.Helpers.psm1` lists exactly the seven retained functions and none of the relocated seven; the exported set matches the functions defined in the file.

- [x] [P3-T3] Fix the two `Context 'Module exports'` assertions so each test file asserts only the functions its module now exports (the existing assertion in `tests/scripts/Publish.Helpers.Tests.ps1` lines ~81-93 hard-codes a 14-function list that includes the seven relocated functions).
  - In `tests/scripts/Publish.Helpers.Tests.ps1`, update the `Context 'Module exports'` `It` to assert exactly the seven RETAINED functions via `Get-Command -Module Publish.Helpers` (`Invoke-DotnetPublish`, `Invoke-DotnetExe`, `Resolve-CertThumbprint`, `Copy-DockerArtifact`, `Copy-InstallScriptsIntoBundle`, `New-ManifestEntry`, `Write-PublishManifest`), and change the `It` count text from 14 to 7.
  - In the new `tests/scripts/Publish.Msix.Tests.ps1`, add a `Context 'Module exports'` `It` asserting exactly the seven RELOCATED functions via `Get-Command -Module Publish.Msix` (`Find-WindowsSdkTool`, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, `Invoke-SignTool`).
  - Acceptance: the `Publish.Helpers` exports `It` asserts exactly the seven retained names with count text 7, the `Publish.Msix` exports `It` asserts exactly the seven relocated names; both pass via `mcp__drm-copilot__run_poshqc_test`. Verified passing in P3-T10.

- [x] [P3-T4] Add `Export-ModuleMember` to `scripts/Publish.Msix.psm1` exporting exactly the seven relocated functions it now defines (`Find-WindowsSdkTool`, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, `Invoke-SignTool`).
  - Acceptance: `Export-ModuleMember` in `scripts/Publish.Msix.psm1` lists exactly the seven relocated functions; the exported set matches the functions defined in the file.

- [x] [P3-T5] Update `scripts/Publish.ps1` to `Import-Module` `scripts/Publish.Msix.psm1` in addition to the existing `scripts/Publish.Helpers.psm1` import, so all call sites for the relocated functions still resolve. Do not change call-site logic.
  - Acceptance: `scripts/Publish.ps1` imports both `Publish.Helpers.psm1` and `Publish.Msix.psm1`; every relocated-function call site resolves against the new module; `scripts/Publish.ps1` remains under 500 lines.

- [x] [P3-T6] Verify no other caller of the relocated functions breaks: grep the repository for invocations and imports of the seven relocated functions outside `scripts/Publish.ps1` and `tests/scripts/Publish.Msix.Tests.ps1`, and confirm each resolving site is non-orphaned after the import change.
  - Expected resolved (non-orphaned) caller: `tests/scripts/Publish.Tests.ps1` references the relocated function names only through caller-scope mocks (no `-ModuleName`), which resolve via `Publish.ps1`'s `Publish.Msix.psm1` import (added in P3-T5). Record it as an expected, resolved caller, not a break; do NOT require `tests/scripts/Publish.Tests.ps1` to import `Publish.Msix.psm1` and do NOT edit it for this phase.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/other/relocated-callers.2026-06-16T10-28.md` records `Timestamp:`, the grep `Command:`, `EXIT_CODE:`, and `Output Summary:` enumerating every call/import site of the seven functions, explicitly listing `tests/scripts/Publish.Tests.ps1` as a resolved caller-scope-mock site that needs no edit, and confirming no orphaned caller remains after the import change.

- [x] [P3-T7] MOVE the Pester tests for the relocated functions out of `tests/scripts/Publish.Helpers.Tests.ps1` into a new `tests/scripts/Publish.Msix.Tests.ps1` so test files mirror module structure. Move the corresponding `Describe`/`Context` blocks and their shim/mock setup; do not duplicate tests. Tests must use in-memory content only (no temp files; pure helpers driven with in-memory content per general-unit-test.md). The move is byte-verbatim except for the two module-binding adjustments below; no test logic or assertions change.
  - Module import: `tests/scripts/Publish.Msix.Tests.ps1` must `Import-Module` the new `scripts/Publish.Msix.psm1` in its `BeforeAll` (mirroring how `tests/scripts/Publish.Helpers.Tests.ps1` imports its module at line 56 via `Import-Module $script:ModulePath -Force` with `$script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Publish.Msix.psm1'`).
  - Intra-module mock rebinding: the three relocated intra-module mocks of `Find-WindowsSdkTool` (currently `Mock -ModuleName Publish.Helpers Find-WindowsSdkTool` at `tests/scripts/Publish.Helpers.Tests.ps1` lines 194, 212, 240, used by the `Invoke-MakePri`/`Invoke-MakeAppx`/`Invoke-SignTool` contexts because those functions call `Find-WindowsSdkTool` internally) must change `-ModuleName Publish.Helpers` to `-ModuleName Publish.Msix`. This is the only deviation from a verbatim move.
  - Shared external-tool shim move: move the `global:makepri`, `global:makeappx`, `global:signtool` shim functions plus their `$script:` shim state (`$script:MakePri*`, `$script:MakeAppx*`, `$script:SignTool*`) and the corresponding `BeforeEach` resets and `AfterAll` `Remove-Item` cleanup (currently in `tests/scripts/Publish.Helpers.Tests.ps1` `BeforeAll`/`BeforeEach`/`AfterAll`, lines ~18-79) into the new file's `BeforeAll`/`BeforeEach`/`AfterAll`. The `global:dotnet` shim and its `$script:Dotnet*` state STAY in `tests/scripts/Publish.Helpers.Tests.ps1` (used by the retained `Invoke-DotnetPublish` context). Contract: no shim is duplicated across the two files; each file declares only the shims its own contexts use.
  - Acceptance: `tests/scripts/Publish.Msix.Tests.ps1` exists, imports `scripts/Publish.Msix.psm1`, and contains the moved tests for the seven relocated functions with the three `Find-WindowsSdkTool` mocks rebound to `-ModuleName Publish.Msix` and the makepri/makeappx/signtool shims (state + resets + cleanup) present; `tests/scripts/Publish.Helpers.Tests.ps1` no longer contains those tests or those three shims, retains the `global:dotnet` shim and `$script:Dotnet*` state, and no test logic/assertions changed; no shim or test is duplicated across the two files; both files are <= 500 lines; no temporary files created. Verified passing via P3-T10.

- [x] [P3-T8] Run PoshQC format over the changed/created files for this phase (`scripts/Publish.Helpers.psm1`, `scripts/Publish.Msix.psm1`, `scripts/Publish.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Msix.Tests.ps1`).
  - Command: `mcp__drm-copilot__run_poshqc_format`.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/format-final.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (no files require reformatting). If files are reformatted, restart the toolchain from this task.

- [x] [P3-T9] Run PSScriptAnalyzer over the changed/created files for this phase.
  - Command: `mcp__drm-copilot__run_poshqc_analyze`.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/analyze-final.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (0 new analyzer findings; no new debt).

- [x] [P3-T10] Run the full Pester suite under `tests/scripts` with coverage enabled.
  - Command: `mcp__drm-copilot__run_poshqc_test`.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/test-final.2026-06-16T10-28.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` with numeric pass count and post-change line/branch coverage values; the relocated-function tests pass from `tests/scripts/Publish.Msix.Tests.ps1`, and both `Context 'Module exports'` `It` blocks (the seven retained in `Publish.Helpers.Tests.ps1`, the seven relocated in `Publish.Msix.Tests.ps1`; see P3-T3) pass.

- [x] [P3-T11] Verify coverage thresholds and no-regression after the relocation (line >= 85%, branch >= 75%).
  - Scope clarity: `scripts/Publish.Msix.psm1` is evaluated as new code against the absolute thresholds (line >= 85%, branch >= 75%); because the relocation is pure (tests moved with their functions), the relocated functions must retain their prior coverage — the per-function coverage for the seven relocated functions must not drop relative to the P2-T3/P2-T4 post-change values. `scripts/Publish.Helpers.psm1` (now smaller) must retain coverage for the functions that remain.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/coverage-delta.2026-06-16T10-28.md` reports post-change coverage (from P3-T10) for `scripts/Publish.Msix.psm1` (new-code thresholds) and `scripts/Publish.Helpers.psm1` (no-regression on retained functions), confirms the seven relocated functions retain their prior coverage, and records verdict PASS only when thresholds and no-regression hold, otherwise remediation-required.

- [x] [P3-T12] Verify the 500-line cap holds for every changed/created file in this phase.
  - Acceptance: `docs/features/active/env-driven-publish-versioning/evidence/qa-gates/filesize-final.2026-06-16T10-28.md` lists post-change line counts for production files `scripts/Publish.Helpers.psm1` (now <= ~480, target approximately 360), `scripts/Publish.Msix.psm1`, `scripts/Publish.ps1` and for test files `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Msix.Tests.ps1`, each verified <= 500; explicitly confirms `scripts/Publish.Helpers.psm1` is now under the cap (was 597).

## Acceptance Criteria Traceability

- AC-1 -> P1-T3, P1-T9, P1-T12
- AC-2 -> P1-T9, P1-T12
- AC-3 -> P1-T10, P1-T12
- AC-4 -> P1-T6, P1-T7, P1-T8, P1-T11, P1-T12
- AC-5 -> P1-T13, P1-T14
- AC-6 -> P1-T2, P1-T5
- AC-7 -> P1-T15
- AC-8 -> P1-T16
- AC-9 -> P2-T1, P2-T2, P2-T3, P2-T4

## Open Questions / Notes

- Extraction: new module `scripts/Publish.Env.psm1` is required because `scripts/Publish.Helpers.psm1` is already 581 lines (over the 500-line cap). This is the smallest change that adds the env helpers without enlarging an over-cap file.
- The repo-root `.env` is gitignored; version/cert state is per-clone (issue.md Dependencies/Risks). Tests never read or write the real `.env`; they drive the pure helpers with in-memory content arrays and mock the file seam (D10, general-unit-test.md no-temp-files rule).

### Preflight revision log (v1.1, 2026-06-16T11-05)

Applied the five executor-preflight deltas (REVISIONS REQUIRED):

- Delta 1 (Blocking): `tests/scripts/Publish.Helpers.Tests.ps1` is 541 lines (over cap). Added P1-T7 to split the `Resolve-CertThumbprint` context into new sibling `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1` before extending the cert tests (P1-T8). Recorded the approved per-batch override (`orchestrator-state.json` `batch_override`) in Scope and Design Notes and added cap-respecting Phase 1 sub-batch grouping (<= 3 production + 3 test files per executor batch). Updated P2-T5 to verify all changed/created test files <= 500.
- Delta 2 (Blocking): P1-T13 now defines a script-scope `Save-CertThumbprintToEnv -Thumbprint -EnvPath` helper above the `Main` guard in `scripts/New-MsixDevCert.ps1`, callable when dot-sourced; P1-T14 calls it directly and mocks the write seam.
- Delta 3 (Blocking): P1-T11 makes Stage 0a D7 wiring explicit (`.env` `OPENCLAW_CERT_THUMBPRINT` -> new precedence parameter; `$env:OPENCLAW_CERT_THUMBPRINT` -> lowest-precedence `-EnvThumbprint`). P1-T12 adds a call-site `It` asserting `.env` beats user secret and process env.
- Delta 4 (Minor): P1-T6 acceptance states `Export-ModuleMember` is unchanged (parameter-only addition; `Resolve-CertThumbprint` already exported).
- Delta 5 (Minor): P2-T4 clarifies baseline-vs-post comparison applies to pre-existing changed files (`Publish.Helpers.psm1`, `Publish.ps1`, `New-MsixDevCert.ps1`), while new `Publish.Env.psm1` is evaluated as new code against absolute thresholds with no baseline delta.

### Revision log (v1.2, 2026-06-16T14-05)

Added Phase 3 — File-size cap remediation (Publish.Helpers.psm1 extraction). Execution surfaced `scripts/Publish.Helpers.psm1` at 597 lines, over the 500-line cap (`.claude/rules/general-code-change.md`). It was already 581 (over cap) at baseline; the P1-T6 parameter-only addition pushed it to 597. The orchestrator approved a bounded extraction (`artifacts/orchestration/orchestrator-state.json` `open_items.item_2_publish_helpers_overcap`). Phase 3 (P3-T1..P3-T11) moves the cohesive Windows SDK / MSIX function set (`Find-WindowsSdkTool`, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, `Invoke-SignTool`; verified at lines 23-256, ~234 lines) into a new `scripts/Publish.Msix.psm1`, bringing `Publish.Helpers.psm1` to approximately 360 lines (target <= ~480). The phase updates `Export-ModuleMember` in both modules, imports the new module in `scripts/Publish.ps1`, verifies no caller breaks, relocates the corresponding Pester tests into `tests/scripts/Publish.Msix.Tests.ps1` without duplication, and re-runs the full PowerShell toolchain with updated qa-gates evidence (format-final, analyze-final, test-final, coverage-delta, filesize-final). Pure relocation only: no behavior or signature changes. The phase touches 3 production + 2 test files, within the per-batch cap; no override required for this phase.

### Revision log (v1.3, 2026-06-16T15-20)

Applied the three executor-preflight Phase 3 deltas (REVISIONS REQUIRED). Phase 0-2 (completed) are unchanged.

- Delta 1 (Blocking): the P3-T6 test move is not byte-verbatim. Amended P3-T6 to require `tests/scripts/Publish.Msix.Tests.ps1` to `Import-Module scripts/Publish.Msix.psm1` (mirroring the `Publish.Helpers.Tests.ps1` import at line 56), to rebind the three relocated intra-module `Find-WindowsSdkTool` mocks from `-ModuleName Publish.Helpers` to `-ModuleName Publish.Msix` (verified at `Publish.Helpers.Tests.ps1` lines 194/212/240, used by the `Invoke-MakePri`/`Invoke-MakeAppx`/`Invoke-SignTool` contexts), and to move the `global:makepri`/`global:makeappx`/`global:signtool` shims plus their `$script:` state and `BeforeEach`/`AfterAll` resets (lines ~18-79) into the new file. The `global:dotnet` shim and `$script:Dotnet*` state stay in `Publish.Helpers.Tests.ps1` for the retained `Invoke-DotnetPublish` context. No shim duplicated; no test logic/assertions changed.
- Delta 2 (Blocking): the `Context 'Module exports'` `It` in `Publish.Helpers.Tests.ps1` (lines ~81-93) hard-codes a 14-function list including the seven relocated functions. Added P3-T2a to update that `It` to assert exactly the seven RETAINED functions and change the count text from 14 to 7, and to add a `Module exports` `It` in `Publish.Msix.Tests.ps1` asserting exactly the seven RELOCATED functions via `Get-Command -Module Publish.Msix`. Both pass via `mcp__drm-copilot__run_poshqc_test`; referenced in P3-T9 acceptance.
- Delta 3 (Minor): amended P3-T5 to record `tests/scripts/Publish.Tests.ps1` as an expected, resolved (non-orphaned) caller. It references the relocated function names only through caller-scope mocks (no `-ModuleName`), resolving via `Publish.ps1`'s `Publish.Msix.psm1` import (P3-T4); it needs no edit and is not required to import `Publish.Msix.psm1`.

File-size cap confirmation: the P3-T6 move removes the seven relocated-function contexts and the makepri/makeappx/signtool shims from `tests/scripts/Publish.Helpers.Tests.ps1` (currently within the cap) and places them, with the small `Module exports` `It`, into the new `tests/scripts/Publish.Msix.Tests.ps1`. Neither test file, nor any production file in scope (`scripts/Publish.Helpers.psm1` target ~360, `scripts/Publish.Msix.psm1`, `scripts/Publish.ps1`), exceeds 500 lines after these changes; P3-T11 verifies post-change line counts.

> Note: the v1.1/v1.2/v1.3 task-ID references above are retained verbatim as a historical record of those revisions. For the current numeric-only IDs (after v1.3.1), use the live Phase 3 task list in this plan body. The former `P3-T2a` is now `P3-T3`, and the subsequent tasks shift up by one (former `P3-T3..P3-T11` are now `P3-T4..P3-T12`).

### Revision log (v1.3.1, 2026-06-16T16-05)

v1.3.1: renumbered Phase 3 task IDs to numeric-only for validator compliance.
