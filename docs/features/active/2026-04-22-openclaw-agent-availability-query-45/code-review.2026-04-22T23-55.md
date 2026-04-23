# Code Review — Issue #45 (OpenClaw availability-query defects)

Timestamp: 2026-04-22T23-55
Reviewer: feature-review-workflow agent
Branch: `bug/openclaw-agent-availability-query-45` @ `733d959`
Base: `development` @ `83459c2`
Scope: full branch diff (52 files; 2734 insertions; 90 deletions)

## Scope Summary

| Surface | Files | Change character |
|---|---|---|
| HostAdapter contract | `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` | +1 additive tail parameter on `EventDto` |
| Scanner | `src/OpenClaw.MailBridge/OutlookScanner.cs`, `OutlookScanner.Normalized.cs` | +1 COM read call; private nested records extracted to partial file |
| Cache repository | `src/OpenClaw.MailBridge/CacheRepository.cs`, `CacheRepository.Readers.cs` | +1 idempotent migration; +1 column in upsert/read; row-materialization helpers extracted to partial file |
| Tests | `tests/OpenClaw.MailBridge.Tests/*.cs` (3 new; 1 modified) | MSTest + FluentAssertions; in-memory SQLite and fake COM doubles |
| Container | `docker-compose.yml` | +1 line `TZ: "America/New_York"` |
| Agent markdown | `deploy/docker/openclaw-assistant/USER.md`, `AGENTS.md`, `SKILL.md`, `TOOLS.md` | additive subsections / tables; no existing heading modified |
| Documentation | `docs/features/active/...`, `docs/features/potential/promoted/...` | planning, spec, evidence artifacts |

## Code Quality — Findings

### Production — C# contracts

`BridgeContracts.cs`:

- The additive `int? ResponseStatus = null` at the tail of the `EventDto` positional record is the minimum-surface-area change that satisfies the contract requirement while preserving positional-constructor source compatibility.
- No XML doc comment on the new field. Recommendation: add a one-line `<param>` describing the `OlResponseStatus` mapping so downstream `TOOLS.md` and the skill have a single source of truth. Non-blocking.

### Production — OutlookScanner

`OutlookScanner.cs` / `OutlookScanner.Normalized.cs`:

- The new `OutlookComHelpers.GetOptionalInt(item, "ResponseStatus")` call sits alongside the existing reads for `BusyStatus`, `MeetingStatus`, and `Sensitivity`. It uses the same reflection seam and null-on-exception semantics. This is the right layering: per-event COM quirks are contained in a shared helper; the scanner is unaware.
- Moving the nested `NormalizedMessage` / `NormalizedEvent` records into `OutlookScanner.Normalized.cs` to stay under 500 lines is a clean SOC-only extraction. Both records are `private sealed record` inside the same `internal sealed partial class`; external visibility is unchanged. The XML doc on the partial explains the split's motivation.
- Result: the primary file dropped from 503 lines (baseline) to 495 lines (post-change), within the 500-line policy.

### Production — CacheRepository

`CacheRepository.cs` / `CacheRepository.Readers.cs`:

- `MigrateEventsSchemaAsync` uses a `PRAGMA table_info(events)` existence check before `ALTER TABLE`. This is the idiomatic SQLite approach for guarded additive migrations and is proven idempotent by `CacheRepositoryMigrationIdempotencyTests`. The design is strictly additive: no DROP, no RENAME, no column type change.
- The `CREATE TABLE IF NOT EXISTS events(...)` DDL in `InitializeAsync` already lists `response_status INTEGER NULL` at the tail, so a freshly created cache DB is correct without the ALTER. The ALTER path is reserved for pre-existing caches from `development`. Both paths are exercised by the two `CacheRepositoryMigrationIdempotencyTests` methods (fresh-schema idempotency + explicit ALTER branch on a pre-migration schema).
- `UpsertEventAsync` extends the INSERT column list, the VALUES placeholder list, and the `ON CONFLICT … DO UPDATE SET` clause with `response_status`. The `AddEventParameters` binding uses the existing `ToDbValue(int?)` helper so a null carries through as `DBNull` rather than 0.
- `ReadEvent` in the new `CacheRepository.Readers.cs` partial passes `GetNullableInt(reader, "response_status")` into `EventDto` at the new tail slot.
- Extracting `ReadMessage`, `ReadEvent`, `ToDbValue`, and the four column-accessor helpers to `CacheRepository.Readers.cs` cleanly separates pure row materialization from the I/O workflow in the primary file. No behavior change; pure refactor.
- Post-change line counts: primary 465, partial 84 — well within 500 lines.

### Production — docker-compose.yml

- One-line addition of `TZ: "America/New_York"` inside the existing `environment:` block for the `openclaw-agent` service. No other service gets a TZ entry. Hardening flags (`read_only: true`, `cap_drop: [ALL]`, `security_opt: [no-new-privileges:true]`, `tmpfs /tmp`, `tmpfs /.openclaw` with `noexec,nosuid,nodev`) are byte-identical to baseline.
- `docker compose config` validates without warnings (evidence: `evidence/other/compose-config-validate.2026-04-22T23-20.md`).

### Production — Agent markdown

