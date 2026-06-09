# Remediation Inputs: openclaw-agent-deterministic-core (#70)

**Entry Timestamp:** 2026-06-09T13-19
**Feature Folder:** `docs/features/active/openclaw-agent-deterministic-core-70`
**Base Branch:** `main` (merge-base `848e326dfdbbb2b533eea290234078aa022cd811`)
**Head Branch:** `open-claw-bridge-wt-2026-06-09-11-54` (`f51468a9b6d652ea71aabf0253f64c10d6d5aaab`)
**Authored By:** feature-review agent
**Cycle severity:** Material PARTIAL (non-blocking). No FAIL findings. No Blocker/Major code-review findings.

## Source Audit Artifacts

These findings derive from this review cycle's artifacts:

- `docs/features/active/openclaw-agent-deterministic-core-70/policy-audit.2026-06-09T13-19.md` (Section 4-C#, Section 8)
- `docs/features/active/openclaw-agent-deterministic-core-70/code-review.2026-06-09T13-19.md` (Findings Table, Minor row)
- `docs/features/active/openclaw-agent-deterministic-core-70/feature-audit.2026-06-09T13-19.md` (AC-12 = PARTIAL)

## Enumerated Fix List

### FIX-1 — Add a property-based test for `RecurringMeetingClassifier.Classify` (AC-12 / T1 property-test density)

- **Finding:** `RecurringMeetingClassifier.Classify(NormalizedMeetingContext ctx, string ownerEmail)` is a pure function but has no CsCheck/FsCheck property-based test. `quality-tiers.md` requires at least one property test per pure function for T1 modules, and AC-12 enumerates this function explicitly. The function currently has six example-based `[TestMethod]` tests at 100% line/branch coverage, so behavior is verified; the unmet item is the property-test gate specifically.
- **Severity:** Material PARTIAL, non-blocking.
- **File to add to:** `tests/OpenClaw.Core.Tests/Agent/RecurringMeetingClassifierTests.cs` (add a property test method) or a new `tests/OpenClaw.Core.Tests/Agent/RecurringMeetingClassifierPropertyTests.cs`.
- **Expected behavior to assert (at least one CsCheck property):**
  - For any generated `NormalizedMeetingContext` (varying `IsRecurring`, organizer, attendee set including/excluding the owner, attendee count across the `>5` boundary) and any non-null `ownerEmail`, `Classify` returns a defined `RecurringMeetingKind` member (never an undefined enum value).
  - Partition invariants hold: a non-recurring context always maps to `NON_RECURRING`; a recurring context whose only non-organizer attendee is the owner maps to `ONE_ON_ONE`; a recurring context with more than five total attendees maps to `RECURRING_FORUM`; any other recurring context maps to `RECURRING_OTHER`.
  - Use a seedable CsCheck generator consistent with the existing `*PropertyTests.cs` files; print the seed on failure per the determinism policy.
- **Verification commands:**
  - `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true`
  - `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~RecurringMeeting" --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
  - `csharpier check .` (or the repo-resolved CSharpier command)
- **Acceptance:** A CsCheck property test for `RecurringMeetingClassifier.Classify` exists and passes; all seven pure functions named in AC-12 each have at least one property test; coverage on `OpenClaw.Core` remains >= 85% line / >= 75% branch with no regression on changed lines.

## Do-Not-Do List

- Do not modify any policy document under `.claude/rules/**` or `.github/instructions/**`.
- Do not weaken, delete, or relax any existing test or assertion to satisfy the gate.
- Do not implement any of the deferred upstream issues #71–#76; they remain out of scope for #70.
- Do not introduce new production behavior, new public APIs, or refactors beyond adding the missing test. This is a test-only remediation.
- Do not add new dependencies; CsCheck is already referenced by `OpenClaw.Core.Tests.csproj`.
- Do not change the `OpenClaw.Core.Agent` namespace layout or the architecture-boundary structure.
- Do not write evidence to non-canonical paths; all evidence goes to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/<kind>/`.

## Non-Triggered Rules (for the record)

- `modified-workflow-needs-green-run`: NOT triggered. The branch diff contains no `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**` changes.
- Evidence-location compliance: PASS. No files written under `artifacts/baselines|qa|evidence|coverage/`.
- File-size limit: PASS. No changed `.cs` file exceeds 500 lines.

## Handoff Note

This is a single-fix, test-only remediation against a non-blocking PARTIAL. The downstream `atomic-planner` should author `remediation-plan.2026-06-09T13-19.md` conforming to `.claude/skills/atomic-plan-contract/SKILL.md` (Phase 0 policy reads + baseline; a phase adding FIX-1; a final QA phase running the full toolchain loop with numeric coverage), then proceed through the standard preflight → execute → reaudit chain. Because the only finding is non-blocking, the orchestrator may alternatively accept the feature with FIX-1 tracked as a fast-follow; that decision belongs to the orchestrator's exit gate, not to this review.
