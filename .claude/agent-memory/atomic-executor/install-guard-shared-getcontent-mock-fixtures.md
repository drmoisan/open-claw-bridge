---
name: install-guard-shared-getcontent-mock-fixtures
description: Adding a new Get-Content-consuming guard to scripts/Install.ps1 Stage 9 requires fixture updates in every Install.*.Tests.ps1 file with its own $script:GetContentMock, not just the plan's explicitly-scoped test file(s)
metadata:
  type: project
---

`scripts/Install.ps1` has multiple sibling Pester suites (`Install.Tests.ps1`, `Install.Force.Tests.ps1`, `Install.DockerStage.Tests.ps1`, `Install.Docker.Tests.ps1`) that each define their own `$script:GetContentMock` closure rather than sharing one fixture module. When issue #147 added a new Stage 9 guard that calls `Get-Content -LiteralPath $ComposeFilePath` for the first time, only `Install.DockerStage.Tests.ps1` (the plan's explicitly-scoped test file) had its mock updated with a `*docker-compose.yml` branch — `Install.Tests.ps1` and `Install.Force.Tests.ps1` did not, and both broke (13 and 6 new failures respectively) because their non-`-SkipDocker` scenarios now hit the new guard with an unmocked compose-file read.

**Why:** These test files are NOT wired through the shared `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` fixture (that fixture is for the `OpenClawContainerValidation` module, unrelated to `Install.ps1`'s own local `Get-Content` mocking pattern). Each `Install.*.Tests.ps1` file's `$script:GetContentMock` is copy-pasted and independently maintained, so a new `Get-Content` call site inside `Install.ps1`'s guarded (`-not $SkipDocker`) code path silently breaks every sibling suite that doesn't special-case it.

**How to apply:** Before landing any change to `Install.ps1` that adds a new `Get-Content`/`Get-Content -LiteralPath` call inside the non-`-SkipDocker` Stage 9+ code path, grep all `tests/scripts/Install*.Tests.ps1` files for `$script:GetContentMock` and add a matching branch to each one — not just the test file(s) an atomic plan explicitly names in scope. Confirm via a full-suite regression run (see [[pester-narrow-filelist-false-failures]]) rather than a narrow targeted run, since a narrow run of only the plan-scoped test file will not surface breakage in the untouched siblings.
