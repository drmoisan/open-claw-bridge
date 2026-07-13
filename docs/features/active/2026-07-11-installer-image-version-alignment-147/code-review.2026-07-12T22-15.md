# Code Review — Issue #147 (installer-image-version-alignment)

- Reviewed: 2026-07-12T22-15
- Base: `origin/epic/openclaw-runtime-remediation-integration` @ `5f6bab23778e62eb2f9a3f17bf18189d90c2b4ba`
- Head: `bug/installer-image-version-alignment-147` @ `0e7174d719561d8e707f72a6c1197bb593f311ab`

## Executive Summary

The branch closes the gap where `Install.ps1` could stage and start a Docker Compose stack whose Control UI (`openclaw/core`) and gateway (`openclaw/agent`) image tags disagreed with each other or with the bundle's resolved version. A new Stage 9 guard (`Assert-ComposeImageVersionAligned`, backed by the local helper `Get-ComposeServiceImageTag`) reads the staged `docker-compose.yml`, extracts both services' pinned tags, and throws before `Invoke-DockerImageLoad` runs if either tag disagrees with `$ResolvedVersion`. A parallel, directly-testable pair of pure functions (`ConvertFrom-OpenClawImageReference`, `Test-OpenClawImageVersionAligned`) is added to `OpenClawContainerValidation.psm1`. The deliberate non-import of the validation module from `Install.ps1` (duplicating ~10 lines of comparison logic) is a ratified, well-reasoned design decision tied to the bundle-staging file set, and is independently verified honored (zero `Import-Module` references to the module in `Install.ps1`). All #142/#144 invariants are independently confirmed unchanged. Two Minor and two Info findings; no Blocking or Major findings.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Minor | `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | `Test-OpenClawImageVersionAligned`, well-formedness check (`[System.Version]::TryParse`) | The "well-formed 4-part version" check does not actually enforce a 4-part format. `[System.Version]::TryParse` accepts 2-, 3-, and 4-component strings; a 3-part tag such as `1.2.3` parses successfully (`TryParse("1.2.3") == $true`, independently verified), so the function reports it as a "version mismatch" rather than the "malformed image tag(s)" label the spec and AC6 describe for this class of input. `IsExpected` still correctly evaluates `$false` (the exact-string-equality check against `-ExpectedVersion` is the actual safety net), so no incorrect installs result — the gap is in message classification, not in the fail-safe outcome. | No functional fix required for correctness. If message precision matters, add an explicit `-match '^\d+\.\d+\.\d+\.\d+$'` check alongside `TryParse` so 3-part strings are classified as malformed rather than "mismatched." Non-gating either way, since the described boundary-condition unit test (`-ForEach @('1.2.3', ...)`) only asserts the malformed literal string appears somewhere in `Summary`, which it does regardless of which branch produces it. | This exact `TryParse`-only pattern already exists, unchanged, in `Get-ManifestVersion` (`scripts/Install.Helpers.psm1:43`), which the spec explicitly cites as the pattern to mirror — so this is a pre-existing codebase convention being extended consistently, not a new defect introduced by this branch. | Independently reproduced: `[System.Version]::TryParse("1.2.3", [ref]$v)` returns `$true`; direct function call with `-CoreImageReference 'openclaw/core:1.2.3' -AgentImageReference 'openclaw/agent:1.2.3.0' -ExpectedVersion '1.2.3.0'` returns `Summary = "Unexpected: version mismatch - core tag '1.2.3', ..."` (not "malformed"). |
| Minor | `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Force.Tests.ps1` | Shared `$script:GetContentMock`, new `*docker-compose.yml` branch | The plan scoped exactly 2 test files; these 2 additional files were extended during execution (task P3-T4) after the full regression run showed the new Stage 9 guard broke 19 pre-existing tests (no `docker-compose.yml` fixture branch existed). Independently confirmed to be fixture-only: both diffs add only a 6-line `Get-Content` mock branch returning `openclaw/core:1.2.3.0`/`openclaw/agent:1.2.3.0` (matching each file's existing `Get-ManifestVersion` mock of `'1.2.3.0'`); no production logic, assertion, or test intent in either file was altered. | No further action required on this branch — the extension is correctly scoped, low-risk, and necessary (the alternative was 19 broken pre-existing tests). Recommend recording an explicit change-budget override note alongside the existing narrative evidence for future occurrences of this same fixture-cascade pattern (see policy-audit PA-1). | A production behavior change (a new, real Stage 9 guard) that reads a file none of the pre-existing non-`-SkipDocker` test scenarios previously fixture-mocked will always require this kind of mechanical fixture update wherever that mock is duplicated across test files; this is inherent to the shared-fixture-per-file pattern already used in this test suite, not a scope-control lapse. | `git diff` of both files shows exactly the 6-line mock-branch addition each; independently re-run 36/36 passing combined. |
| Info | `scripts/Install.ps1` | `Assert-ComposeImageVersionAligned` / `Get-ComposeServiceImageTag` | Intentional duplication of the pure tag-vs-version comparison logic between this local helper pair and the module's `Test-OpenClawImageVersionAligned`/`ConvertFrom-OpenClawImageReference`. | No action required. This is a ratified, explicitly documented design decision (spec.md "Design summary"), driven by the fact that `OpenClawContainerValidation.psm1` is not part of `Copy-InstallScriptsIntoBundle`'s staged file set, so `Install.ps1` cannot import it without breaking at runtime on a real installed bundle. | Confirmed by independent verification: `Copy-InstallScriptsIntoBundle`/`Publish.Helpers.psm1` is untouched in this diff, and `grep -n "OpenClawContainerValidation" scripts/Install.ps1` returns zero matches. AC11 explicitly exists as the concrete, repeatable check against future accidental re-introduction of an import. | `git diff` (empty) for `scripts/Publish.Helpers.psm1`; `grep` result for `Install.ps1`. |
| Info | `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | Three pre-existing docstrings condensed (`Get-OpenClawOperatorEnvFilePath`, `Resolve-OpenClawDefaultEnvFilePath`, `Test-OpenClawGatewayTokenInContainer`) | Multi-line `.DESCRIPTION` blocks on three unrelated, pre-existing functions were shortened to single-line `.SYNOPSIS` text, to stay under the 500-line cap after adding the two new functions (452 -> 495 lines). | No action required; this is disclosed by the executor (`evidence/other/pr-notes.2026-07-11T19-34.md`) as behavior-preserving. Independently confirmed the underlying logic (`Join-Path`, `Test-Path`, null/whitespace checks) is byte-identical; only comment text changed. | Reducing pre-existing comment verbosity to make room for genuinely new functionality, in the same file, in the same commit, is a reasonable and disclosed way to stay under the file-size cap rather than splitting the module — consistent with "Simplicity first" and does not touch any invariant-preserving behavior. | `git diff` of the three docstring blocks; source read confirms unchanged function bodies. |

No Blocking or Major findings were identified.

## Files Reviewed

| File | Change | Assessment |
|---|---|---|
| `scripts/Install.ps1` | +41/-0 (net line delta 455 -> 496): new `Get-ComposeServiceImageTag`, `Assert-ComposeImageVersionAligned`; Stage 9 guard call wired in before `Invoke-DockerImageLoad`, inside the existing `-SkipDocker` gate, with an `Write-Information` progress line matching the surrounding style. | Correct placement, correct gating, correct error content (both raw image refs, both extracted tags, `$ResolvedVersion`, `$ComposeFilePath`). Approved verb usage (`Get`, `Assert` — both Microsoft-approved). |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | +93/-19 (net 452 -> 495): new `ConvertFrom-OpenClawImageReference`, `Test-OpenClawImageVersionAligned`; both added to `Export-ModuleMember`; three pre-existing docstrings condensed. | New functions are pure, follow the module's existing `Get-OpenClawValidationResult`-shaped `Category`/`Name`/`Details` convention exactly. See CR-1 (Minor) for the malformed-tag message-classification gap. |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` | +2 lines: `FunctionsToExport` extended with both new function names. | Clean superset extension; no export removed or renamed. |
| `tests/scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.ImageVersionAlignment.Tests.ps1` | New, 110 lines, 12 Its. | See Test Quality below. |
| `tests/scripts/Install.DockerStage.Tests.ps1` | +102/-0 (202 -> 304 lines): new `Get-Content` mock branch; new `Context 'image version alignment guard'` (5 Its). | See Test Quality below. |
| `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Force.Tests.ps1` | +6 lines each: identical `*docker-compose.yml` mock-branch addition. | Fixture-only, mechanical, low-risk (see CR-2, Minor). |

## Design Principles (general-code-change.md)

- **Simplicity first**: the guard is a minimal regex line-scan over a small, structurally-guaranteed (post-#142) compose file, not a general YAML parser — appropriately scoped to the actual input shape.
- **Reusability**: the module's two new functions reuse the existing `Get-OpenClawValidationResult` result-shaping helper, matching every other probe in the module.
- **Extensibility**: both new module functions use named, mandatory parameters; no positional-parameter coupling.
- **Separation of concerns**: pure parse/compare logic (`ConvertFrom-OpenClawImageReference`, `Test-OpenClawImageVersionAligned`, `Get-ComposeServiceImageTag`) is fully separated from the single `Get-Content` I/O call in `Assert-ComposeImageVersionAligned`.
- **Non-goals honored** (all independently confirmed via empty `git diff`): tracked `docker-compose.yml` byte-identical; `Install.Docker.psm1` untouched; `Install.Helpers.psm1` untouched (four direct `docker` call sites remain un-retrofitted); `Publish.Helpers.psm1`/`Copy-InstallScriptsIntoBundle` untouched; `Invoke-OpenClawContainerPathValidation.ps1` untouched; shared test fixture untouched.

## Test Quality

- One behavior per `It`, clear `Context` grouping (`ConvertFrom-OpenClawImageReference`; matched/single-mismatch; same-wrong-version/pre-mvp edge cases; malformed tags), descriptive names traceable to the spec's named edge cases. Arrange–Act–Assert structure used consistently (explicit `# Arrange`/`# Act`/`# Assert` comments in the new module test file).
- All 12 module tests and all 12 tests in the extended `Install.DockerStage.Tests.ps1` (including the 5 new guard-context tests) were independently re-run by this reviewer with plain `Invoke-Pester` (no coverage wrapper): 100% pass in both files.
- Hermeticity: the new/extended `Get-Content` mock branches intercept an already-mocked filesystem primitive at the harness level in these files (not a newly-introduced external-executable mock); no `global:docker` shim, no real Docker/network calls, no temp files (fixtures are in-memory string arrays) — independently confirmed by direct read of all 4 changed/new test files.
- Exact-value assertions: the guard-context tests assert both the throw itself (`Should -Throw`) and the negative absence of the docker state-changing call (`$global:InstallTestCalls -notcontains 'Invoke-DockerImageLoad'`), directly matching the spec's stated verification technique.
- Boundary/edge-case coverage in the module test file: `pre-mvp` floating tag, missing `:` separator (empty tag), and all four named malformed-version-string shapes (`1.2.3`, `v1.2.3.0`, `1.2.3.a`, `latest`) are each independently exercised; see CR-1 for the one message-classification nuance among these (functional outcome remains correct in all cases).
- Fail-before/pass-after evidence is genuine: `evidence/regression-testing/ps-expect-fail-image-version-guard.2026-07-11T19-34.md` shows 3 of 5 targeted guard-context Its failing against the pre-guard `Install.ps1` (captured before the guard existed), independently reviewed for content accuracy.

## Naming and Style

- Approved verbs throughout (`Get-`, `Assert-`, `ConvertFrom-`, `Test-`); zero `PSUseApprovedVerbs` or other analyzer findings, independently confirmed via `Invoke-ScriptAnalyzer` on all 7 changed files.
- Comment-based help (`.SYNOPSIS`) present on both new module functions, consistent with the module's condensed-help convention post-change.
- `Write-Information` progress-line style (`[install:docker-version-check] ...`) matches the surrounding Stage 9 log-prefix convention exactly.

## Error Handling and Logging (general-code-change.md)

- Fail-fast with specific, actionable messages: the cross-service/same-wrong-version throw names both raw image refs (`openclaw/core:$coreTag`, `openclaw/agent:$agentTag`), `$ResolvedVersion`, and `$ComposeFilePath`, plus a concrete remediation instruction ("Re-publish the bundle with scripts/Publish.ps1 or correct the staged docker-compose.yml before retrying"). The missing-`image:`-line throw names the specific repository and explains the likely cause (malformed/drifted bundle).
- No broad catch-alls introduced; both new `Install.ps1` functions use `throw` directly, matching the surrounding Stage 9 code's existing error-handling style.
- The module's `Test-OpenClawImageVersionAligned` never throws for a "false" condition, consistent with every other probe in this module (returns a shaped result object instead) — matches the module's established API contract.

## Dependencies

- No new package dependencies. Both changes use only built-in PowerShell (`Get-Content`, regex, `[System.Version]`) already used elsewhere in these files.

## I/O Boundaries (general-code-change.md)

- The only new I/O call is a single `Get-Content -LiteralPath` read of the staged compose file inside `Assert-ComposeImageVersionAligned`; both new module functions perform zero I/O. Consistent with the repo's established pure-logic/thin-wiring separation.

## Risks Observed During Review

- The regex-based `Get-ComposeServiceImageTag` assumes the compose file's structural shape enforced by `Convert-ComposeToBundleCompose` (4-space `image:` indentation, exactly one `image:` line per service). This is explicitly documented as a risk in spec.md and is an acceptable, disclosed coupling given the alternative (a full YAML parser) is out of scope.
- No finding rises above Minor; both Minor findings are message-classification/process-documentation nuances, not functional or safety defects — the fail-safe `IsExpected = $false` / `throw` outcome is correct in every tested case.

## Overall Code Quality Verdict

**PASS.** The change is well-decomposed, honors every stated boundary and non-goal (all independently re-verified via direct diff/grep, not solely accepted from the executor's report), uses the repository's established result-shape and testing idioms, and carries genuine fail-before/pass-after regression evidence. 0 Blocking, 0 Major, 2 Minor (both non-gating: a pre-existing-pattern message-classification nuance in the malformed-tag path, and a disclosed/justified 2-test-file scope extension), 2 Info.
