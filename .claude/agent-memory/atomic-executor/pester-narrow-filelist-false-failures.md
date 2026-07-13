---
name: pester-narrow-filelist-false-failures
description: Running Invoke-Pester against a curated subset of test file paths in open-claw-bridge can produce false failures (test-isolation/ordering artifacts) that disappear when run via the full default Run.Path
metadata:
  type: feedback
---

Running `Invoke-Pester`/`Invoke-PoshQCTest` against a hand-picked list of specific `*.Tests.ps1` paths (rather than the full default `Run.Path` — `scripts`, `tests/powershell`, `tests/scripts`) can produce false failures that do not reproduce when the same files run as part of the full suite discovery order. Observed concretely on issue #147: a narrow run of 8 named AC14 regression files showed 46 failures including `CommandNotFoundException: Could not find Command Invoke-VersionStamp` in `Publish.ps1`/`Publish.Docker.Tests.ps1`/`Publish.Helpers.Tests.ps1`; re-running via the full default `Run.Path` showed 0 failures in those same files.

**Why:** Some test files rely on module state/ordering that the full suite's natural discovery order happens to satisfy (e.g., a module import or global function defined by an earlier-discovered file's `BeforeAll`). A curated subset can skip whatever established that state, producing spurious `CommandNotFoundException`-style failures that look like real regressions but are not.

**How to apply:** Before concluding a regression run shows genuine new failures, always cross-check by re-running the same file(s) via the full default `Run.Path` (or the full named regression list in one `Invoke-Pester`/`Invoke-PoshQCTest` call) before diagnosing root cause. Compare per-`<testsuite>` `failures="N"` counts in the JUnit XML against a full-suite baseline rather than trusting a narrow ad hoc run. See also [[install-guard-shared-getcontent-mock-fixtures]].
