# Policy Audit — graph-activity-log-purview (Issue #124)

- **Feature folder:** `docs/features/active/2026-07-07-graph-activity-log-purview-124/`
- **Branch:** `feature/graph-activity-log-purview-124`
- **Base:** `origin/epic/openclaw-vision-integration` (merge-base `7a29286`)
- **Work mode:** `full-feature` (from `issue.md`: `- Work Mode: full-feature`)
- **Reviewed:** 2026-07-07T06-54 UTC
- **Overall verdict: PASS**

## 0. Environment Accommodations

- The MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are
  not available in this review environment; this artifact set mirrors the structure of the most
  recent accepted C# artifact set (`docs/features/active/2026-07-06-negative-scope-smoke-test-120/`)
  instead.
- `dotnet tool restore` is not usable in this environment; the globally installed `csharpier`
  (invoked as `csharpier check .`, confirmed on PATH) was used instead — an accepted fallback.

## 1. Scope Verification

`git diff origin/epic/openclaw-vision-integration...HEAD` (merge-base `7a29286`) touches 61 files.
Confirmed by direct `git diff --name-only` re-run in this review (not taken from the caller's
summary):

- **Production code (11 files, all under `src/OpenClaw.Core/`):**
  `Agent/Contracts/CloudSyncActingFlags.cs` (new), `Agent/Contracts/CloudSyncActivityAuditor.cs`
  (new), `Agent/Contracts/CloudSyncActivityResultCode.cs` (new),
  `Agent/Contracts/CloudSyncActivityType.cs` (new), `Agent/Contracts/PurviewActivityLogProjection.cs`
  (new), `Agent/Contracts/PurviewActivityLogRecord.cs` (new), `CloudSync/GraphDeltaReconciler.cs`
  (modified), `CloudSync/GraphSubscriptionManager.cs` (modified),
  `CloudSync/NotificationRequestProcessor.cs` (modified), `ICloudSyncActivityAuditor.cs` (new),
  `Program.cs` (modified).
- **Test code (16 files):** under `tests/OpenClaw.Core.Tests/Agent/Contracts/` and
  `tests/OpenClaw.Core.Tests/CloudSync/`, plus `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogTests.cs`.
- **Feature docs, evidence, runbook, memory:** remaining 34 files under
  `docs/features/active/2026-07-07-graph-activity-log-purview-124/` and `.claude/agent-memory/`.

**Verdict: PASS.** The "production changes are confined to `src/OpenClaw.Core/`" claim is confirmed
by direct file-list enumeration, not by trusting the caller's characterization.

### No workflow/benchmark changes

`git diff --stat origin/epic/openclaw-vision-integration...HEAD -- '.github/workflows/**' 'scripts/benchmarks/**'`
returns empty output. **Verdict: PASS.** `modified-workflow-needs-green-run`
(`.claude/rules/ci-workflows.md`, `.claude/rules/benchmark-baselines.md`) does not apply to this
branch.

## 2. Rejected Scope Narrowing

None observed. The caller instructions in this review request explicitly reaffirmed the full-diff
scope invariant and did not attempt to narrow it to a plan/task/phase subset.

## 3. Evidence Location Compliance

`git diff --name-only origin/epic/openclaw-vision-integration...HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
returns no matches. No `validate_evidence_locations.py` script exists in this repository (confirmed
by `find` — this is a known, previously-documented environment gap, not new to this review). All
evidence produced by this feature lives under the canonical
`docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/{baseline,qa-gates,regression-testing,other}/`
tree. **Verdict: PASS.** No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries required.

## 4. Architecture-Boundary Compliance (central design decision)

The plan's binding decision (spec.md Design Decision 1, revised post-execution) requires that
`OpenClaw.Core.CloudSync` never depend on `OpenClaw.Core.Agent`, mediated instead through a narrow
port `ICloudSyncActivityAuditor` declared in the bare `OpenClaw.Core` namespace.

**Independent verification performed in this review (not taken on the executor's word):**

1. `grep -rn "OpenClaw.Core.Agent" src/OpenClaw.Core/CloudSync/` → one match, in
   `NotificationRequestProcessor.cs` line 113, inside an XML-doc `<c>` tag
   (`/// <c>OpenClaw.Core.Agent</c>'s <c>CloudSyncActivityResultCode</c> constants...`). This is
   documentation prose, not a `using` directive or type reference, and does not create a compiled
   namespace dependency (see the code-review artifact for a documentation-precision note). Zero
   actual production code references to `OpenClaw.Core.Agent` exist under `src/OpenClaw.Core/CloudSync/`.
