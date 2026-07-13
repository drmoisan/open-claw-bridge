# installer-image-version-alignment (Spec)

- **Issue:** #147
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-11T20-15
- **Status:** Ready for review
- **Version:** 0.2

## Context
The installer can stage a Control UI (openclaw/core) container image whose version does not match the gateway (openclaw/agent) container image, producing a broken runtime where the two containers disagree on their contract version.

Environment:
- OS/version: Windows 11 (installer host); Docker Desktop compose runtime
- Python version: n/a (PowerShell installer surface)
- Command/flags used: `.\Install.ps1` against a bundle produced by `scripts/Publish.ps1`
- Data source or fixture: bundled `docker/docker-compose.yml` (version-pinned `image:` lines) and `docker/openclaw-images.tar` (combined `docker save` of the two images at versioned and `pre-mvp` tags)

Impact / Severity:
- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low


## Repro & Evidence
Steps to Reproduce:
1. Produce or hand-assemble an install bundle in which the staged `docker-compose.yml` `image:` tag for one service (for example `openclaw/core`) resolves to a different version than the other service (`openclaw/agent`), or in which the loaded image tar contains a version that does not match the compose `image:` tags.
2. Run `.\Install.ps1` from the bundle.
3. Observe that the installer loads the images and starts compose without detecting the cross-image version mismatch.

Expected:
The installer staging/validation surface should detect a version mismatch between the Control UI (openclaw/core) image and the gateway (openclaw/agent) image (and between the compose-pinned tags and the images actually available to load), fail fast with an explicit remediation message, and never start a compose stack in which the two container images disagree on version.

Actual:
The installer stages and starts a mismatched image pair without a version-alignment guard. There is no validation function in `OpenClawContainerValidation` that asserts image-version alignment, and `Install.ps1` performs no such check before `Invoke-DockerImageLoad` / `Invoke-ComposeUp`. The result is a runtime in which the Control UI and gateway containers run at different versions.

Logs / Screenshots:
- [ ] Attached minimal logs or screenshot
- Snippet: (to be captured during research/repro)


## Scope & Non-Goals
- In scope:
  - `scripts/Install.ps1` — a new local helper function and one new guard call in Stage 9, placed immediately after `$ComposeFilePath` is resolved and before `Invoke-DockerImageLoad`.
  - `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` — two new pure functions, `ConvertFrom-OpenClawImageReference` and `Test-OpenClawImageVersionAligned`, added to `Export-ModuleMember`.
  - `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` — `FunctionsToExport` updated to include the two new functions. This is the manifest sibling of the `.psm1` and is part of "the module," not a scope extension.
  - `tests/scripts/**` — new and extended Pester coverage for the above (see Test Strategy).
- Out of scope / non-goals:
  - Any change to `scripts/Publish.ps1` or `scripts/Publish.Docker.psm1` (the publish-side staging that produces the compose file and image tar). The publish path already threads a single `$Version` through both image builds and the compose rewrite, and issue #147's stated scope does not include it.
  - Bundling `scripts/powershell/modules/OpenClawContainerValidation/**` into the published install bundle, or modifying `Copy-InstallScriptsIntoBundle` / `Publish.Helpers.psm1` to add it to the staged-file list.
  - Retrofitting the four pre-existing direct `docker` call sites in `Install.Helpers.psm1` (`docker info`, `compose up -d`, `compose ps`, `compose down`) onto the `Invoke-OpenClawDockerCommand` wrapper seam. This was already an explicit non-goal preserved from #142 (invariant 4).
  - Adding a post-`docker load` `docker image inspect` cross-check (mismatch vector (b) in the research). This is already substantially mitigated by the existing `pull_policy: never` + `Invoke-ComposeUp` throw-on-nonzero-exit behavior established by #142; re-implementing it would require a new docker call in `Install.Docker.psm1`, which is out of this issue's stated 3-file scope.
  - Wiring a runtime, docker-backed probe into `scripts/Invoke-OpenClawContainerPathValidation.ps1` (the standalone post-install diagnostic script). The research flags this as an optional, separately-approved extension; it is not part of this fix.
  - Modifying `scripts/Install.Docker.psm1` or the tracked `docker-compose.yml`.
