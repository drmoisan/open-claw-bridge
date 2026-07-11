# Phase 4 — PoshQC Toolchain Loop (Issue #142)

Timestamp: 2026-07-10T19-10

Scope (1 prod, 3 test): `scripts/Install.ps1` (455), `tests/scripts/Install.Tests.ps1` (485, down from 505 pre-change via -SkipDocker relocation + harness additions), `tests/scripts/Install.Force.Tests.ps1` (249), NEW `tests/scripts/Install.DockerStage.Tests.ps1` (202). All <= 500. `scripts/Install.Helpers.psm1` unchanged (git diff empty).

One intermediate restart: analyze flagged PSUseBOMForUnicodeEncodedFile on Install.DockerStage.Tests.ps1 (an em-dash in the Describe name). Fixed by replacing the em-dash with an ASCII hyphen (keeps the file ASCII, no BOM needed).

Command: mcp__drm-copilot__run_poshqc_format (workspace_root = repo root)
EXIT_CODE: 0
Output Summary: ok=true; no PowerShell files left unformatted.

Command: mcp__drm-copilot__run_poshqc_analyze (workspace_root = repo root)
EXIT_CODE: 0
Output Summary: ok=true; 0 analyzer findings.

Command: pwsh Invoke-Pester -Configuration (Run.Path = tests/scripts) [full suite]
EXIT_CODE: 0
Output Summary: Tests Passed: 402, Failed: 0, Skipped: 0. Duration ~29s. Previous phase full-suite was 398; +4 net (new Install.DockerStage.Tests.ps1 adds 7 Its; 3 -SkipDocker Its were relocated out of Install.Tests.ps1). The P4-T6 expect-fail image-load-stage tests now PASS: Stage 9 calls Invoke-DockerImageLoad on `<DestDockerDir>\openclaw-images.tar` after Copy-BundleContents and before Invoke-ComposeUp, on both default and -Force paths, and is skipped under -SkipDocker. The Install.Tests.ps1 stage-ordering test (now including Invoke-DockerImageLoad before Invoke-ComposeUp) passes.
