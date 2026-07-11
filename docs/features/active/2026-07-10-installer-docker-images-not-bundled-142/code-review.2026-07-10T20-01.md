# Code Review — Issue #142 (installer-docker-images-not-bundled)

- Reviewed: 2026-07-10T20-01
- Base: `main` @ `ca53297a558cd0fd8d3f13e8994d2637bef6740a`
- Head: `bug/installer-docker-images-not-bundled-142` @ `79c6a3b`

## Executive Summary

The branch fixes the blocker where the scripted-bundle install failed at `docker compose up` (no images in any registry, no `src/` tree in the bundle to build from) by moving image acquisition to publish time: a new Stage 3b builds both images, `docker save`s all four refs into a single `docker/openclaw-images.tar`, and overwrites the bundle compose with a transformed copy (no `build:` keys, `pull_policy: never`, versioned `image:` tags); install then `docker load`s the tar immediately before `Invoke-ComposeUp` inside the existing `-SkipDocker` gate. Two enabling fixes ride along: the `New-ManifestEntry` size cast widens `[int]` -> `[long]` (multi-GiB tar support, with genuine fail-before evidence) and the dead `docker-compose.dev.yml` is no longer shipped. The design is disciplined: all docker invocations route through per-module `Invoke-DockerExe` seams with result-shaping extracted into a pure, tested helper; the compose transform is a pure function with fail-fast drift detection; and every stated non-goal (tracked compose byte-identical, `Install.Helpers.psm1` untouched) is honored. One Minor and three Info findings; no Blocking or Major findings.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Minor | `scripts/Install.ps1` | Line 113 (new `catch` throw for the `Install.Docker.psm1` import) | The new import-failure catch arm is uncovered (`mi=2 ci=0` in the per-line JaCoCo data); no test exercises a failed `Import-Module` of the new docker module. | Add one directed test that forces the docker-module import to fail (e.g., a harness variant pointing `$PSScriptRoot` resolution at a location without the module, or asserting the throw message contract), or accept as pattern-consistent debt with this record. Non-gating. | The gap exactly replicates the pre-existing accepted pattern: the two sibling catch arms for the helper (line 99) and preflight (line 106) module imports are equally uncovered at head, and the file's whole-file figures pass all gates (88.57% line, 85.65% command). A changed-region gap replicating a pre-existing accepted pattern in a passing file grades Minor per the established shifted-pattern precedent (#128). | Independent per-line parse of `artifacts/pester/powershell-coverage.xml`: lines 99, 106, 113 all `mi=2 ci=0`. |
| Info | `scripts/Publish.Docker.psm1`, `scripts/Install.Docker.psm1` | `ConvertTo-DockerExeResult` (lines 31-56 / 22-47) and `Invoke-DockerExe` (lines 58-80 / 49-69) | The seam pair is duplicated verbatim across the two modules rather than shared. | No action required. The duplication is a deliberate, documented constraint: `Install.ps1` imports only files staged inside the bundle, so `Install.Docker.psm1` must be self-contained (module header "Self-containment" block; spec Boundaries section). | Sharing would either force the publish module into the bundle or introduce a third staged module — both worse than 26 duplicated lines of stable wrapper code. | Source read of both module headers; spec.md `### Boundaries and invariants to preserve`. |
| Info | `scripts/Publish.Docker.psm1` | `Build-OpenClawDockerImage`, `-AgentBaseImage` parameter (line 136, default `''`) | A direct call with `-Kind agent` and no `-AgentBaseImage` would emit `--build-arg OPENCLAW_AGENT_IMAGE=` (empty string), which overrides the Dockerfile `ARG` default and would fail the `FROM` resolution at build time rather than falling back to the default. | Consider validating that `-AgentBaseImage` is non-empty when `-Kind agent`, or resolving the default inside the function. Purely defensive: the only production caller (`Invoke-PublishDockerStage`, line 352-355) always resolves the value via `Resolve-OpenClawAgentBaseImage` first, so the path is unreachable in the shipped flow, and docker itself fails fast with a clear error if ever hit. | Unreachable-in-production defensive hardening; not a defect in the delivered flow. | Source read: `Invoke-PublishDockerStage` resolves before both build calls; `Resolve-OpenClawAgentBaseImage` handles absent/blank map values. |
| Info | `scripts/Publish.Docker.psm1` | `Convert-ComposeToBundleCompose`, build-block removal loop (lines 260-264) | A blank line inside a service's `build:` block would terminate the deeper-indent removal early (a blank line computes indent 0 and is not `> 4`), leaving subsequent block-body lines orphaned in the output without their `build:` parent. The count-based drift guard would not throw for this shape (build/image keys still count 1 each). | No action required now. The tracked compose has no blank lines inside either build block, and the real-file drift-guard test (`transforms the tracked docker-compose.yml without throwing` + zero-`build:`-lines assertion) runs against the actual repo file on every test pass, so a future compose edit that broke the transform's assumptions would surface quickly. Optionally tighten the loop to skip blank lines while `removingBuild` is set. | Latent robustness edge on a repo-controlled input, guarded by an always-run real-file test; fail-fast drift throw covers the more likely drift shapes (renamed/moved keys). | Source read of the transform; `tests/scripts/Publish.Docker.Tests.ps1` Context "drift guard". |