- Explicitly excluded systems, integrations, or datasets:
  - The MSIX packaging/signing pipeline (not touched by this fix).
  - CI workflow files under `.github/workflows/**` and `scripts/benchmarks/**` (not touched by this fix; the `modified-workflow-needs-green-run` and benchmark-baseline provenance rules do not apply).

## Root Cause Analysis
- Image build/save/compose-rewrite occurs in `scripts/Publish.ps1` and `scripts/Publish.Docker.psm1` (versioned + `pre-mvp` tags for both `openclaw/core` and `openclaw/agent`; compose `image:` lines rewritten to the versioned tag).
- Image load and compose start occur in `scripts/Install.ps1` (Stage 9) via `scripts/Install.Docker.psm1` (`Invoke-DockerImageLoad`).
- There is no image-version-alignment guard on the staging/validation surface.
- Fix must stay consistent with prior container-validation fixes #142 (installer-docker-images-not-bundled) and #144 (container-validation-stray-v1-and-env-target) and must not regress them.
- Fix scope is confined to: `scripts/Install.ps1`, `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`, and tests under `tests/scripts`.


## Proposed Fix

### Design summary (what changes where):

Two coordinated additions close the gap identified by the root-cause analysis:

1. **`OpenClawContainerValidation.psm1` gains two pure functions** — `ConvertFrom-OpenClawImageReference` (parses a `repo:tag` string) and `Test-OpenClawImageVersionAligned` (compares the Control UI and gateway image tags against the resolved bundle version and returns a `Get-OpenClawValidationResult`-shaped object). These satisfy the issue's named scope (`OpenClawContainerValidation.psm1`) and give the parse/compare logic first-class, direct-import unit tests, following the module's existing `Category`/`Name`/`Details` result-shape convention.
2. **`Install.ps1` Stage 9 gains a small, self-contained static guard**, invoked before `Invoke-DockerImageLoad`, that reads the staged `docker-compose.yml` as text, extracts each service's pinned `image:` tag via a minimal regex scan, and compares both tags against `$ResolvedVersion` (already resolved and `[System.Version]`-validated earlier in the script). A mismatch throws immediately, naming both image references, both extracted tags, `$ResolvedVersion`, and `$ComposeFilePath`, before any docker state changes in this stage.

