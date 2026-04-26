# Policy Compliance Audit — Issue #45 (OpenClaw availability-query defects)

Timestamp: 2026-04-22T23-55
Reviewer: feature-review-workflow agent
Branch: `bug/openclaw-agent-availability-query-45`
Base (resolved): `development` @ `83459c201e0676c000b486290ea3435cf88e6a42`
Head: `733d959b62fcc4773f20426fa991c9e7e9a4d6a1`
Scope: full branch diff against base (52 files; 2734 insertions; 90 deletions)
Work mode: `full-bug` (authoritative AC source: `issue.md` — section `## Acceptance Criteria`, AC-1..AC-9)

## Rejected Scope Narrowing

No narrowing attempts were detected in the caller prompt. The caller explicitly instructed the reviewer to "Determine scope per your own scope invariant using the branch diff between the merge-base and head SHAs above," which aligns with the scope invariant. No rejections to record.

## Policy Reading Order (applied)

1. `CLAUDE.md` — not present at the repository root; no standing instructions beyond `.claude/rules/`.
2. `.claude/rules/general-code-change.md` — loaded.
3. `.claude/rules/general-unit-test.md` — loaded.
4. `.claude/rules/csharp.md` — loaded (C# files are in scope).
5. `.claude/rules/tonality.md` — loaded (applies to all agent-authored artifacts).

Languages exercised by branch diff: C# (production + tests), YAML (`docker-compose.yml`), Markdown (agent configuration + feature folder docs). No Python, PowerShell, or TypeScript source files changed on the branch; coverage verdicts for those languages are not applicable because zero files changed.

## Coverage Verification (mandatory for every language with changed files)

Coverage artifact expectations defined in the reviewer contract are by language. For C# the canonical path is `artifacts/csharp/coverage.xml`. That exact path does not exist in the repository; however, `dotnet test --collect:"XPlat Code Coverage"` produced Cobertura XML artifacts under `artifacts/coverage/post-change/<guid>/coverage.cobertura.xml` (three files, one per test project) and the evidence artifact `evidence/qa-gates/qa-dotnet-test-coverage.2026-04-22T23-20.md` documents the repo-wide and per-package roll-up.

| Language | Changed files? | Coverage artifact | Repo-wide line coverage | Verdict |
|---|---|---|---|---|
| C# | Yes (3 production, 3 tests added; 1 test double modified) | `artifacts/coverage/post-change/*/coverage.cobertura.xml` + `evidence/qa-gates/qa-dotnet-test-coverage.2026-04-22T23-20.md` | 89.34 % (3443 / 3854) | PASS |
| TypeScript | No | n/a | n/a | N/A (zero changed files) |
| Python | No | n/a | n/a | N/A (zero changed files) |
| PowerShell | No | n/a | n/a | N/A (zero changed files) |

### New / modified C# file coverage (required thresholds: >=90 % for new; no regression and >=80 % for modified)

| File | Status on branch | Line coverage | Required | Verdict |
|---|---|---|---|---|
| `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` | modified (additive tail parameter) | 100.00 % (104 / 104 reported lines) | no regression and >=80 % | PASS |
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | modified | 91.64 % (318 / 347) vs. baseline 91.78 % | no regression and >=80 % | PASS (delta of -0.14 pts is attributable to the extraction of nested records into the new partial, not a new uncovered line; the modified `NormalizeEvent` method itself is at 100 %) |
| `src/OpenClaw.MailBridge/OutlookScanner.Normalized.cs` | new | 100.00 % (7 / 7) | >=90 % | PASS |
| `src/OpenClaw.MailBridge/CacheRepository.cs` | modified | 87.23 % (280 / 321) vs. baseline 85.84 % | no regression and >=80 % | PASS (improved 1.39 pts) |
| `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` | new | 100.00 % (57 / 57) | >=90 % | PASS |
| `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs` | modified (test code, excluded from coverage denominator) | n/a | n/a | PASS (policy excludes tests) |

Every new or modified C# method introduced by this feature is at 100 % line coverage (`MigrateEventsSchemaAsync`, `EventsColumnExistsAsync`, `UpsertEventAsync`, `AddEventParameters`, `InitializeAsync`, `NormalizeEvent`, `ReadEvent`). Evidence: `evidence/qa-gates/coverage-delta.2026-04-22T23-20.md`, `evidence/qa-gates/qa-coverage-ac8.2026-04-22T23-20.md`.

Caveat on artifact path: the reviewer contract lists `artifacts/csharp/coverage.xml` as the canonical C# coverage artifact. The repository's established C# coverage pattern emits Cobertura XML under `artifacts/coverage/post-change/<guid>/coverage.cobertura.xml` instead. This is a convention difference, not a gate failure: coverage evidence is present, parseable, and the repo-wide and per-method numbers in the evidence artifacts are consistent with the Cobertura files on disk (spot-check of the first Cobertura file shows `line-rate="0.8258"` for the MailBridge production package, consistent with the 88.01 % MailBridge project roll-up after applying the best-per-package union across the three result files).

## General Code-Change Policy — Verdicts

| Policy clause | Verdict | Evidence |
|---|---|---|
| Simplicity first | PASS | Changes are textual / additive: one new tail parameter on `EventDto`, one additive COM read in `NormalizeEvent`, one guarded ALTER migration, two partial-class splits to stay under 500 lines, one YAML line, four markdown edits. No deep indirection introduced. |
| Reusability | PASS | The new migration helper follows the existing `AddMessageParameters`/`AddEventParameters` / `ReadMessage` / `ReadEvent` patterns. COM reflection uses the pre-existing `OutlookComHelpers.GetOptionalInt`; no duplication. |
| Extensibility | PASS | `EventDto` remains an immutable record; the new field is keyword/positional-compatible via a default value. No breaking change to the public contract. |
| Separation of concerns | PASS | `CacheRepository.Readers.cs` isolates pure row materialization from the I/O workflow in the primary file; `OutlookScanner.Normalized.cs` isolates the private record types. Both are SOC-only refactors, not behavior changes. |
| Classes vs. standalone functions | PASS | New helpers (`MigrateEventsSchemaAsync`, `EventsColumnExistsAsync`) are private static methods on the repository class — a state-holding type where they belong. No god objects introduced. |
| Toolchain loop (format -> lint -> type-check -> test in a single convergent pass) | PASS | Evidence: `evidence/other/toolchain-csharp.2026-04-22T23-20.md`. CSharpier converged; `dotnet build -warnaserror` clean (0 warnings / 0 errors); nullable analysis clean; tests 280 / 0 / 3. |
| File size <= 500 lines | PASS | Post-change line counts: `OutlookScanner.cs` 495, `CacheRepository.cs` 465, `OutlookScanner.Normalized.cs` 21, `CacheRepository.Readers.cs` 84. All production files are within the 500-line limit. |
| Error handling and logging | PASS | The COM read is delegated to `OutlookComHelpers.GetOptionalInt` which swallows per-event reflection failures to `null` without losing the scan. `MigrateEventsSchemaAsync` fails fast if the ALTER throws after the existence check passes (surfaced by SQLite). |
| Naming | PASS | `ResponseStatus`, `response_status`, `MigrateEventsSchemaAsync`, `EventsColumnExistsAsync` match the C# PascalCase / SQLite snake_case conventions already in use. |
| Public APIs and backward-compat | PASS | `EventDto` change is strictly additive with a default value; all existing positional constructors remain source-compatible. The SQLite migration is additive-only (new nullable column). |
| Dependencies | PASS | No new dependencies introduced. |
| I/O boundaries | PASS | Production code keeps I/O in `CacheRepository` (SQLite) and `OutlookScanner` (COM via `OutlookComHelpers`); tests use in-memory SQLite and fake COM objects. No tests touch disk. |

## General Unit-Test Policy — Verdicts

| Policy clause | Verdict | Evidence |
|---|---|---|
| Independence | PASS | Each new test instantiates its own in-memory SQLite or COM fake; no shared mutable state. |
| Isolation | PASS | Tests target a single behavior each (`ResponseStatus` round-trip; null round-trip; COM-present; COM-absent; migration idempotency; ALTER branch). |
| Fast execution | PASS | Full MailBridge suite runs in ~12 s; new tests are small focused MSTest `[TestMethod]`s. |
| Determinism | PASS | Scanner tests use a fixed `FixedNow` clock seam; SQLite tests use `Mode=Memory;Cache=Shared` with an anchor connection; no wall-clock or network. |
| Readability | PASS | AAA structure with Arrange/Act/Assert comments; FluentAssertions messages explain intent. |
| Coverage >=80 % repo / >=90 % new | PASS | See Coverage Verification table above. |
| Scenario completeness | PASS | AC-4 covers positive, null, declined, idempotent, and ALTER-branch migration scenarios. |
| AAA structure | PASS | Enforced in all five new tests. |
| External dependencies | PASS | No network, filesystem, or external process calls. In-memory SQLite shared-cache is not an external service. |
| No temp files in tests | PASS | In-memory SQLite and in-memory COM fakes only. No `Path.GetTempFileName`, `Path.GetTempPath`, or temp-file creation. |
| Documentation | PASS | Each new test class has an XML `<summary>` explaining AC linkage; each test method has an AAA-pattern comment. |

## C# Policy (language-specific) — Verdicts

| Policy clause | Verdict | Evidence |
|---|---|---|
| Formatting (CSharpier) | PASS | `csharpier.exe format .` converged; second invocation is a no-op. |
| Linting (.NET analyzers) | PASS | `dotnet build -warnaserror` reports 0 warnings / 0 errors across all nine projects. |
| Type checking (nullable) | PASS | `Nullable=enable` with `TreatWarningsAsErrors`; build clean. |
| Testing (MSTest + Moq + FluentAssertions) | PASS | New tests use `[TestClass]`, `[TestMethod]`, and FluentAssertions; match the existing test project pattern. |
| Naming | PASS | See general policy table. |
| Null safety | PASS | `ResponseStatus` is `int?` end-to-end; `GetNullableInt` returns `int?`; `ToDbValue(int?)` maps null to `DBNull`. |
| Composition over inheritance | PASS | Partial classes and static helpers are used; no new inheritance. |
| Async/await | PASS | `MigrateEventsSchemaAsync`, `EventsColumnExistsAsync`, `InitializeAsync` are all `async Task`; each uses `await using` for SQLite connections. |
| Exceptions | PASS | No new broad `catch` blocks in production code. COM read uses the pre-existing narrow `OutlookComHelpers.GetOptionalInt` seam. |
| Public surface minimal | PASS | All new symbols are private or internal. `EventDto.ResponseStatus` (public record member) is the single intentional public addition, required by the HostAdapter contract. |
| XML docs on non-obvious APIs | PASS | `MigrateEventsSchemaAsync` and the two new partial-class files carry XML doc comments explaining intent and file-size rationale. |
| Repo coverage >=80 %; new >=90 % | PASS | 89.34 % repo; 100 % per new method. |
| Deterministic test rules | PASS | Seam-based mocking; fixed clock; in-memory SQLite; no machine-state coupling. |
| DI seams | PASS | Uses existing `IProcessRunner`/`IFileSystem`-style seams (`FakeComActiveObject`, `() => FixedNow`); no new seams needed beyond the repository constructor overload that takes a connection string. |

## Feature-level invariants (from `spec.md` "Boundaries and invariants to preserve")

| Invariant | Verdict | Evidence |
|---|---|---|
| `openclaw.json` `"profile": "coding"` unchanged | PASS | Byte-identical to baseline (blob hash `99125e795e…`). `evidence/qa-gates/invariants-final.2026-04-22T23-20.md`. |
| `docker-compose.yml` hardening unchanged (`read_only`, `cap_drop`, `no-new-privileges`, `noexec,nosuid,nodev` tmpfs) | PASS | Token-by-token byte-identical; line-number ripple only. `evidence/other/compose-tz-and-hardening.2026-04-22T23-20.md`. |
| `EventDto` change strictly additive | PASS | Tail parameter with default value; no existing positional parameter is reordered, renamed, or re-typed. |
| `CacheRepository` migration backward-compatible | PASS | `ALTER TABLE events ADD COLUMN response_status INTEGER NULL` guarded by `PRAGMA table_info` existence check; proven idempotent by `CacheRepositoryMigrationIdempotencyTests`. |
| HostAdapter API remains read-only | PASS | No new write-side endpoints or verbs introduced. |

## Minor Findings (not remediation-required)

- **TOOLS.md — malformed JSON example on single-event endpoint.** `deploy/docker/openclaw-assistant/TOOLS.md` line 114 renders the example response as `{ "bridgeId": "...", "subject": "...", "start": "...", "end": "...", "responseStatus": <int or null>, ... } }` — a stray trailing closing brace that did not exist in the baseline (which was correctly balanced: `{ ... }`). The baseline was balanced; the diff introduces the imbalance. This is a cosmetic markdown / documentation defect that does not affect runtime behavior and does not violate an acceptance criterion (AC-5 requires documentation of `responseStatus` and the value table; both are present). Recommendation: fix in a subsequent minor-audit cycle or a follow-up docs commit. List endpoint (line ~87) is not affected.
- **AGENTS.md — redundant first bullet.** The new `## Availability-Query Protocol` subsection repeats "Before answering any availability or scheduling question…" in both the prelude sentence and bullet 1. Purely stylistic; does not violate a policy or acceptance criterion.

Neither finding is remediation-required under the stated policies. Both are tracked here for transparency.

## Toolchain Loop Evidence

- Format: CSharpier converged (single-pass no-op on second invocation).
- Lint / Build: `dotnet build OpenClaw.MailBridge.sln --nologo -warnaserror` exit 0; 0 warnings, 0 errors.
- Type check: nullable analysis covered by the build with `TreatWarningsAsErrors`.
- Test: `dotnet test OpenClaw.MailBridge.sln --nologo` exit 0; 280 passed / 0 failed / 3 skipped (same three pre-existing platform skips).
- Coverage: repo 89.34 % (>=80 % required), per-new-method 100 % (>=90 % required).

Evidence: `evidence/other/toolchain-csharp.2026-04-22T23-20.md`, `evidence/qa-gates/qa-dotnet-test-coverage.2026-04-22T23-20.md`, `evidence/qa-gates/coverage-delta.2026-04-22T23-20.md`, `evidence/regression-testing/csharp-regression-existing-tests.2026-04-22T23-20.md`.

## Overall Policy Verdict

**PASS.** Every mandatory gate is satisfied on the full branch diff. Two minor documentation / stylistic findings are recorded above but are not remediation-required.
