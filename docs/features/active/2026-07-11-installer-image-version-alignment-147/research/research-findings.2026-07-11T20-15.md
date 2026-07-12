# Research Findings — installer-image-version-alignment (Issue #147)

- Date: 2026-07-11T20-15
- Scope of change under research: `scripts/Install.ps1`, `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`, tests under `tests/scripts`
- This document is research only. No production code was modified while producing it.

## 1. Root Cause

**Statement:** No code on the install or diagnostic path asserts that (a) the two compose services' `image:` tags for `openclaw-core` and `openclaw-agent` name the same version, or (b) that shared version equals the resolved bundle version (`manifest.json`'s `version` field, read into `Install.ps1`'s `$ResolvedVersion`). The absence of this check is explicit, not incidental: `Test-ManifestIntegrity`'s own docstring states the cross-check is deliberately deferred to a caller that never implements it.

Evidence:
- `scripts/Install.Helpers.psm1:58-61` (`Test-ManifestIntegrity` docstring): *"The top- level version value itself is not compared against any external value here; that cross-check is the orchestrator's responsibility."* — i.e., `Install.ps1` is the intended cross-check owner, and it currently performs none.
- `scripts/Install.ps1:429-438` (Stage 9): `$ComposeFilePath` and `$ImageTarPath` are computed, then `Invoke-DockerImageLoad` and `Invoke-ComposeUp` run back-to-back with no intervening version-alignment assertion. `$ResolvedVersion` was already resolved at `scripts/Install.ps1:306` (`Get-ManifestVersion`) and is available in scope at Stage 9 but is never compared against the staged compose file's `image:` tags.
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` contains no image-version function of any kind (confirmed by full-file read); its `Export-ModuleMember` list (lines 437-452) has 14 entries, none of which parse or compare an image reference.
- `scripts/Invoke-OpenClawContainerPathValidation.ps1`'s `Get-OpenClawContainerInspection` (lines 96-131) already retrieves each container's `Config.Image` via `docker container inspect`, and `Invoke-OpenClawContainerValidation` (lines 133-169) stores it in `Details.image` per container — but nothing cross-compares the two containers' `image` values against each other or against a version. The raw data needed for a runtime cross-check already flows through the script; only the comparison is missing.

**Why mismatch does not normally occur today (and why it still can):** Within one `Publish.ps1` run, `Invoke-PublishDockerStage` (`scripts/Publish.Docker.psm1:323-363`) threads a single `$Version` parameter through both `Build-OpenClawDockerImage` calls (lines 354-355), through `Save-OpenClawDockerImage` (line 358), and through `Write-BundleCompose`/`Convert-ComposeToBundleCompose` (lines 360-362, and the `$Version` interpolation at line 274: `"    image: $($imageForService[$current]):$Version"`). This is why a bundle produced by an unmodified, single, successful `Publish.ps1` run is internally consistent — but the consistency is an **implicit, non-enforced invariant of one code path**, not a checked invariant of the install or diagnostic path. `Test-ManifestIntegrity` (`scripts/Install.Helpers.psm1:50-107`) verifies every bundle file's size and SHA-256 against `manifest.json` (byte-level integrity), but performs no semantic/cross-file check, so a bundle whose `docker/docker-compose.yml` was hand-edited, copied in from a different published version, or produced by a future publish-side regression that decouples the two builds' `$Version` values would pass Stage 2 untouched as long as its bytes match its own `manifest.json`. Issue #147's own repro steps name this directly: *"Produce or hand-assemble an install bundle in which the staged docker-compose.yml image tag for one service ... resolves to a different version than the other service ... or in which the loaded image tar contains a version that does not match the compose image tags."*

## 2. Publish-Side Surface (context, not in fix scope)

`scripts/Publish.ps1` Stage 3b (lines 209-214) calls `Invoke-PublishDockerStage -RepoRoot $RepoRoot -BundleDockerDir $DockerBundleDir -Version $Version -Configuration $Configuration -EnvMap (...)`.

`scripts/Publish.Docker.psm1`:
- `Build-OpenClawDockerImage` (110-171): builds `openclaw/core:$Version` + `openclaw/core:pre-mvp` (core) or `openclaw/agent:$Version` + `openclaw/agent:pre-mvp` (agent), one `docker build` per `-Kind`, always via the same `$Version` string passed by the caller.
- `Save-OpenClawDockerImage` (173-207): one `docker save -o <tar> openclaw/core:$Version openclaw/core:pre-mvp openclaw/agent:$Version openclaw/agent:pre-mvp` — all four refs into `<BundleDockerDir>/openclaw-images.tar`.
- `Convert-ComposeToBundleCompose` (209-290): pure line-scanner. Tracks the current service via 2-space-indent headers (`openclaw-core:`/`openclaw-agent:`), removes each service's 4-space `build:` block, and rewrites the 4-space `image:` line to `"    image: $($imageForService[$current]):$Version"` followed by `pull_policy: never`. Throws (`line 285`) if either service does not have exactly one `build:` and one `image:` key — a fail-fast drift guard, but only for *structural* drift, not for a *value* mismatch between the two services (both always receive the same `$Version` parameter by construction of this single function call).
- `Invoke-PublishDockerStage` (323-363): the facade; both `Build-OpenClawDockerImage` calls and the one `Write-BundleCompose` call all receive the identical `$Version` value from `Invoke-PublishDockerStage`'s own parameter — the single source of truth is one call frame.
- `Export-ModuleMember` (365-373): 7 functions.

Docker-compose build-vector cross-reference (per this module's own header comment, lines 13-20): core build args mirror `docker-compose.yml:5-10`/image line 11; agent build args mirror `docker-compose.yml:53-57`/image line 58. Confirmed against the current tracked `docker-compose.yml` (root of repo): core `build:`/`image:` at lines 5-11, agent at lines 53-58, both `image:` values `openclaw/*:pre-mvp` in the tracked (dev) file.

## 3. Install-Side Surface (fix wiring point)

`scripts/Install.ps1` Stage 9 (lines 429-438):
```
$ComposeFilePath = Join-Path $DestDockerDir 'docker-compose.yml'
if (-not $SkipDocker) {
    $ImageTarPath = Join-Path $DestDockerDir 'openclaw-images.tar'
    Invoke-DockerImageLoad -ImageTarPath $ImageTarPath
    Invoke-ComposeUp -DestDockerDir $DestDockerDir -ComposeFilePath $ComposeFilePath
    Wait-ComposeHealthy -ComposeFilePath $ComposeFilePath
}
```
Data available at this point: `$ResolvedVersion` (string, resolved at line 306 from `manifest.json`, already `[System.Version]`-validated by `Get-ManifestVersion`), `$ComposeFilePath` (staged, byte-verified by `Test-ManifestIntegrity` at Stage 2), `$ImageTarPath` (staged, byte-verified), `$DestDockerDir`. The compose file's *content* is never read by `Install.ps1` anywhere in the current script — this is new instrumentation surface.

`scripts/Install.Docker.psm1` exports only `Invoke-DockerExe` and `Invoke-DockerImageLoad` (lines 100-103); it performs a `docker load -i <tar>` and nothing else. It has no image-inspection capability and, per this issue's stated 3-file scope, is not a file this fix may modify.

`scripts/Install.Helpers.psm1` supplies `Get-ManifestVersion` (10-48), `Test-ManifestIntegrity` (50-107, code past line 100 continues the discrepancy-accumulation pattern only, not shown here but confirmed structurally consistent with a "collect all findings, throw once" style), `Invoke-ComposeUp` (302-327), `Wait-ComposeHealthy` (329-404), `Invoke-ComposeDown` (406-428). None of these touch image versions.

## 4. Concrete Mismatch Mechanisms (what to guard, with evidence)

Three theoretically distinct mismatch vectors, evaluated:

- **(a) Cross-service compose tag mismatch** — the `openclaw-core` and `openclaw-agent` `image:` lines in the staged `docker-compose.yml` name different versions. **Guard this.** This is exactly issue #147's stated repro step 1 and is fully detectable by static text inspection of the staged compose file before any docker call — no docker invocation required.
- **(b) Compose-pinned tags vs. what the tar actually contains** — `docker load` succeeds, but the loaded tar's layers/tags do not include the exact ref the compose file pins. This is already substantially mitigated by the existing design: `Write-BundleCompose` always sets `pull_policy: never` (established by issue #142; see `Convert-ComposeToBundleCompose` output, `pull_policy: never` inserted immediately after every rewritten `image:` line), so if the pinned ref is not present locally after `docker load`, `docker compose up` fails immediately with an explicit "image not found" / pull-denied error rather than silently starting a wrong image — `Invoke-ComposeUp` (`Install.Helpers.psm1:302-327`) already throws on non-zero exit. Recommend documenting this as an already-covered failure mode rather than re-implementing a redundant post-load `docker image inspect` check, which would require touching `scripts/Install.Docker.psm1` (out of this issue's stated scope) to add a new docker call.
- **(c) Compose/tar version vs. resolved bundle version** — both compose `image:` tags agree with each other but disagree with `manifest.json`'s `version` (`$ResolvedVersion`). **Guard this together with (a)** by comparing both compose tags against the single trusted `$ResolvedVersion` (already integrity-verified) rather than merely against each other: comparing against a single ground truth catches (a) as a side effect (two tags that agree with each other but not with `$ResolvedVersion` are still a failure) and additionally catches a same-wrong-version case that a bare (core-tag == agent-tag) check would miss.

**Recommendation:** implement (a)+(c) as one static pre-`docker load` guard in `Install.ps1` comparing each service's compose-pinned tag against `$ResolvedVersion`; treat (b) as already covered by the existing `pull_policy: never` + `Invoke-ComposeUp` throw-on-nonzero-exit behavior established by issue #142, and note it in the spec as a documented, not re-implemented, mitigation.

## 5. Container-Validation Module Patterns (for the new pure functions)

`OpenClawContainerValidation.psm1` established conventions every new function must follow:
- **Result shape**: every probe returns `Get-OpenClawValidationResult` (lines 149-177): `{ Category, Name, Target, Uri, IsExpected, HttpStatusCode, ExpectedCondition, Summary, ContentType, Details, ErrorMessage, BodyPreview }`. `Category` values already in use: `'Endpoint'` (default), `'Container'`, `'Configuration'`, `'Docker'`.
- **Docker seam**: `Invoke-OpenClawDockerCommand -ExecutablePath <string> -CommandArguments <string[]>` (184-209) — never call `docker`/`& docker` directly; wraps exit code + output + error into `{ Succeeded, ExitCode, Output, ErrorMessage }`.
- **Env-map parsing**: `Get-OpenClawEnvFileMap -EnvFilePath <string>` (216-235) — pure, blank-safe, comment-skipping `.env` parser returning a hashtable.
- **Export list**: `Export-ModuleMember -Function @(...)` (437-452) — every new public function must be added here and to the `.psd1` `FunctionsToExport` list (`OpenClawContainerValidation.psd1:10-25`), or the diagnostic script's `Import-Module` will not expose it.
- **Pure-vs-impure separation**: functions like `Get-OpenClawEndpointUri`, `Get-OpenClawPropertyValue`, `Get-OpenClawContentPreview`, `ConvertFrom-OpenClawJsonContent` are pure string/object transforms with no I/O; probe functions (`Invoke-OpenClawReadyzProbe`, `Test-OpenClawGatewayTokenPresence`, etc.) compose a pure transform + one I/O call + `Get-OpenClawValidationResult`. A new image-version function set should mirror this split: a pure parse/compare function plus (optionally, for the standalone diagnostic script only — see Section 7) an I/O-backed probe function.

`scripts/Invoke-OpenClawContainerPathValidation.ps1` orchestrates: imports the module via its `.psd1` (lines 39-45, guarded by `Get-Module` check so Pester's pre-imported module + `Mock -ModuleName` is not invalidated by a forced re-import), defines its own container-inspection helpers (`Get-OpenClawContainerInspection`, `Invoke-OpenClawContainerValidation`) locally (not in the module), and aggregates every probe into `$supportingDiagnostics` / `$result.OverallResult`. This script is **not** one of the three files this issue's stated scope names for modification.

## 6. Issue #142 and #144 Invariants That Must Not Regress

**#142 (installer-docker-images-not-bundled)** — confirmed by re-reading `spec.md` and the current on-disk state of `Publish.Docker.psm1`/`Install.Docker.psm1`/`docker-compose.yml`:
1. Tracked repo `docker-compose.yml` keeps both `build:` blocks and `openclaw/*:pre-mvp` `image:` tags (dev `--build` workflow) — confirmed unchanged in the current file (lines 5-11, 53-58).
2. `New-ManifestEntry` size field is `[long]`, not `[int]` (`Publish.Helpers.psm1:155` area) — must not regress to `[int]`.
3. `Install.Docker.psm1` self-containment: "must not import any other repo module" (module header, lines 9-12) — **directly relevant to this fix's design**, see Section 7 below.
4. The four pre-existing direct `docker` call sites in `Install.Helpers.psm1` (`docker info`, `compose up -d`, `compose ps`, `compose down`) remain un-retrofitted onto a wrapper seam (explicit non-goal); this fix must not force a seam change onto those call sites.
5. `pull_policy: never` + versioned `image:` tags in the bundle compose (Section 4(b) above) — must remain intact; the new guard must not require or assume the `pre-mvp` floating tag is present in the bundle compose (post-#142 the bundle compose never uses `pre-mvp`).
6. Bundle-staging list (`Copy-InstallScriptsIntoBundle`, `Publish.Helpers.psm1:161-`): exactly `Install.ps1, Uninstall.ps1, Install.Helpers.psm1, Install.Preflight.psm1, Install.Docker.psm1` — **`OpenClawContainerValidation.psm1` and its parent directory tree are not in this list.** This is a load-bearing fact for Section 7.

**#144 (container-validation-stray-v1-and-env-target)** — confirmed against current on-disk state:
1. `Invoke-OpenClawHostAdapterInContainerProbe` (`OpenClawContainerValidation.psm1:315-351`) curls `http://host.docker.internal:4319/status` (no `/v1` segment) — confirmed at line 322. The `ExpectedCondition` text also says `/status` (line 343), not `/v1/status`.
2. `Invoke-OpenClawContainerPathValidation.ps1` default `-EnvFilePath` resolution: `Get-OpenClawOperatorEnvFilePath` (module, 247-253) + `Resolve-OpenClawDefaultEnvFilePath` (module, 265-275), wired at script lines 248-251, falling back to `./.env` only when the deployed operator env file is absent — confirmed present and unchanged.
3. `Test-OpenClawGatewayTokenInContainer` (module, 402-435) — a distinct in-container check from the `.env`-file presence check (`Test-OpenClawGatewayTokenPresence`) — both present, both exported, both still used by the script (lines 262, 270).
4. `AgentDashboard` probe wording does not claim operator authentication (script lines 230-246; `ExpectedCondition` explicitly disclaims auth verification) — confirmed unchanged.
5. Shared test fixture `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` `Import-OpenClawContainerValidationModule` uses `-Global` on `Import-Module` (line 38) — this was the #144 remediation-cycle fix (R4/`3b0a2b3`) for an unscoped-`Mock` visibility failure; **any new test file that imports the module through a helper function living in another module must use the same `-Global` pattern**, or its `Mock -ModuleName OpenClawContainerValidation` calls will silently fail to intercept.

None of these invariants are threatened by the recommended design in Section 7, provided the new code does not import `OpenClawContainerValidation.psm1` from `Install.ps1` (see below) and does not touch the tracked `docker-compose.yml`, `Install.Docker.psm1`, or `Publish.Helpers.psm1`.

## 7. Recommended Fix Design

### 7.1 Packaging/self-containment finding (must be resolved by the spec, not silently worked around)

`Install.ps1`'s own established invariant (Section 6, #142 item 3) is that it must not depend on repo modules outside what `Copy-InstallScriptsIntoBundle` stages into the bundle root. `scripts/powershell/modules/OpenClawContainerValidation/**` is verified **not** in that staged set. A Pester test that invokes `& $PSScriptRoot\..\..\scripts\Install.ps1` runs the script from its *repo* location, where the sibling `scripts/powershell/modules/` directory still exists — so a test suite could pass even if `Install.ps1` were changed to `Import-Module scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1`, while the actual installed bundle (`artifacts/publish/<version>/Install.ps1`, which has no `scripts/powershell/` subtree) would throw `Failed to load bundled install ... module` at runtime. This is a latent defect class the test suite cannot detect by construction (the same class of risk that motivated `Install.Docker.psm1`'s self-containment note in the first place).

Because this issue's stated scope is confined to exactly `scripts/Install.ps1`, `OpenClawContainerValidation.psm1`, and `tests/scripts` — and explicitly excludes `Publish.Helpers.psm1` (which owns the bundle-staging list) — the design below keeps `Install.ps1` import-free of the container-validation module and accepts a small, explicit duplication of pure comparison logic instead of a cross-module import. This tradeoff should be recorded in the spec as a deliberate decision, not left implicit.

### 7.2 New functions in `OpenClawContainerValidation.psm1`

Add two pure, dependency-free functions (for the standalone diagnostic script's benefit, and as directly unit-testable logic in the module the issue names):

- `ConvertFrom-OpenClawImageReference -ImageReference <string>` → `[pscustomobject]@{ Repository; Tag }`. Splits on the last `:`. Returns `Tag = ''` (not a thrown error) when no `:` is present, so callers can fold a missing tag into the existing `IsExpected/Summary` result-object pattern rather than an exception — consistent with every other probe in this module (none of the existing probe functions throw for a "false" condition; they all return `IsExpected = $false` with a `Summary`).
- `Test-OpenClawImageVersionAligned -CoreImageReference <string> -AgentImageReference <string> -ExpectedVersion <string>` → a `Get-OpenClawValidationResult`-shaped object, `Category 'Container'`, `Name 'ImageVersionAlignment'`. `IsExpected` is true only when both parsed tags equal `-ExpectedVersion` by exact string comparison (matching how the tags were written: `Convert-ComposeToBundleCompose` interpolates `"$repo:$Version"` verbatim, so string equality is the correct, simplest check — no `[System.Version]` normalization is needed since both sides originate from the same literal string in a correct bundle) **and** both parsed tags additionally validate as well-formed 4-part versions via `[System.Version]::TryParse` (mirroring `Install.Helpers.psm1`'s `Get-ManifestVersion` pattern at line 43), so a malformed tag produces a distinct, actionable message from a merely-mismatched tag. `Details` should include both raw image refs, both parsed tags, and the expected version, so the fail-fast message names concrete evidence.

These two functions are added to `Export-ModuleMember` and to `OpenClawContainerValidation.psd1`'s `FunctionsToExport`.

Optional, lower-priority addition (would require also touching `scripts/Invoke-OpenClawContainerPathValidation.ps1` to wire it into `$supportingDiagnostics`, which is outside this issue's stated 3-file scope — flag to the planner as a separately-approved extension if desired, not a silent default): a docker-backed probe `Invoke-OpenClawImageVersionAlignmentProbe -DockerExecutablePath -CoreContainerName -AgentContainerName -ExpectedVersion` that fetches both running containers' `Config.Image` (same mechanism as `Get-OpenClawContainerInspection`, script lines 96-131) and calls `Test-OpenClawImageVersionAligned` on the results, giving the standalone post-install diagnostic tool the same guarantee at runtime that `Install.ps1` enforces pre-`compose up`.

### 7.3 New wiring in `Install.ps1`

Insert a new Stage 9 sub-step **before** `Invoke-DockerImageLoad` (so a detected mismatch aborts before any docker state change in this stage):

1. A new local, unconditionally-defined helper (no `Get-Command` override guard needed — it has no external I/O dependency beyond `Get-Content`, which tests already mock elsewhere in this suite): `Get-ComposeServiceImageTag -ComposeContent <string[]> -ImageRepository <string>` — a minimal regex scan for a line matching `^\s{4}image:\s*<ImageRepository>:(.+)$` (anchored on the literal `openclaw/core`/`openclaw/agent` repository prefixes, which are the only two `image:` values ever present in this compose file — no full indentation/service-context state machine is required, unlike `Convert-ComposeToBundleCompose`, because this is a read-only, single-purpose scan over a file that, post-#142, has exactly one `image:` line per service and no ambiguous nested `build:` context to skip). Throws a specific, actionable error if no match is found (drift/malformed-bundle fail-fast, consistent with `Convert-ComposeToBundleCompose`'s own drift-guard precedent at `Publish.Docker.psm1:283-287`).
2. A duplicated (small, ~10-line, explicitly justified per Section 7.1) local comparison: extract both tags, compare each to `$ResolvedVersion`, throw one aggregated fail-fast error naming both observed image refs, both extracted tags, `$ResolvedVersion`, and `$ComposeFilePath` when either tag is absent or does not equal `$ResolvedVersion`.
3. Call `Get-Content -LiteralPath $ComposeFilePath` once, pass the array into the helper twice (once per repository), consistent with how the module's own `Get-OpenClawEnvFileMap` reads a file once per call rather than re-opening it per key.

Placement: immediately after `$ComposeFilePath = Join-Path $DestDockerDir 'docker-compose.yml'` (existing line 430) and before the existing `Write-Information "[install:docker] Loading bundled container images..."` / `Invoke-DockerImageLoad` call — i.e., a new `Assert-ComposeImageVersionAligned` (or equivalently named) call inside the existing `if (-not $SkipDocker) { ... }` block, gated the same way every other Stage 9 docker step already is.

## 8. Exact Files to Change / Tests to Add or Update

**Production (within stated 3-file scope):**
- `scripts/Install.ps1` — new local helper function(s) + one new guard call in Stage 9, before `Invoke-DockerImageLoad`.
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` — two new pure functions (`ConvertFrom-OpenClawImageReference`, `Test-OpenClawImageVersionAligned`), added to `Export-ModuleMember`.
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` — `FunctionsToExport` list updated to include the two new functions (this file is a manifest sibling of the `.psm1`; editing it is part of "the module," not a scope violation).

**Tests to add:**
- `tests/scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.ImageVersionAlignment.Tests.ps1` (new) — direct-import unit tests for `ConvertFrom-OpenClawImageReference` and `Test-OpenClawImageVersionAligned`, mirroring the existing `OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1` pattern (`BeforeDiscovery` + `BeforeAll` re-import via `.psd1`, no docker/HTTP seam needed since both functions are pure). Cover: matching versions (expected), mismatched-but-well-formed versions, one tag equal to `pre-mvp` (unexpected — bundle compose should never emit `pre-mvp`), missing `:` (empty tag), malformed tag (`1.2.3`, `v1.2.3.0`, `latest`).
- `tests/scripts/Install.DockerStage.Tests.ps1` (extend, not new) — this file already builds the full Stage 9 harness (`Get-ManifestVersion` mock returning `'1.2.3.0'`, `Invoke-DockerImageLoad` mock, ordering assertions via `$global:InstallTestCalls`). Add a `Get-Content` mock branch matching `*docker-compose.yml` returning a fixture compose-line array (the current mock's `$script:GetContentMock`, lines 92-98, has no branch for the compose file path and would fall through to the `'test-hostadapter-token'` default, which would break the new parser — this is new test-fixture surface, not existing coverage). Add a new `Context 'image version alignment guard'` with cases: matched versions proceed to `Invoke-DockerImageLoad`/`Invoke-ComposeUp` in order; mismatched core-vs-agent tags throw before `Invoke-DockerImageLoad` is called (assert via `$global:InstallTestCalls -notcontains 'Invoke-DockerImageLoad'` after catching the throw); tag matching neither service's expected version throws; the guard is skipped under `-SkipDocker` (mirrors the existing `-SkipDocker` `Context` in this file, lines 174-201). If the additions push this file close to the 500-line cap, split into a sibling `tests/scripts/Install.ImageVersionAlignment.Tests.ps1` reusing the identical `BeforeAll`/`BeforeEach` harness (this file is currently 203 lines, so a single extension is likely to fit comfortably; a split is a fallback, not a default).

**Tests likely requiring no change but that must be re-run for regression:** `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Force.Tests.ps1`, `tests/scripts/Install.Docker.Tests.ps1`, `tests/scripts/Invoke-OpenClawContainerPathValidation.*.Tests.ps1` (all six split files), `tests/scripts/Publish.Docker.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1` — none of these files' production targets change under this fix, but they exercise the shared harnesses and fixtures (in particular `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`) that a new test file will also depend on, so a full-suite run is the correct regression gate rather than a scoped subset.

## 9. Version Comparison Semantics

- **Format observed:** 4-part, `^\d+\.\d+\.\d+\.\d+$`, identical to the `ValidatePattern` used throughout `Publish.ps1`/`Publish.Docker.psm1`/`Publish.Env.psm1` (e.g. `Publish.Docker.psm1:126`, `:185`, `:230`, `:342`; `Publish.Env.psm1:168`) and validated on read by `Get-ManifestVersion` via `[System.Version]::TryParse` (`Install.Helpers.psm1:43`).
- **Floating tag:** `pre-mvp` is used only for the tracked dev compose (`docker-compose.yml:11,58`) and as a secondary tag on the built/saved images (`Publish.Docker.psm1:147,159,196,198`). The bundle compose emitted by `Convert-ComposeToBundleCompose` **never** emits `pre-mvp` — every `image:` line it writes is `<repo>:$Version` with `$Version` matching the strict 4-part pattern (enforced by that function's own `[ValidatePattern]` parameter attribute at `Publish.Docker.psm1:230-231`). Therefore, if the new alignment guard ever observes `pre-mvp` in a staged bundle's `docker-compose.yml`, that is itself anomalous and should be treated as a failure (either a hand-edited/corrupted bundle, or a case that predates issue #142's fix), not tolerated as an equivalent-to-versioned case.
- **Recommended comparison:** exact string equality of the extracted tag against `$ResolvedVersion`, not `[System.Version]` numeric equality — because both values originate from the same literal interpolation in a correct bundle (`"$repo:$Version"`), string equality is simpler (Simplicity-first per `.claude/rules/general-code-change.md`) and avoids silently accepting a differently-formatted-but-numerically-equal string that would indicate a formatting bug elsewhere. Use `[System.Version]::TryParse` only as a secondary, message-quality check to distinguish "malformed tag" from "version mismatch" in the thrown/returned error text.
- **Edge cases:** (1) `pre-mvp` present where a version is expected — fail, name it explicitly as "the bundle compose should never pin the floating pre-mvp tag" (post-#142 invariant). (2) Missing tag (no `:` in the `image:` value, or an empty capture) — fail, distinguish from "mismatched version" in the message. (3) Malformed version string (`1.2.3`, `v1.2.3.0`, `1.2.3.a`) — fail via `[System.Version]::TryParse` returning `$false`; message should name the literal malformed string. (4) Both tags equal but not equal to `$ResolvedVersion` — must still fail (Section 4(c)); do not implement this as a pairwise `core == agent` check alone.

## 10. Automation Feasibility

This fix and its verification can be performed autonomously with no human interaction.

- All new logic is pure PowerShell string/regex parsing and comparison, or a Pester-mockable seam already established in this repository (`Invoke-OpenClawDockerCommand`, `Get-Content`). No third-party UI, no manual docker-registry interaction, and no signing/certificate step is involved (the fix touches neither the MSIX pipeline nor `Publish.ps1`).
- The regression-test harnesses this fix extends (`Install.DockerStage.Tests.ps1`, the `OpenClawContainerValidation` module test conventions) already run hermetically under Pester v5 with mocked seams — no real Docker Desktop, no real containers, no network access, and no temporary files, consistent with `.claude/rules/general-unit-test.md` and `.claude/rules/powershell.md`.
- The full toolchain (PoshQC format → PoshQC analyze → Pester) runs via the existing MCP commands (`mcp__drm-copilot__run_poshqc_format`, `_analyze`, `_test`) with no human step, matching the pattern already used for #142/#144.
- No workflow-file or CI-config change is implied by this fix (it does not touch `.github/workflows/**` or `scripts/benchmarks/**`), so the `modified-workflow-needs-green-run` policy rule and the benchmark-baseline provenance rule (`.claude/rules/ci-workflows.md`, `.claude/rules/benchmark-baselines.md`) do not apply and impose no human-gated requirement here.
- No human-interaction requirement is identified for this fix's implementation or verification.

## Sources

Files read (current on-disk state in this worktree, `open-claw-bridge-wt-2026-07-10T23-04` branch):
- `scripts/Publish.ps1` (full)
- `scripts/Publish.Docker.psm1` (full)
- `scripts/Install.ps1` (full)
- `scripts/Install.Docker.psm1` (full)
- `scripts/Install.Helpers.psm1` (lines 1-100, 290-429)
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` (full)
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` (full)
- `scripts/Invoke-OpenClawContainerPathValidation.ps1` (full)
- `scripts/Publish.Env.psm1` (lines 130-180, `Step-PackageVersion`)
- `scripts/Publish.Helpers.psm1` (`Copy-DockerArtifact`, `New-ManifestEntry`, `Copy-InstallScriptsIntoBundle` regions)
- `docker-compose.yml` (repo root, full)
- `.claude/rules/powershell.md` (full)
- `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/issue.md`, `spec.md` (full)
- `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/issue.md`, `code-review.2026-07-11T09-15.md` (full)
- `docs/features/active/2026-07-11-installer-image-version-alignment-147/issue.md`, `spec.md`, `plan.2026-07-11T19-34.md` (full; both spec and plan are unfilled drafts at time of this research)
- `tests/scripts/Install.DockerStage.Tests.ps1` (full)
- `tests/scripts/Invoke-OpenClawContainerPathValidation.GatewayTokenInContainer.Tests.ps1` (full)
- `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` (full)
- `tests/scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1` (full)
- `tests/scripts/Publish.Docker.Tests.ps1` (lines 1-80)
- `tests/scripts/Install.Tests.ps1` (grep for `composeFilePath`/`ComposeFilePath`)
- Directory listings: `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/**`, `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/**`, `tests/scripts/**`, `tests/scripts/powershell/modules/**`

Git refs referenced from committed evidence (not independently re-run; read from committed code-review artifacts and confirmed against current on-disk file contents, since this worktree's HEAD already contains the merged #142/#144 fixes): `a79dee4`, `3b0a2b3`, `bb2a9b4`, merges `#143`/`#145` (per the conversation's recent-commits list and `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/code-review.2026-07-11T09-15.md`, which cites base `81debeb1` and head `3b0a2b3`).

No production code, configuration, or test files were modified in the course of this research.