No Blocking or Major findings were identified.

## Files Reviewed

| File | Change | Assessment |
|---|---|---|
| `scripts/Publish.Docker.psm1` | New, 373 lines | Publish-side seam + build/save/transform helpers + Stage 3b facade. See Design Principles below. |
| `scripts/Install.Docker.psm1` | New, 103 lines | Self-contained install-side seam + `Invoke-DockerImageLoad`. Clean fail-fast error contract. |
| `scripts/Publish.ps1` | +9/-1: docker-module import, Stage 3b call, staging-message update | Minimal; Stage 3b correctly placed after `Copy-DockerArtifact` and before the MSIX stage and `Write-PublishManifest`, so tar + transformed compose are manifested automatically. `Get-EnvFileMap -Content $envContent` reuses the already-read repo-root `.env` (line 119) — read-only reuse per spec. |
| `scripts/Publish.Helpers.psm1` | `[int]` -> `[long]` size cast; dev-compose copy removed; `Install.Docker.psm1` added to the 5-file staging list; docstrings updated | Exact match to the spec's stated edits; docstrings updated in step with behavior (including the rationale for dropping the dev compose, citing issue #142). |
| `scripts/Install.ps1` | +11: docker-module import (with fail-fast catch matching the two existing import blocks) and Stage 9 load call before `Invoke-ComposeUp` inside the `-SkipDocker` gate | Correct placement and gating; verified by stage-sequence tests and by source read. |
| `tests/scripts/Publish.Docker.Tests.ps1` | New, 278 lines, 15 Its | See Test Quality below. |
| `tests/scripts/Install.Docker.Tests.ps1` | New, 95 lines, 6 Its | See Test Quality below. |
| `tests/scripts/Install.DockerStage.Tests.ps1` | New, 202 lines, 7 Its | Full-orchestrator harness mirroring `Install.Force.Tests.ps1`; includes the relocated `-SkipDocker` trio from `Install.Tests.ps1` plus the new skips-load assertion. |
| `tests/scripts/Install.Tests.ps1` | `-SkipDocker` Context relocated to the new stage file; `Invoke-DockerImageLoad` added to mock set and helper-order expectation | Relocation is a clean move (no assertion lost: all three Its reappear verbatim in `Install.DockerStage.Tests.ps1` plus one new It); the helper-order test now pins the load between `Assert-HostAdapterBridgeReadyPreflight` and `Invoke-ComposeUp`. |
| `tests/scripts/Install.Force.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1` | Mock-set registration, stage-order, staging-list, dev-compose, and `[long]` size updates | All consistent with the production changes; the `[long]` test uses 3,000,000,000 (> `[int]::MaxValue`), which genuinely failed pre-fix per the committed expect-fail evidence. |

## Design Principles (general-code-change.md)

- **Simplicity first**: the compose transform is a deterministic line-based edit of a repo-controlled 95-line file, chosen over YAML round-tripping (rejected in the spec on dependency-policy and interpolation-preservation grounds). Each helper does one thing; the facade adds exactly one orchestrator call site and one test mock.
- **Reusability**: result-shaping extracted once per module into pure `ConvertTo-DockerExeResult`; base-image resolution reuses the existing `Get-EnvFileMap` rather than re-parsing `.env`.
- **Extensibility**: named parameters with validation throughout; `-Kind` `ValidateSet` cleanly extends to future images; the facade signature passes all state explicitly (no script-scoped mutable state).
- **Separation of concerns**: pure logic (`Convert-ComposeToBundleCompose`, `Resolve-OpenClawAgentBaseImage`, `ConvertTo-DockerExeResult`) is fully separated from I/O (`Invoke-DockerExe`, `Write-BundleCompose` thin writer under `ShouldProcess`, `Get-Content` confined to the writer).
- **Non-goals honored** (all independently confirmed via empty `git diff`): tracked `docker-compose.yml` byte-identical (dev `--build` path intact); `deploy/docker/*.Dockerfile` unchanged; the four pre-existing direct `docker` call sites in `scripts/Install.Helpers.psm1` untouched; `Uninstall.ps1` untouched; no registry publishing or signing flows introduced.

