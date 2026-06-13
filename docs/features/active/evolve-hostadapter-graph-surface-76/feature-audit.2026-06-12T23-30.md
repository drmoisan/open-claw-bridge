# Feature Audit: evolve-hostadapter-graph-surface (#76)

**Audit Date:** 2026-06-12
**Feature Folder:** `docs/features/active/evolve-hostadapter-graph-surface-76`
**Base Branch:** `main` (`3041d083691cd77b2b2e888580fc9f2ab8bc611f`)
**Head Branch:** `open-claw-bridge-wt-2026-06-12-22-19` (`e3bc4506e1ebce0080e057306b91ffbbb77fd945`)
**Work Mode:** `full-feature`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (commit `3041d083691cd77b2b2e888580fc9f2ab8bc611f`)
- **Head branch/commit:** `open-claw-bridge-wt-2026-06-12-22-19` (commit `e3bc4506e1ebce0080e057306b91ffbbb77fd945`)
- **Merge base:** `3041d083691cd77b2b2e888580fc9f2ab8bc611f` (identical to base tip)
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/**`
  - Additional evidence: direct `git diff 3041d08..e3bc450`, independent build/test/coverage runs
- **Feature folder used:** `docs/features/active/evolve-hostadapter-graph-surface-76`
- **Requirements source:** `user-story.md` (authoritative checkbox ACs) and `spec.md` (Definition of Done prose).
- **Work mode resolution note:** `issue.md` is absent from the feature folder. Per the work-mode contract, a missing/malformed marker fails closed to `full-feature`; the orchestrator prompt also explicitly specified `full-feature`. `full-feature` resolves AC sources to `spec.md` and `user-story.md`. The checkbox acceptance criteria live under `## Acceptance Criteria` in `user-story.md`.
- **Scope note:** Audit scope is the full branch diff vs the resolved base, not any plan/task subset. The PR-context summary's "Core logic changes: 0 files" classification was rejected as inaccurate (seven `src/**` C# files changed); the audit used the direct branch diff. No caller scope-narrowing instruction was present.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/evolve-hostadapter-graph-surface-76/user-story.md` — primary (checkbox ACs)
- `docs/features/active/evolve-hostadapter-graph-surface-76/spec.md` — secondary (prose Definition of Done; not a separate checkbox AC set)

### Acceptance criteria (from user-story.md `## Acceptance Criteria`)

1. HostAdapter exposes the Graph-shaped routes (`/users/{id}/messages`, `/users/{id}/messages/{messageId}`, `/users/{id}/calendarView`, `/users/{id}/events/{eventId}`, and meeting-requests as a messages-filtered query on `meetingMessageType`) and no longer serves the `/v1/*` bespoke routes (the `/status` operational probe excepted).
2. `IHostAdapterClient` and `HostAdapterHttpClient` call the Graph-shaped endpoints and receive envelope-wrapped (`ApiEnvelope<T>`) results, with `ListMeetingRequestsAsync` retained on the interface.
3. `OpenClawOptions.HostAdapter.BaseUrl` default no longer contains `/v1/`.
4. The adapter version reports `1.0.0` (via `meta.adapterVersion`) to signal the breaking change.
5. `MailboxId` (default `"me"`) is configurable on `HostAdapterOptions` and is used to render the `{id}` path segment.
6. Existing contract/endpoint tests pass against the new routes (HostAdapter.Tests and Core.Tests updated, not weakened).
7. Line coverage >= 85% and branch coverage >= 75% on changed code; no coverage regression on changed lines.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| 1 | Graph-shaped routes served; no `/v1/*` (except `/status`) | PASS | `src/OpenClaw.HostAdapter/Program.cs` registers `/status`, `/users/{id}/messages`, `/users/{id}/messages/{messageId}`, `/users/{id}/calendarView`, `/users/{id}/events/{eventId}`; the messages handler branches on the `meetingMessageType ne null` `$filter` predicate to the meeting-requests command. No `/v1/` string remains in `src/OpenClaw.HostAdapter`. | `grep -rn '"/v1/' src/OpenClaw.HostAdapter/` (no matches); `grep -n 'MapGet' src/OpenClaw.HostAdapter/Program.cs` (5 routes) | Meeting-requests is a filtered messages query, matching the spec mapping. |
| 2 | Client calls Graph endpoints; envelope-wrapped; `ListMeetingRequestsAsync` retained | PASS | `HostAdapterHttpClient.cs` emits the six Graph-shaped relative paths returning `ApiEnvelope<...>`; `IHostAdapterClient` retains all six members including `ListMeetingRequestsAsync` (XML-doc only changed). `HostAdapterHttpClientTests` assert `/users/me/...` paths and renamed params; all pass. | `dotnet test tests/OpenClaw.Core.Tests/...` (178/178); diff of `IHostAdapterClient.cs` (signatures unchanged) | Contract-compat evidence corroborates member parity. |
| 3 | `BaseUrl` default no longer contains `/v1/` | PASS | `CoreOptions.cs`: `BaseUrl` default changed to `http://host.docker.internal:4319/`. Test factory and client tests updated to the `/v1/`-less base URL. | diff of `src/OpenClaw.Core/CoreOptions.cs` | D6 honored. |
| 4 | Adapter version reports `1.0.0` via `meta.adapterVersion` | PASS | `OpenClaw.HostAdapter.csproj` declares `<Version>1.0.0</Version>`; `FormatAdapterVersion` renders the 3-component `1.0.0`; `HostAdapterVersionTests.DefaultAdapterVersion_should_report_1_0_0...` asserts `"1.0.0"` and passes. | `grep -n 'Version' src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj`; `dotnet test tests/OpenClaw.HostAdapter.Tests/...` | The in-process test factory overrides the envelope version to `"test-version"`, so the assembly-derived value is asserted directly (documented in the test). |
| 5 | `MailboxId` (default `"me"`) configurable and renders `{id}` | PASS | `HostAdapterOptions.cs` adds `MailboxId` (default `"me"`) with `Program.cs` post-configure normalization; `HostAdapterHttpClient.cs` sources `{id}` from `options.HostAdapter.MailboxId` (URL-escaped). Client tests assert `/users/me/...`. | diff of `HostAdapterOptions.cs`, `HostAdapterHttpClient.cs`; `dotnet test tests/OpenClaw.Core.Tests/...` | Server-side the `{id}` segment is presentational (not used to scope data); see code-review Minor finding. |
| 6 | Existing contract/endpoint tests pass against new routes; updated not weakened | PASS | All affected tests updated to Graph-shaped routes and pass: HostAdapter.Tests 74/74, Core.Tests 178/178. New tests strengthen coverage (branch dispatch, version). No tests removed or skipped. | `dotnet test tests/OpenClaw.HostAdapter.Tests/...` (74/74); `dotnet test tests/OpenClaw.Core.Tests/...` (178/178) | Updated assertions verify stronger Graph-shaped shapes. |
| 7 | Changed-code line >= 85% / branch >= 75%; no regression on changed lines | PASS | Changed-code aggregate 98.85% line / 79.41% branch; package cobertura HostAdapter 97.08% line / 86.30% branch, Core 98.58% line / 90.32% branch; no changed line regressed. | inspected `evidence/qa-gates/coverage-delta.2026-06-12T23-17.md`; cobertura under `tests/**/TestResults/*/coverage.cobertura.xml` | Two unreachable defensive branches in `FormatAdapterVersion` explain residual branch headroom; aggregate above threshold. |

---

## Summary

**Overall Feature Readiness:** PASS

**Criteria summary:**
- **PASS:** 7 criteria
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

1. None.

**Recommended follow-up verification steps:**

1. Repair or re-pin the CSharpier dotnet-tool so formatting is verified under the pinned version (non-blocking; code-review Minor finding).
2. Optionally add a one-line comment in `Program.cs` noting the `{id}` segment is accepted for Graph route-shape parity and is not used to scope data in the local adapter.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules, all seven criteria are evaluated PASS. In `user-story.md` they were already recorded as `[x]` by the executor during delivery; this audit confirms each PASS against independent evidence and leaves the existing checked state intact. No additional source-file edit was required (the boxes are already checked and the evaluation confirms them).

### AC Status Summary

- Source: `docs/features/active/evolve-hostadapter-graph-surface-76/user-story.md`
- Total AC items: 7
- Checked off (delivered): 7
- Remaining (unchecked): 0
- Items remaining: None.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `user-story.md` | 7 | 7 | 0 | Checkbox-backed; all PASS and confirmed; already checked at delivery. |
| `spec.md` | 0 | 0 | 0 | Prose Definition of Done; not a separate checkbox AC set. Behavior covered by the seven user-story criteria above. |

No new source-file checkbox change was made because all seven authoritative criteria were already checked `[x]` and this audit's independent evaluation confirms each as PASS.