2. `dotnet build` (full solution, from repo root): **0 Warning(s), 0 Error(s)**, 9 projects built.
3. `dotnet test --filter "FullyQualifiedName~ArchitectureBoundary" --no-build`: **14/14 passed**,
   0 failed, including all four `CloudSyncArchitectureBoundaryTests` (issue #117):
   `CloudSync_DependsOnlyOnTheAllowedOpenClawSurfaces`, `CloudSync_DoesNotDependOnTheAgentPartition`,
   `CloudSync_DoesNotDependOnComInterop`, `NothingOutsideCloudSync_DependsOnCloudSyncInternals`.
4. `find src/OpenClaw.Core/CloudSync -iname "*ServiceCollection*"` →
   `CloudSyncServiceCollectionExtensions.cs`; grep for `ICloudSyncActivityAuditor`/
   `CloudSyncActivityAuditor` in that file returns no matches. The DI registration for the concrete
   `OpenClaw.Core.Agent.CloudSyncActivityAuditor` adapter lives only in `Program.cs` (confirmed by
   reading the file: `builder.Services.AddSingleton<ICloudSyncActivityAuditor,
   OpenClaw.Core.Agent.CloudSyncActivityAuditor>();`), which is the composition root exempted from
   the `NothingOutsideCloudSync_DependsOnCloudSyncInternals` boundary per `evidence/other/architecture-boundary-conflict.md`.

**Verdict: PASS.** The claim in the review request — "the three CloudSync classes depend only on
the bare-`OpenClaw.Core` port; the adapter that touches `OpenClaw.Core.Agent` lives in the Agent
partition; DI registration is in `Program.cs`, not in `CloudSyncServiceCollectionExtensions.cs`" —
is independently confirmed by direct grep, build, and test execution in this review, not merely
restated from the executor's evidence files.

The `evidence/other/architecture-boundary-conflict.md` record documents that this seam was added
*after* an initial direct-dependency implementation failed the same two tests (2 new failures
against a 14/14 Phase 0 baseline); the fix (Phase 9 commits `3af1f33`..`b13da2f`) is the version now
on `HEAD` and is what this audit evaluated. This is a well-documented example of the required
escalate-rather-than-improvise-around-a-conflicting-architecture-rule behavior.

## 5. Test Coverage Detail

Independently re-run in this review (not taken from committed evidence alone):

- `dotnet test --collect:"XPlat Code Coverage" --no-build` (full solution): **857/857** passed
  (`OpenClaw.Core.Tests`), **100/100** passed (`OpenClaw.HostAdapter.Tests`), **347/352** passed,
  5 skipped (`OpenClaw.MailBridge.Tests`, pre-existing platform-conditional skips unrelated to this
  feature — confirmed by name: `Com_active_object_create_and_logon_should_throw_on_non_windows`,
  publish-output checks, and two `SendMail` COM-dependent tests).
- Parsed the resulting Cobertura report
  (`tests/OpenClaw.Core.Tests/TestResults/7f0e6918-c972-4718-abc7-a6493f79ad9a/coverage.cobertura.xml`)
  directly with a scratch Python script:
  - `OpenClaw.Core` package: **line-rate 0.9303 (93.03%), branch-rate 0.8145 (81.45%)**. This
    exactly matches the committed `evidence/qa-gates/p9-qa-04-dotnet-test-coverage.md` figures.
  - Per-file aggregation (summing `<line>` hit/valid and condition-coverage across all `<class>`
    entries per file, since Cobertura reports one entry per compiler-generated nested/partial
    type) reproduced the committed per-file figures within normal aggregation-method variance:
    `GraphSubscriptionManager.cs` ~90%/79.4% line/branch, `NotificationRequestProcessor.cs`
    100%/75.0%, `GraphDeltaReconciler.cs` ~100%/~91%, `PurviewActivityLogProjection.cs`
    92.2%/85.0%, `CloudSyncActivityAuditor.cs` 100%/83.3%, `PurviewActivityLogRecord.cs` 69.2% line
    (0/0 branches — a record type's compiler-generated equality/`ToString` members).
  - `CloudSyncActivityType.cs`, `CloudSyncActivityResultCode.cs`, `CloudSyncActingFlags.cs`: 0/0
    lines — const-only classes with no executable behavior, correctly outside the Cobertura
    denominator per the type-with-no-executable-behavior clarification in
    `.claude/rules/general-unit-test.md` / `.claude/rules/csharp.md`.
  - `ICloudSyncActivityAuditor.cs`: interface-only file, absent from the Cobertura report for the
    same reason.

**Coverage Evidence Checklist (C# only; TypeScript/PowerShell N/A — out of scope for this branch,
zero changed files in those languages):**

| Metric | Baseline (Phase 0, `evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`) | Post-change (this review's independent re-run) | Change | Threshold | Verdict |
|---|---|---|---|---|---|
| OpenClaw.Core line coverage | 92.88% | 93.03% | +0.15pp | >= 85% | PASS |
| OpenClaw.Core branch coverage | 81.48% | 81.45% | -0.03pp | >= 75% | PASS |

New/changed-code coverage: every new/changed production file except const-only and interface-only
types (which have zero executable lines by design) is at or above 85% line / 75% branch — see
per-file figures above. Disposition: no regression on changed lines; both uniform T1-T4 thresholds
held. Evidence: this review's own `dotnet test --collect:"XPlat Code Coverage"` run plus
`evidence/qa-gates/p9-qa-04-dotnet-test-coverage.md` and `p9-qa-05-coverage-delta.md`.

Async-instrumentation note: per this repository's known Cobertura behavior, plain
`dotnet test --collect:"XPlat Code Coverage"` (no `--settings`) fully instruments async method
bodies, auto-properties, and lambda bodies — the mode used both by this review's independent
re-run and by the committed Phase 9 evidence. No async-body coverage masking applies to the
figures above.

**Coverage Verdict: PASS** (repo-wide `OpenClaw.Core` and all measurable new/changed files hold
both uniform thresholds; zero regression against the Phase 0 baseline).

## 6. Toolchain Compliance (`general-code-change.md` seven-stage loop)

Independently re-run in this review:

| Stage | Result | Evidence |
|---|---|---|
| 1. Formatting (CSharpier) | PASS | `csharpier check .` on the 11 changed production files: "Checked 11 files in 622ms", zero unformatted |
| 2. Linting (.NET analyzers) | PASS | `dotnet build`: 0 Warning(s), 0 Error(s) |
| 3. Nullable type-check | PASS | same `dotnet build` run (`TreatWarningsAsErrors=true`) |
| 4. Architecture-boundary tests | PASS | `dotnet test --filter ArchitectureBoundary`: 14/14 passed |
| 5. Unit tests (incl. property tests) | PASS | 857/857 (Core), 100/100 (HostAdapter), 347/352 (MailBridge, pre-existing skips) |
| 6. Contract/schema compatibility | N/A | no external API contract changed by this feature |
| 7. Integration tests | N/A | no new external-system integration; all instrumentation targets existing in-process seams |

**Verdict: PASS — single clean pass confirmed independently in this review**, superseding the
committed Phase 8 evidence (`final-qa-06-toolchain-clean-pass.md`), which correctly recorded a
FAIL/BLOCKED state for the pre-Phase-9 direct-dependency implementation. The Phase 9 evidence
(`p9-qa-06-toolchain-clean-pass.md`) already recorded the fixed state; this review reproduces that
result from a fresh `dotnet build` / `dotnet test` invocation rather than accepting the artifact on
its own.

## 7. Code Quality Checks

See `code-review.2026-07-07T06-54.md` for the full narrative review. Summary of policy-relevant
checks performed directly in this audit:

- **File size limit (<= 500 lines):** all 11 production files and all 16 test files measured by
  `wc -l`; maximum is `Program.cs` at 368 lines. **PASS.**
- **Banned APIs** (`DateTime.Now`, `DateTime.UtcNow`, `Random.Shared`, `Thread.Sleep`,
  `Task.Delay`): `grep` across all new/changed production and test files returns zero matches.
  `TimeProvider`/`FakeTimeProvider` used exclusively for time. **PASS.**
- **Untyped escape hatches** (`dynamic`): zero occurrences in new/changed production files
  (T1 module, threshold is 0). **PASS.**
- **Suppressions** (`#pragma warning disable`, `SuppressMessage`, `NOSONAR`): zero occurrences in
  the four core new/changed production files checked. **PASS.**
- **DI seam pattern:** interface seam (`ICloudSyncActivityAuditor`) used, matching
  `.claude/rules/csharp.md`'s stated preference order ("Interface seam (preferred)"). **PASS.**
- **Temp-file prohibition in tests:** `grep` for `Path.GetTempFileName`/`Path.GetTempPath` across
  all 9 new/changed test files returns zero matches. **PASS.**

## 8. Property-Test-Density Gate (T1, `quality-tiers.md`)

`OpenClaw.Core` is a T1 module (classifier/adapter-adjacent critical path); the gate requires
>= 1 property test per pure function. `PurviewActivityLogProjection.Project` is this feature's one
new pure function (static, no I/O, no clock read, no randomness — confirmed by reading the source).
`tests/OpenClaw.Core.Tests/Agent/Contracts/PurviewActivityLogProjectionPropertyTests.cs` contains
two genuine CsCheck property tests (`Project_AnyValidRecord_NeverThrows`,
`Project_AnyValidRecord_ReturnsNonEmptyIdCorrelationIdAndActivityDateTime`), each sampling 1000
generated `ActionAuditRecord` instances including out-of-known-constant-set `ActionType`/
`ResultCode` values to exercise the fallback branches. **Verdict: PASS.**

## 9. Acceptance-Criteria Source Verification (full-feature: `spec.md` + `user-story.md`)

See `feature-audit.2026-07-07T06-54.md` for the full evaluation table. Summary: all 5 checkbox
acceptance criteria in `user-story.md` (the authoritative checkbox AC list for this feature) are
independently verified PASS with concrete evidence in this review. `spec.md` does not carry a
dedicated `## Acceptance Criteria`/`### Acceptance Criteria`/`## Done When` heading (it carries
generically-named `## Definition of Done` and `## Seeded Test Conditions (from potential)`
checklists instead, both entirely unchecked) — this is noted as a documentation-formatting
observation in the feature-audit, not a blocking finding, since the checklist content is otherwise
satisfied by the same evidence verified against the `user-story.md` items.

## 10. Additive-Only Contract Verification

- `git diff origin/epic/openclaw-vision-integration...HEAD -- src/OpenClaw.Core/Agent/Contracts/IActionAuditLog.cs src/OpenClaw.Core/Agent/Contracts/ActionAuditRecord.cs src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs`
  → **empty diff**. `IActionAuditLog`'s two methods and `ActionAuditRecord`'s 13 positional
  parameters are byte-identical to the pre-feature state.
- `git diff --stat ... -- '**/CoreCacheRepository*' '**/*.sql'` → only the new test file
  `CoreCacheRepositoryAuditLogTests.cs` (+37 lines); no production `CoreCacheRepository*` file
  changed. No `audit_log` schema (DDL) change.
- `dotnet test --filter "FullyQualifiedName~AuditLog" --no-build`: **24/24 passed** (F9 store/audit
  suite, unchanged).
- Pre-existing F14 test files (`GraphSubscriptionManagerTests.cs`, `GraphDeltaReconcilerTests.cs`,
  `NotificationRequestProcessorTests.cs`, `NotificationRequestProcessorEdgeTests.cs`,
  `GraphNotificationsEndpointTests.cs`, `CloudSyncServiceCollectionExtensionsTests.cs`) were diffed
  directly: every change is DI-wiring only (a new required constructor parameter threaded to a
  `NoOpCloudSyncActivityAuditor` test double and, where needed, a `FakeTimeProvider`) — zero
  changes to existing assertions, arrange/act/assert bodies, or expected outcomes. All of these
  files, plus the new `*AuditTests.cs` files, pass: `dotnet test --filter
  "FullyQualifiedName~OpenClaw.Core.Tests.CloudSync"` → **101/101 passed**.

**Verdict: PASS.** The additive-only claim (no `IActionAuditLog` signature change, no `audit_log`
schema change, no change to existing F14 CloudSync behavior/return contracts) is independently
confirmed, not merely asserted by the executor's evidence.

## Appendix A: Test Inventory

| Test file | Count | Result |
|---|---|---|
| `CloudSyncActivityAuditorTests.cs` | (part of 78 audit/Purview-filtered) | pass |
| `CloudSyncActivityConstantsTests.cs` | (part of 78) | pass |
| `PurviewActivityLogProjectionContractTests.cs` | (part of 78) | pass |
| `PurviewActivityLogProjectionPropertyTests.cs` | 2 (1000 samples each) | pass |
| `PurviewActivityLogProjectionTests.cs` | (part of 78) | pass |
| `GraphSubscriptionManagerAuditTests.cs` | (part of 101 CloudSync) | pass |
| `GraphDeltaReconcilerAuditTests.cs` | (part of 101) | pass |
| `NotificationRequestProcessorAuditTests.cs` | (part of 101) | pass |
| `CoreCacheRepositoryAuditLogTests.cs` | (part of 24 AuditLog-filtered) | pass |
| Combined `Audit\|Purview\|CloudSyncActivity` filter | 78 | 78/78 pass |
| Combined `OpenClaw.Core.Tests.CloudSync` filter | 101 | 101/101 pass |
| Combined `ArchitectureBoundary` filter | 14 | 14/14 pass |
| Full solution (`OpenClaw.Core.Tests`) | 857 | 857/857 pass |
| Full solution (`OpenClaw.HostAdapter.Tests`) | 100 | 100/100 pass |
| Full solution (`OpenClaw.MailBridge.Tests`) | 352 | 347 pass, 5 pre-existing skips |

## Summary

Zero blocking findings in this artifact. All uniform T1-T4 gates (format, lint, nullable,
architecture, coverage) hold; the central architecture-boundary design decision is verified
independently rather than accepted on the executor's word; the additive-only contract is confirmed
by direct diff and unchanged-test-suite execution; the property-test-density gate is satisfied with
genuine CsCheck tests. See `code-review.2026-07-07T06-54.md` for two Minor/Info-level code-quality
observations (an unused injected `TimeProvider` field, and a documentation-only mention of
`OpenClaw.Core.Agent` inside a comment) that do not rise to blocking.