- `USER.md`: new `## Operator` section includes timezone, weekday business-hours window, and a four-tier meeting-tier policy with precedence rules. The pre-existing "Operator note" at the bottom is untouched.
- `AGENTS.md`: new `## Availability-Query Protocol` subsection with four rules. Minor: the first rule repeats the prelude sentence verbatim; purely stylistic, not a policy violation.
- `SKILL.md`: new `## Scheduling Rules` subsection with six mandatory rules labelled (a)–(f). Each AC-2 clause maps one-to-one to one of the six rules.
- `TOOLS.md`: the list endpoint (`GET /v1/calendar`) and the single-event endpoint (`GET /v1/events/{bridgeId}`) both document the new `responseStatus` field and carry an OlResponseStatus value table (0..5).

**Defect — TOOLS.md line 114 (minor).** The updated example response for the single-event endpoint renders as:

```
{ "bridgeId": "...", "subject": "...", "start": "...", "end": "...", "responseStatus": <int or null>, ... } }
```

The trailing `} }` contains one extra closing brace that did not exist in the baseline (baseline: `{ ... }` balanced). This is a cosmetic documentation defect. It does not affect runtime or AC satisfaction (AC-5 requires the field and value table to be documented; both are present). Recommend a trivial follow-up edit to remove the stray `}`. **Non-blocking.**

### Test code

All three new test classes follow the MSTest + FluentAssertions pattern used elsewhere in the suite:

- `OutlookScannerResponseStatusTests.cs` — two tests covering the positive (ResponseStatus = 3) and null cases. Uses the repository's existing `FakeAppointmentItem` and `FakeComActiveObject` doubles (extended with `ResponseStatus` in `MailBridgeRuntimeTestDoubles.cs`).
- `CacheRepositoryResponseStatusTests.cs` — two tests covering declined (4) round-trip and null round-trip. Uses `Mode=Memory;Cache=Shared` SQLite with a unique `Data Source` GUID per test to preserve independence.
- `CacheRepositoryMigrationIdempotencyTests.cs` — two tests: (1) runs `InitializeAsync` twice on a shared in-memory DB and asserts the schema is stable; (2) seeds a pre-migration `events` table without `response_status`, runs `InitializeAsync`, and asserts the ALTER branch added the column.

All five tests use the Arrange–Act–Assert shape explicitly marked in comments. Tests use `repo.InitializeAsync()` rather than calling `ALTER TABLE` directly, which is the correct level of abstraction (integration-grade unit tests against the repository interface rather than against private helpers). No temp files; no network; no machine-state coupling; no mutable globals.

One small observation: `CacheRepositoryResponseStatusTests.BuildEvent` constructs the `EventDto` using named parameters — an appropriate choice that insulates the test from future additive parameter changes.

### Test-double delta

`tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs` adds a single line: `public int? ResponseStatus { get; init; }` on `FakeAppointmentItem`. This is the minimum-surface change needed to let the reflection-based `OutlookComHelpers.GetOptionalInt` see the member on the fake. Consistent with the existing init-only pattern used elsewhere in the double.

## Architectural observations

- **Layering is preserved.** The new field flows through the expected layers: Outlook COM property → `OutlookComHelpers.GetOptionalInt` → `NormalizeEvent` → `EventDto` → `CacheRepository` (upsert column + migration + read) → HostAdapter HTTP → skill prompt. Each layer applies exactly one concern.
- **Skill-side filtering** (`responseStatus == 4` excluded from busy holds) is documented in `SKILL.md` rule (d), in `AGENTS.md` availability-protocol rule 4, and in `TOOLS.md` with the value table. This is consistent triangulation across the three documents a skill model reads.
- **Business-hours and tier-policy enforcement** are pushed to prompt-level rules in `USER.md` / `SKILL.md` / `AGENTS.md` rather than into HostAdapter code. This is consistent with the HostAdapter's read-only design and avoids embedding operator-specific policy in infrastructure code.
- **Timezone anchoring** is split between two layers: the container `TZ` env var anchors container-internal wall-clock / log rendering, and the skill rule (a) requires model-side local-time rendering. Both layers are needed: the env var alone does not govern how the model renders responses.

## Readability and maintainability

- C# naming, nullability, async/await, and FluentAssertions usage align with the project's established style.
- XML doc comments are present on non-obvious types: `MigrateEventsSchemaAsync`, the two new partial-class files, and each new test class's `<summary>`.
- The SQL strings are multi-line raw strings; column lists are consistently ordered between INSERT, VALUES, and ON CONFLICT DO UPDATE SET. A reviewer can diff them visually without false positives.
- The partial-class split is annotated with XML `<summary>` explaining the 500-line guideline — future maintainers will understand why the split exists.

## Tonality compliance

All new agent-authored artifacts (policy-audit, code-review, feature-audit) use declarative, measured phrasing. Evidence-first wording matches the strength of available evidence (PASS is claimed only where an artifact or diff confirms it; AC-9 is labeled PENDING_MANUAL_VERIFY because automated work alone cannot confirm operator repro). No hyperbole or metaphor.

## Overall Code-Review Verdict

**APPROVE with minor cleanup recommended.** The production C#, YAML, and markdown changes are well-scoped, additive, and consistent with project conventions. The test suite is focused, deterministic, and covers positive, null, declined, idempotent-migration, and ALTER-branch cases. The one cosmetic defect (TOOLS.md line 114 extra `}`) is cleanup-grade and does not block merge; it should be folded into a follow-up commit or a subsequent minor-audit cycle.