**Explicitly ratified design decision — `Install.ps1` does not import `OpenClawContainerValidation.psm1`.** Research (Section 7.1) confirmed that `scripts/powershell/modules/OpenClawContainerValidation/**` is not part of the fixed set of files `Copy-InstallScriptsIntoBundle` (`Publish.Helpers.psm1`) stages into a published bundle — only `Install.ps1`, `Uninstall.ps1`, `Install.Helpers.psm1`, `Install.Preflight.psm1`, and `Install.Docker.psm1` are copied. A Pester suite that invokes `Install.ps1` from its repo location would not detect an `Import-Module` of the container-validation module as a defect, because the sibling `scripts/powershell/modules/` directory still exists there — but the real installed bundle (`artifacts/publish/<version>/Install.ps1`) has no such subtree and would throw a missing-module error at runtime. This mirrors the self-containment invariant already established for `Install.Docker.psm1` (#142, invariant 3). Consequently `Install.ps1`'s guard is implemented as a small, self-contained local helper that duplicates the pure tag-vs-version comparison rather than calling into `Test-OpenClawImageVersionAligned`. This duplication is intentional and bounded (~10 lines), not accidental copy-paste; it is called out here so reviewers evaluate it against the deployment constraint above rather than the general reuse guidance in `.claude/rules/general-code-change.md`.

### Boundaries and invariants to preserve:

From #142 (installer-docker-images-not-bundled):
1. The tracked repo `docker-compose.yml` keeps both `build:` blocks and `openclaw/*:pre-mvp` `image:` tags (dev `--build` workflow). This fix does not touch that file.
2. `New-ManifestEntry`'s size field remains `[long]`. Not touched by this fix, but not to be regressed by any adjacent edit.
3. `Install.Docker.psm1` must not import any other repo module. This fix does not modify `Install.Docker.psm1` and, by the same principle, `Install.ps1`'s new guard does not import `OpenClawContainerValidation.psm1` either (see Design summary above).
4. The four pre-existing direct `docker` call sites in `Install.Helpers.psm1` remain un-retrofitted onto the `Invoke-OpenClawDockerCommand` wrapper seam; this fix does not touch `Install.Helpers.psm1`.
5. `pull_policy: never` plus versioned `image:` tags in the bundle compose remain intact; the new guard must not require or assume `pre-mvp` is present in a staged bundle's compose file — if it is present where a version is expected, that is itself a failure (see Technical specifications, edge case 1).
6. The bundle-staging list in `Copy-InstallScriptsIntoBundle` (`Install.ps1, Uninstall.ps1, Install.Helpers.psm1, Install.Preflight.psm1, Install.Docker.psm1`) is not modified; `OpenClawContainerValidation.psm1` is not added to it.

From #144 (container-validation-stray-v1-and-env-target):
1. `Invoke-OpenClawHostAdapterInContainerProbe`'s `/status` endpoint (no `/v1` segment) is unchanged by this fix.
2. `Invoke-OpenClawContainerPathValidation.ps1`'s default `-EnvFilePath` resolution chain (`Get-OpenClawOperatorEnvFilePath` / `Resolve-OpenClawDefaultEnvFilePath`) is unchanged by this fix.
3. `Test-OpenClawGatewayTokenInContainer` and `Test-OpenClawGatewayTokenPresence` remain distinct, both exported, both still used by the diagnostic script.
4. The `AgentDashboard` probe's `ExpectedCondition` wording, which disclaims operator-authentication verification, is unchanged.
5. Any new test file that imports the module through the shared fixture helper (`tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`, `Import-OpenClawContainerValidationModule`) must rely on that helper's existing `-Global` `Import-Module` pattern (the #144 R4 fix) so `Mock -ModuleName OpenClawContainerValidation` continues to intercept calls correctly.

### Dependencies or blocked work:
- None. This fix has no dependency on the publish-side surface, on bundling changes, or on any other in-flight issue.

### Implementation strategy (what changes, not sequencing):

#### Files/modules to change:
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` — add `ConvertFrom-OpenClawImageReference`, `Test-OpenClawImageVersionAligned`; extend `Export-ModuleMember`.
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` — extend `FunctionsToExport`.
- `scripts/Install.ps1` — add a local `Get-ComposeServiceImageTag` helper and an `Assert-ComposeImageVersionAligned`-style guard call in Stage 9.

#### Functions/classes/CLI commands impacted:
- New: `ConvertFrom-OpenClawImageReference`, `Test-OpenClawImageVersionAligned` (module, exported, pure).
- New: `Get-ComposeServiceImageTag`, and the guard invocation in `Install.ps1` Stage 9 (script-local, not exported — `Install.ps1` is an entry point, not a module).
- Unchanged: `Invoke-DockerImageLoad`, `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Get-ManifestVersion`, `Test-ManifestIntegrity` — the guard is inserted between existing calls without modifying their signatures or behavior.

#### Data flow and validation changes:
- `Install.ps1` already resolves `$ResolvedVersion` at line 306 (via `Get-ManifestVersion`, itself `[System.Version]`-validated) and computes `$ComposeFilePath` at Stage 9 (existing line 430). The guard reads `$ComposeFilePath`'s content once via `Get-Content -LiteralPath`, extracts the `openclaw/core` and `openclaw/agent` `image:` tags, and compares each against `$ResolvedVersion` before `Invoke-DockerImageLoad` runs. This is new instrumentation surface: `Install.ps1` currently never reads the compose file's content anywhere else in the script.
- The guard runs only inside the existing `if (-not $SkipDocker) { ... }` block, so `-SkipDocker` continues to bypass all docker-related staging exactly as it does today.

#### Error handling and logging updates:
- A mismatch (either service's tag absent, malformed, or not equal to `$ResolvedVersion`) throws a single aggregated, actionable error naming: both raw `image:` values, both extracted tags, `$ResolvedVersion`, and `$ComposeFilePath` — consistent with this repo's fail-fast, no-silent-catch policy (`.claude/rules/general-code-change.md`).
- A staged compose file missing an `image:` line for either service (drift/malformed bundle) throws a distinct, specific error rather than silently skipping the check — consistent with `Convert-ComposeToBundleCompose`'s own drift-guard precedent (`Publish.Docker.psm1:283-287`).
- No new logging levels are introduced; the guard uses `throw`, matching the surrounding Stage 9 code's existing error-handling style.

#### Rollback/feature-flag considerations (if applicable):
- No feature flag is introduced. The guard is a fail-fast correctness check with no intended false-positive path against a correctly-produced bundle; a rollback is a revert of the two production-file diffs.

### Technical specifications (interfaces/contracts):

#### Inputs/outputs and formats:

Module functions (design-level signatures, `OpenClawContainerValidation.psm1`):

```
ConvertFrom-OpenClawImageReference -ImageReference <string>
  -> [pscustomobject] @{ Repository = <string>; Tag = <string> }
```
Splits on the last `:` in `-ImageReference`. When no `:` is present, returns `Tag = ''` (not a thrown error) so callers can fold a missing tag into the standard `IsExpected/Summary` result shape rather than an exception, matching every other function in this module (no existing probe throws for a "false" condition).

```
Test-OpenClawImageVersionAligned
  -CoreImageReference <string> -AgentImageReference <string> -ExpectedVersion <string>
  -> Get-OpenClawValidationResult-shaped [pscustomobject]
     Category = 'Container'; Name = 'ImageVersionAlignment'
```
`IsExpected` is `$true` only when both parsed tags equal `-ExpectedVersion` by exact string comparison AND both parsed tags independently validate as well-formed 4-part versions via `[System.Version]::TryParse` (mirroring `Get-ManifestVersion`'s validation pattern, `Install.Helpers.psm1:43`). `Details` includes both raw image references, both parsed tags, and `-ExpectedVersion`, so a failing result carries concrete evidence without requiring the caller to re-parse anything.

`Install.ps1`-local helper (design-level signature, not exported — script scope only):

```
Get-ComposeServiceImageTag -ComposeContent <string[]> -ImageRepository <string>
  -> [string] (the tag) or throws if no matching image: line is found
```
Scans `-ComposeContent` for a line matching `^\s{4}image:\s*<ImageRepository>:(.+)$`, anchored on the literal `openclaw/core` / `openclaw/agent` repository prefixes — the only two `image:` values ever present in this compose file. No full indentation/service-context state machine is required (unlike `Convert-ComposeToBundleCompose`) because this is a read-only scan over a file that, post-#142, has exactly one `image:` line per service and no ambiguous nested `build:` context to skip.

#### Required configuration keys and defaults:
- None. No new configuration keys, environment variables, or manifest fields are introduced. The guard consumes values (`$ResolvedVersion`, `$ComposeFilePath`) already produced by existing Stage 2/Stage 9 logic.

#### Backward-compatibility expectations:
- **Version-comparison semantics:** exact string equality of the extracted tag against `$ResolvedVersion` / `-ExpectedVersion`, not `[System.Version]` numeric equality. Both values originate from the same literal interpolation (`"$repo:$Version"`) in a correctly-produced bundle (`Convert-ComposeToBundleCompose`, `Publish.Docker.psm1:274`), so string equality is the simplest correct check and avoids silently accepting a differently-formatted-but-numerically-equal string that would itself indicate a formatting bug elsewhere. `[System.Version]::TryParse` is used only as a secondary, message-quality check to distinguish "malformed tag" from "version mismatch" in the failure text.
- **4-part version format:** the expected format is `^\d+\.\d+\.\d+\.\d+$`, identical to the pattern enforced throughout `Publish.ps1`/`Publish.Docker.psm1`/`Publish.Env.psm1` and validated on read by `Get-ManifestVersion`.
- **`pre-mvp` floating-tag edge case:** the bundle compose emitted by `Convert-ComposeToBundleCompose` never emits `pre-mvp` post-#142; every `image:` line it writes is `<repo>:$Version` with `$Version` matching the 4-part pattern. If the guard observes `pre-mvp` in a staged bundle's compose file, that is itself anomalous (a hand-edited/corrupted bundle, or a case predating #142) and must fail, not be tolerated as version-equivalent.
- **Missing-tag edge case:** a compose `image:` value with no `:` separator, or an empty tag capture, fails with a message that distinguishes "missing tag" from "mismatched version."
- **Malformed-version edge case:** a tag that does not match the 4-part pattern (e.g. `1.2.3`, `v1.2.3.0`, `1.2.3.a`, `latest`) fails via `[System.Version]::TryParse` returning `$false`; the message names the literal malformed string.
- **Matching-but-wrong edge case:** both tags equal each other but neither equals `$ResolvedVersion` — this must still fail. The guard compares each tag against the single trusted `$ResolvedVersion`, not merely against each other, so a same-wrong-version case is caught (mismatch mechanism (c) in the research), not just cross-service disagreement (mechanism (a)).
- **Not re-implemented, documented as already covered:** mismatch mechanism (b) — the loaded tar's contents disagreeing with the compose-pinned tag after `docker load` succeeds — is already mitigated by the existing `pull_policy: never` (set by `Convert-ComposeToBundleCompose`, #142) combined with `Invoke-ComposeUp`'s existing throw-on-nonzero-exit behavior (`Install.Helpers.psm1:302-327`): if the pinned ref is not present locally after `docker load`, `docker compose up` fails immediately with an explicit image-not-found error rather than silently starting a wrong image. This fix does not add a redundant post-load `docker image inspect` check, since doing so would require modifying `Install.Docker.psm1`, which is outside this issue's 3-file scope.

#### Performance constraints (latency/throughput/memory):
- The guard adds one `Get-Content` read of a small text file (the staged `docker-compose.yml`, typically well under 100 lines) and two regex-line scans, executed once per install run before any docker call. This is negligible relative to the existing `docker load` / `docker compose up` / health-wait steps in the same stage; no measurable latency, throughput, or memory impact is expected, and no performance budget is waived.

## Assumptions, Constraints, Dependencies
- Assumptions (environment, data, access):
  - The staged `docker-compose.yml` always contains exactly one `image:` line for `openclaw/core` and one for `openclaw/agent`, each at 4-space indentation, per the structural drift guard already enforced at publish time by `Convert-ComposeToBundleCompose` (`Publish.Docker.psm1:283-287`).
  - `$ResolvedVersion` is available in `Install.ps1`'s scope at Stage 9 (resolved at existing line 306) and has already passed `[System.Version]::TryParse` validation via `Get-ManifestVersion`.
  - Byte-level bundle integrity (file size/SHA-256 against `manifest.json`) has already been verified by `Test-ManifestIntegrity` at Stage 2; this fix adds a semantic/cross-file check that integrity verification does not perform.
- Constraints (budget, performance, compatibility):
  - Per-issue scope constraint: exactly `scripts/Install.ps1`, `OpenClawContainerValidation.psm1` (+ its `.psd1` manifest sibling), and `tests/scripts/**`.
  - `Install.ps1` must remain import-free of `OpenClawContainerValidation.psm1` (see Design summary and Boundaries above) because the module is not part of the bundle-staged file set.
  - PowerShell 7+ compatibility and the repo's PSScriptAnalyzer settings apply to all new code (`.claude/rules/powershell.md`).
- External dependencies (services, libraries, releases):
  - None. No new third-party dependency is introduced; both changes use only built-in PowerShell (`Get-Content`, regex, `[System.Version]`) already used elsewhere in these files.

## Data / API / Config Impact
- User-facing or API changes:
  - Installer behavior change: a bundle whose staged `docker-compose.yml` pins a Control UI (`openclaw/core`) tag and a gateway (`openclaw/agent`) tag that disagree with each other, or with the bundle's own resolved version, now causes `Install.ps1` to fail fast with an explicit remediation message instead of silently starting a mismatched compose stack.
  - No new CLI flags or parameters are added to `Install.ps1`.
- Data or migration considerations:
  - None. No persisted data format, manifest schema, or on-disk artifact changes.
- Logging/telemetry updates (if any):
  - The new guard's failure path uses `throw` with a message naming both image references, both extracted tags, `$ResolvedVersion`, and `$ComposeFilePath`, consistent with the surrounding Stage 9 error-handling style. No new structured telemetry is introduced.
- Compatibility notes (CLI flags, config schemas, versioning):
  - `-SkipDocker` continues to bypass the new guard along with the rest of Stage 9's docker steps, since the guard lives inside the existing `if (-not $SkipDocker) { ... }` block.
  - `OpenClawContainerValidation.psd1`'s `FunctionsToExport` list is a superset extension (two additions); no existing export is removed or renamed.

## Test Strategy
Seeded from issue:

- [ ] Unit coverage areas: a new image-version-alignment validation function in `OpenClawContainerValidation` (version parsing/comparison as pure logic), plus its wiring in `Install.ps1`.
- [ ] Integration scenario to retest: installer aborts on a mismatched image/compose pair; installer proceeds on a matched pair; #142/#144 behaviors remain green.
- [ ] Manual verification notes: confirm fail-fast message names both image versions and the remediation step.

- Regression tests to add or update:
  - New: `tests/scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.ImageVersionAlignment.Tests.ps1` — direct-import unit tests for `ConvertFrom-OpenClawImageReference` and `Test-OpenClawImageVersionAligned`, mirroring the existing `OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1` pattern (`BeforeDiscovery` + `BeforeAll` re-import via the `.psd1`, no docker/HTTP seam since both functions are pure).
  - Extend: `tests/scripts/Install.DockerStage.Tests.ps1` — add a `Get-Content` mock branch matching `*docker-compose.yml` returning a fixture compose-line array (the current mock, lines 92-98, has no such branch), and a new `Context 'image version alignment guard'`. If the file grows close to the 500-line cap after this addition, split the new context into a sibling `tests/scripts/Install.ImageVersionAlignment.Tests.ps1` reusing the same `BeforeAll`/`BeforeEach` harness; at 203 existing lines a single-file extension is expected to fit, so the split is a fallback, not the default.
- Unit tests (Pester) for the fixed behavior and boundaries:
  - `ConvertFrom-OpenClawImageReference`: splits `repo:tag` correctly; returns `Tag = ''` (not a throw) when no `:` is present; handles a repository name containing `/`.
  - `Test-OpenClawImageVersionAligned`: `IsExpected = $true` when both tags equal `-ExpectedVersion` and both are well-formed 4-part versions; `IsExpected = $false` with an actionable `Summary`/`Details` for: one tag mismatched, both tags mismatched but equal to each other, one tag equal to `pre-mvp`, one tag with no `:` separator (empty tag), one tag that is a malformed version string (`1.2.3`, `v1.2.3.0`, `1.2.3.a`, `latest`).
  - `Install.ps1` guard (via `Install.DockerStage.Tests.ps1` / its split sibling): matched versions proceed to `Invoke-DockerImageLoad` then `Invoke-ComposeUp` in order; mismatched core-vs-agent tags throw before `Invoke-DockerImageLoad` is called (assert via `$global:InstallTestCalls -notcontains 'Invoke-DockerImageLoad'` after catching the throw); a tag matching neither service's expected version throws; a compose file missing an `image:` line for a service throws a distinct error; the guard is skipped entirely under `-SkipDocker` (mirrors the existing `-SkipDocker` `Context`, lines 174-201).
- Edge cases and negative scenarios (invalid inputs, missing data, boundary values):
  - `pre-mvp` present where a version is expected — must fail (post-#142 invariant: bundle compose never pins the floating tag).
  - Missing tag (no `:` in the `image:` value) — must fail with a message distinct from "version mismatch."
  - Malformed 4-part version string — must fail via `[System.Version]::TryParse` returning `$false`, naming the literal malformed string.
  - Both tags equal each other but not equal to `$ResolvedVersion` — must fail (guards against comparing only core-vs-agent instead of each side against the trusted `$ResolvedVersion`).
  - Compose file present but with no matching `image:` line for one service (malformed/drifted bundle) — must throw a distinct, actionable error.
- Error handling and logging verification:
  - Assert the thrown/returned message text includes both raw image references, both extracted tags, `$ResolvedVersion`, and (for the `Install.ps1` guard) `$ComposeFilePath`.
  - Assert no docker state-changing call (`Invoke-DockerImageLoad`, `Invoke-ComposeUp`) executes when the guard throws.
- Coverage impact and targets for changed lines/modules:
  - Line coverage >= 85% and branch coverage >= 75% on all changed lines in `OpenClawContainerValidation.psm1` and `Install.ps1`, per `.claude/rules/quality-tiers.md` and `.claude/rules/general-unit-test.md` (uniform across tiers; no regression on changed lines).
- Toolchain commands to run (format → lint → type-check → test):
  - `mcp__drm-copilot__run_poshqc_format` → `mcp__drm-copilot__run_poshqc_analyze` (type-check stage is not applicable to PowerShell; skip) → `mcp__drm-copilot__run_poshqc_test` (Pester v5, repo config `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`). Restart from format if any stage fails or auto-fixes files, per `.claude/rules/general-code-change.md`.
  - Full-suite regression run (not a scoped subset) including `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Force.Tests.ps1`, `tests/scripts/Install.Docker.Tests.ps1`, all six `tests/scripts/Invoke-OpenClawContainerPathValidation.*.Tests.ps1` split files, `tests/scripts/Publish.Docker.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1` — none of these files' production targets change under this fix, but they exercise shared harnesses/fixtures (notably `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`) that the new test files also depend on.
- Manual validation steps (if required):
  - None required. Per research Section 10, this fix's implementation and verification are fully automatable: all new logic is pure PowerShell string/regex parsing plus the already-established Pester-mockable seams, with no third-party UI, docker-registry interaction, signing step, or workflow/CI-config change involved.


## Acceptance Criteria
- [x] `Install.ps1` Stage 9 detects a cross-service compose tag mismatch (`openclaw/core` tag != `openclaw/agent` tag) before `Invoke-DockerImageLoad` runs, and aborts with an error naming both image references, both tags, `$ResolvedVersion`, and `$ComposeFilePath`.
- [x] `Install.ps1` Stage 9 detects the case where both compose tags agree with each other but disagree with `$ResolvedVersion`, and aborts with the same actionable error content.
- [x] `Install.ps1` Stage 9 proceeds to `Invoke-DockerImageLoad` and `Invoke-ComposeUp` (in that order) when both compose tags equal `$ResolvedVersion`.
- [x] The guard runs only inside the existing `-SkipDocker`-gated block, so `-SkipDocker` continues to bypass it exactly as it bypasses the rest of Stage 9's docker steps.
- [x] `ConvertFrom-OpenClawImageReference` and `Test-OpenClawImageVersionAligned` are added to `OpenClawContainerValidation.psm1`, exported via `Export-ModuleMember`, and listed in `OpenClawContainerValidation.psd1`'s `FunctionsToExport`.
- [x] `Test-OpenClawImageVersionAligned` correctly fails (with a distinguishing message) for each named edge case: `pre-mvp` floating tag, missing `:` separator, malformed 4-part version string, and same-wrong-version-on-both-sides.
- [x] Unit tests exist and pass at `tests/scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.ImageVersionAlignment.Tests.ps1` covering `ConvertFrom-OpenClawImageReference` and `Test-OpenClawImageVersionAligned`.
- [x] Guard tests exist and pass in `tests/scripts/Install.DockerStage.Tests.ps1` (or a sibling `tests/scripts/Install.ImageVersionAlignment.Tests.ps1` if split for line-count reasons), covering the matched, mismatched, and `-SkipDocker` scenarios.
- [x] All #142 invariants listed in Boundaries and invariants to preserve remain green (tracked repo `docker-compose.yml` unchanged; `Install.Docker.psm1` self-containment preserved; four direct `docker` call sites in `Install.Helpers.psm1` remain un-retrofitted; bundle-staging list in `Copy-InstallScriptsIntoBundle` unchanged).
- [x] All #144 invariants listed in Boundaries and invariants to preserve remain green (`/status` endpoint wording; default `-EnvFilePath` resolution chain; distinct `Test-OpenClawGatewayTokenInContainer`/`Test-OpenClawGatewayTokenPresence`; `AgentDashboard` auth-disclaimer wording; shared fixture's `-Global` `Import-Module` pattern).
- [x] `Install.ps1` contains no `Import-Module` reference to `OpenClawContainerValidation.psm1` or its `.psd1` (ratified design decision: the module is not staged into published bundles).
- [x] Full PowerShell toolchain passes in a single pass: PoshQC format, PoshQC analyze (lint), and Pester test with coverage, on `scripts/Install.ps1`, `OpenClawContainerValidation.psm1`/`.psd1`, and all new/updated test files.
- [x] Line coverage >= 85% and branch coverage >= 75% on changed lines in both production files, with no regression on previously-covered lines.
- [x] Full regression run of the test files named in Test Strategy (`Install.Tests.ps1`, `Install.Force.Tests.ps1`, `Install.Docker.Tests.ps1`, the six `Invoke-OpenClawContainerPathValidation.*.Tests.ps1` files, `Publish.Docker.Tests.ps1`, `Publish.Tests.ps1`, `Publish.Helpers.Tests.ps1`) passes with no new failures.

## Risks & Mitigations
- Technical or operational risks:
  - A future edit to `Install.ps1` could re-introduce an `Import-Module` of `OpenClawContainerValidation.psm1` (for example, to reduce the intentional duplication) without realizing the module is not bundle-staged, silently reintroducing the class of defect described in Design summary above. Because a repo-local Pester run would not catch this (the sibling module directory exists in-repo), this risk is not fully closed by the automated toolchain alone.
  - The regex-based `Get-ComposeServiceImageTag` scan assumes the compose file's structural shape enforced by `Convert-ComposeToBundleCompose` (4-space `image:` indentation, no build-block ambiguity). A publish-side change to that indentation convention, made without updating this guard, would cause the guard to throw a "no matching image: line found" error on an otherwise-valid bundle.
  - Intentional duplication of the pure tag-vs-version comparison between the module function and the `Install.ps1`-local helper creates two places that must be kept semantically consistent if the comparison rules change in the future.
- Mitigations and rollbacks:
  - The Acceptance Criteria explicitly include a check that `Install.ps1` contains no `Import-Module` reference to the container-validation module, giving feature-review a concrete, repeatable check to catch the re-introduction risk even though the Pester suite cannot.
  - The regex-scan assumption is documented here and in the Technical specifications section so a future publish-side formatting change is a visible, cross-referenced risk rather than a silent coupling.
  - Rollback is a straightforward revert of the two production-file diffs (`Install.ps1`, `OpenClawContainerValidation.psm1`/`.psd1`) plus their associated test files, since the guard is additive and does not alter any existing function's signature or behavior.

## Rollout & Follow-up
- Release/rollout steps:
  - Standard PR merge through the existing branch-protection and feature-review process; no phased rollout, feature flag, or migration step is required since the change is a fail-fast validation addition with no schema or data migration.
  - No workflow or CI-config changes are included, so no green-workflow-run gate applies beyond the standard required checks.
- Post-fix monitoring or clean-up tasks:
  - None required beyond the standard toolchain and regression gates listed in Test Strategy and Acceptance Criteria.
  - If a future issue relaxes the 3-file scope constraint (for example, to bundle `OpenClawContainerValidation.psm1` into published installs), revisit the intentional duplication noted in Design summary and consider consolidating `Install.ps1`'s local helper onto `Test-OpenClawImageVersionAligned` directly.
- Links: issue #147 (`docs/features/active/2026-07-11-installer-image-version-alignment-147/issue.md`); prior fixes #142 (`docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/`) and #144 (`docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/`); research findings (`docs/features/active/2026-07-11-installer-image-version-alignment-147/research/research-findings.2026-07-11T20-15.md`).
