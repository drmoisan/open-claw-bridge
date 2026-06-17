# Feature Audit: env-driven-publish-versioning

**Audit Date:** 2026-06-16
**Feature Folder:** `docs/features/active/env-driven-publish-versioning`
**Base Branch:** `main`
**Head Branch:** staged working tree on `feature/env-driven-publish-versioning`
**Work Mode:** `minor-audit`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (merge-base `1f3bb41`)
- **Head branch/commit:** working tree on `feature/env-driven-publish-versioning` (branch HEAD == merge-base; feature changes are uncommitted)
- **Merge base:** `1f3bb41`
- **Evidence sources:**
  - Primary: `git diff HEAD` plus untracked files (equivalent to full feature-vs-base diff)
  - Secondary baseline diff: `git diff HEAD -- .env.example` and per-file diffs
  - Feature evidence: `docs/features/active/env-driven-publish-versioning/evidence/**`
  - Additional evidence: reviewer-run `Invoke-Pester` (normal + coverage) and PoshQC format/analyze
- **Feature folder used:** `docs/features/active/env-driven-publish-versioning`
- **Requirements source:** `issue.md` (`## Acceptance Criteria`, AC-1..AC-9)
- **Work mode resolution note:** `issue.md` line 3 declares `- Work Mode: minor-audit`; per the acceptance-criteria-tracking skill, the only AC source is the `## Acceptance Criteria` section of `issue.md`.
- **Scope note:** Working-tree-only validation because all feature changes are uncommitted (branch HEAD equals the merge-base). PR-context artifacts were not present; the full diff was taken directly from the working tree.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/env-driven-publish-versioning/issue.md` — only source (minor-audit)

### Acceptance criteria

1. AC-1: `scripts/Publish.ps1 -SkipSign` with no `-Version` reads `OPENCLAW_PACKAGE_VERSION` from `.env`, publishes the next revision, and writes the incremented value back to `.env`.
2. AC-2: `scripts/Publish.ps1 -Version 'X.Y.Z.W' -SkipSign` uses `X.Y.Z.W` verbatim and persists it to `OPENCLAW_PACKAGE_VERSION` in `.env`.
3. AC-3: With no `-Version` and a missing/blank `OPENCLAW_PACKAGE_VERSION`, the script throws a clear remediation error before any state-changing stage.
4. AC-4: With no `-CertThumbprint` and no `-SkipSign`, the thumbprint is resolved from `OPENCLAW_CERT_THUMBPRINT` in `.env` (when present) per the D7 precedence.
5. AC-5: `scripts/New-MsixDevCert.ps1` writes the created certificate's thumbprint to `OPENCLAW_CERT_THUMBPRINT` in `.env`, preserving other keys/comments.
6. AC-6: The `.env` writer is idempotent: re-running updates the value in place and does not duplicate the key or disturb unrelated lines.
7. AC-7: `.env.example` documents both new keys with guidance comments; the existing `OPENCLAW_AGENT_MODEL` line change in the working tree is preserved.
8. AC-8: README contains no host-specific absolute repository paths and documents (a) env-driven auto-incrementing publish, (b) the self-sign flow that writes the thumbprint to `.env`, and (c) storing an existing installed cert thumbprint into `.env`.
9. AC-9: PowerShell toolchain passes (format -> analyze -> Pester) with line coverage >= 85% and branch coverage >= 75% on changed code; no new analyzer debt; no temp files in tests.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| 1 | AC-1 no -Version: read/increment/persist | PASS | `Publish.ps1` Stage 00 reads `OPENCLAW_PACKAGE_VERSION` + `Step-PackageVersion`; Stage 0c persists. Tests `Publish.Tests.ps1:285`, `Publish.Env.Tests.ps1` increment | `git diff HEAD -- scripts/Publish.ps1`; `Invoke-Pester` | Increment 1.0.2.0 -> 1.0.2.1 verified |
| 2 | AC-2 -Version verbatim + persist | PASS | Supplied `-Version` used verbatim, persisted Stage 0c. Test `Publish.Tests.ps1:302` | `Invoke-Pester` | ValidatePattern retained |
| 3 | AC-3 missing/blank version throws pre-state | PASS | Stage 00 throws remediation message before state change. Tests `Publish.Tests.ps1:313` (missing), `:321` (blank) | `Invoke-Pester` | Throw text names key + remediation |
| 4 | AC-4 .env thumbprint per D7 | PASS | `Resolve-CertThumbprint -DotEnvThumbprint` ranks above user secret and process env; Stage 0a injects `.env`. Tests `Publish.Helpers.CertThumbprint.Tests.ps1:81-129`, `Publish.Tests.ps1:336,358` | `Invoke-Pester` | Call-site precedence asserted |
| 5 | AC-5 dev cert writes thumbprint to .env | PASS | `Save-CertThumbprintToEnv` above Main guard; Main calls on success path. Tests `New-MsixDevCert.Tests.ps1:114-158` | `Invoke-Pester` | Preserves keys; -WhatIf no write |
| 6 | AC-6 idempotent .env writer | PASS | `Set-EnvFileValue` update-in-place/append/idempotent. Tests `Publish.Env.Tests.ps1:71,87,99,106,112` | `Invoke-Pester` | No dup key; unrelated lines intact |
| 7 | AC-7 .env.example keys + AGENT_MODEL preserved | PASS | Diff adds both keys with comments; `OPENCLAW_AGENT_MODEL` updated and preserved | `git diff HEAD -- .env.example` | Direct file read denied; verified via diff |
| 8 | AC-8 README paths + three flows | PASS | No host-specific absolute paths (grep clean; `$repoRoot` derived); flows documented (~124-156, ~406-414, ~416-433) | grep for absolute paths; `Read README.md` | All three flows present |
| 9 | AC-9 toolchain + coverage + no debt + no temp files | PASS | Format ok, analyze ok (0 new debt), Pester 281/0 (reproduced; coverage mode also passes), aggregate line coverage 89.95%, no temp files | PoshQC MCP; `Invoke-Pester` (+coverage) | Per-file PARTIAL on New-MsixDevCert.ps1 (untestable Main guard; new code fully covered; no regression) — non-blocking; see Notes |

---

## Summary

**Overall Feature Readiness:** PASS

**Criteria summary:**
- **PASS:** 9 criteria
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

1. None.

**Recommended follow-up verification steps:**

1. Optional: add a Main-extraction seam to `scripts/New-MsixDevCert.ps1` so the Main wiring is unit-exercisable and the file-level coverage number reflects the new behavior.
2. Optional: rename the README `$pwd` example variable and remove/ignore the stray `testResults.xml`.

### MCP test-wrapper note

`run_poshqc_test` returned exit code 4294967295 (-1) in coverage mode. Independent
`Invoke-Pester` (normal and coverage) returns Result=Passed, LASTEXITCODE=0, 281/0. The
non-zero wrapper summary is a coverage-mode reporting artifact, not a genuine test/toolchain
failure, and is not Blocking. The authoritative CI command (`Invoke-Pester ... -CI`) is the gate.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules, `issue.md` presents AC-1..AC-9 as prose bullets
(not markdown checkboxes), so no `- [ ]` items exist to toggle. Status is recorded in this
audit only; the source file is not rewritten.

### AC Status Summary

- Source: `docs/features/active/env-driven-publish-versioning/issue.md`
- Total AC items: 9
- Checked off (delivered): 9 (evaluated PASS; prose-only, not checkbox-backed)
- Remaining (unchecked): 0
- Items remaining: None.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `docs/features/active/env-driven-publish-versioning/issue.md` | 9 | 9 | 0 | Prose-only; no checkbox change made |

No source-file checkbox change was made because the AC source uses prose bullets, not markdown
checkboxes.