## Test Quality

- One behavior per `It`, clear `Context` grouping, descriptive names traceable to ACs. Arrange–Act–Assert structure throughout.
- Hermeticity: only the `Invoke-DockerExe` seam is mocked (`Mock -ModuleName`), never the `docker` executable; no `global:docker` shim; no temp files (all fixtures are in-memory string arrays); independently confirmed by direct read of all three new files.
- Mock signature parity: every seam mock declares `param([string[]]$DockerArgs)`, matching the production parameter exactly.
- Exact argument-vector assertions (joined with `|`) make build/save/load drift a test failure, fulfilling the spec's drift-mitigation design (AC2).
- The real-file drift-guard test runs the transform against the actual tracked `docker-compose.yml` on every pass — a live invariant, not a frozen fixture.
- Negative paths: two transform drift throws (each asserting the service name appears in the message), non-zero-exit throws for build/save/load, missing-tar throw asserting both the tar path and the `Publish.ps1` remediation pointer appear in the message.
- `-WhatIf` verified to produce zero seam invocations for both the publish facade and the install load — `ShouldProcess` contract tested, not just declared.
- Global capture variables carry justified, narrowly-worded `PSAvoidGlobalVars` suppressions and are cleaned up in `AfterEach`/`AfterAll`; `BeforeEach` resets shared state, preserving test independence.
- Fail-before/pass-after evidence is genuine for both regression targets (manifest `[int]` overflow; missing Stage 9 wiring), captured before the fixes were applied.

## Naming and Style

- Approved verbs and descriptive nouns throughout (`Build-`, `Save-`, `Convert-`, `Write-`, `Invoke-`, `Resolve-`); zero `PSUseApprovedVerbs` or other analyzer findings in this audit's independent run.
- The intentional `New-` verb note on `New-ManifestEntry` (pure constructor, no `ShouldProcess`) predates this diff and remains accurate after the `[long]` change.
- Comment-based help on every function; the module headers document purpose, the AC2 cross-reference to the tracked compose's `build:` blocks (with line numbers), and the policy rationale for the uncovered seam bodies.

## Error Handling and Logging (general-code-change.md)

- Fail-fast with specific, actionable messages at every failure point: build failure names the image ref and includes docker output; save failure names the tar path; transform drift names the service and the reconciliation target; missing tar names the expected path and directs the operator to re-run `Publish.ps1`; load failure includes docker output. No broad catch-alls; the one catch added to `Install.ps1` immediately re-throws with added context, matching the two existing import blocks verbatim.
- Stage progress uses the established `Write-Information` prefixed convention (`[docker-images]`, `[install:docker]`).

## Dependencies

- No new package dependencies. The spec's evaluation and rejection of `powershell-yaml` and `docker compose config` rendering is documented and consistent with the dependency policy.

## I/O Boundaries (general-code-change.md)

- All external-process I/O funnels through the two seam functions; filesystem writes are confined to `Write-BundleCompose` (under `ShouldProcess`) and docker's own `-o` tar write; the transform and resolution logic is testable with zero I/O. Consistent with the repo's wrapper-seam idiom.

## Risks Observed During Review

- Bundle-size and publish-duration growth from the multi-GiB tar is inherent to the chosen (spec-confirmed) prebuilt-image approach and is documented in the spec with the `[long]` manifest fix removing the resulting overflow failure mode.
- Floating `pre-mvp` tag re-pointing on the install host is neutralized for installed stacks by the bundle compose pinning versioned tags.
- No finding rises above Minor; the single Minor is a non-gating coverage note on a pattern-replicating catch arm.

## Overall Code Quality Verdict

**PASS.** The change is well-decomposed, honors every stated boundary and non-goal, uses the repository's established seam and testing idioms with exact-vector drift protection, and carries genuine fail-before/pass-after regression evidence for both enabling fixes. 0 Blocking, 0 Major, 1 Minor (non-gating), 3 Info.
