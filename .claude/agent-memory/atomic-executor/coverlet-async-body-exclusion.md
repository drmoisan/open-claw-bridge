---
name: coverlet-async-body-exclusion
description: mailbridge.runsettings excludes CompilerGeneratedAttribute, so async method bodies report zero instrumented lines; argue changed-line coverage behaviorally
metadata:
  type: project
---

In this repo's coverage collection (`mailbridge.runsettings`, coverlet XPlat collector), `ExcludeByAttribute` includes `CompilerGeneratedAttribute`, which excludes async state-machine bodies from instrumentation. An async method contributes no body lines to Cobertura output (e.g., `HostAdapterSchedulingService` reports only its primary-constructor lines).

**Why:** During issue #99, converting a non-async throwing method to an async delegating method made the class's instrumented line count drop from 9 to 4 — not a coverage regression, just instrumentation scope. Changed-line coverage for async code cannot be read off Cobertura per-line hits.

**How to apply:** For changed-line coverage evidence on async methods, state the instrumentation exclusion explicitly in the artifact and cite the behavioral tests (fail-before/pass-after) that exercise every branch of the new body. Also note: Cobertura `<class>` line elements appear under both `methods/method/lines` and `class/lines` — sum only `./classes/class/./lines/line` to avoid double counting; pool solution coverage from root `lines-covered`/`lines-valid` attributes across the per-test-project XML files.

**Scope caveat (observed 2026-07-02, issue #113):** the exclusion is not universal. A new `private async Task<T>` method in `ClientCredentialsTokenProvider` (coverlet.collector 6.0.2, same runsettings) WAS fully instrumented — 37/37 lines including the async body and its catch blocks. What reliably reports zero instrumented lines: interface-only files and auto-property-only options bags (accessor bodies are compiler-generated). Check the actual Cobertura output before arguing behaviorally; the behavioral argument is the fallback, not the default.
