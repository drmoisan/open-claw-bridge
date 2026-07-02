# Remediation Inputs: sensitivity-redaction (#18, co-delivers #20)

- **Cycle entry timestamp:** 2026-07-02T09-45
- **Branch:** `feature/sensitivity-redaction-18` @ `d267c663b0ea966609a97dc9e98e9e5ccbdc8cff`
- **Base:** `main` @ merge-base `8c969f1a6e96120dd95f835a289c8b185abee202`
- **Produced by:** feature-review agent (review cycle 2026-07-02T09-45)
- **Source audit artifacts:**
  - `docs/features/active/sensitivity-redaction-18/policy-audit.2026-07-02T09-45.md` (Blocking finding: Section 1.2 / Section 8 item 1; Major finding: Section 4 / Section 8 item 2)
  - `docs/features/active/sensitivity-redaction-18/code-review.2026-07-02T09-45.md` (Findings Table rows 1-2; rows 3-5 are Minor, optional)
  - `docs/features/active/sensitivity-redaction-18/feature-audit.2026-07-02T09-45.md` (spec item T1 evaluated PARTIAL)
  - Reviewer coverage evidence: `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review.2026-07-02T09-45.md`

## Remediation-Required Findings (enumerated fix list)

### Fix 1 (Blocking) — Close the new-file branch-coverage gap: sensitive meeting-message normalization is untested

- **File(s):** `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationTests.cs` (extend) and/or a new test class in `tests/OpenClaw.MailBridge.Tests/`; test-double support in `tests/OpenClaw.MailBridge.Tests/SensitivityRedactionTestDoubles.cs` if a meeting-typed double variant is needed. **No production code change expected.**
- **Problem:** NEW production file `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` has branch coverage 71.43% (10/14 conditions), below the uniform 75% new-code branch gate (`.claude/rules/quality-tiers.md`). Uncovered conditions (reviewer cobertura):
  - Line 63 (`NormalizeSensitiveMessage`, `new MessageDto(...)`): 3/6 — the `isMeeting == true` arms of `isMeeting ? "meeting" : "mail"` and `MeetingMessageType: isMeeting ? GetOptionalInt(item, "MeetingType") : null`, and the true short-circuit of `GetOptionalBool(item, "Attachments") || GetOptionalBool(item, "HasAttachments")`.
  - Line 170 (`IsMeetingItem` fallback): 1/2 — the `string.IsNullOrWhiteSpace(messageClass) == true` short-circuit.
- **Expected behavior after fix:** scanner-level tests normalize a `Sensitivity` 2 and/or 3 **meeting message** (double with `MessageClass = "IPM.Schedule.Meeting.Request"` and a readable mechanical `MeetingType` member; optionally a variant whose runtime type name contains "Meeting" with a null/whitespace `MessageClass` to cover line 170) and assert: full Group A redaction disposition, `ItemKind == "meeting"`, retained non-null `MeetingMessageType`, and (never-ingest) zero protected-member accesses. Optionally include an `Attachments == true` double variant for the `||` short-circuit. Resulting `OutlookScanner.Redaction.cs` branch coverage must be **>= 75%** (covering the two ternary true-arms and line 170 alone yields 13/14 = 92.9%).
- **Verification commands:**
  - `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerSensitivityNormalizationTests"` (EXIT 0)
  - Full suite with coverage: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (EXIT 0), then parse the MailBridge cobertura per-file and record **line AND branch** percentages for `OutlookScanner.Redaction.cs` in the coverage evidence (must show branch >= 75%).
  - `csharpier check .` and `dotnet build OpenClaw.MailBridge.sln` clean per the standard loop.

### Fix 2 (Major) — T2 property-test density for the three new pure functions

- **File(s):** new test file (e.g., `tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionPropertyTests.cs`); `Directory.Packages.props` + `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` if adding the CsCheck package.
- **Problem:** `OpenClaw.MailBridge` is T2; `.claude/rules/quality-tiers.md` requires >= 1 property-based test per pure function for T2. This feature adds three new pure functions — `IsSensitive(int?)`, `RedactMessage(MessageDto)`, `RedactEvent(EventDto)` — with no property tests. CsCheck (the tool named by `.claude/rules/csharp.md`) is not referenced anywhere in the repository.
- **Expected behavior after fix (either option):**
  - **Option A (preferred):** add CsCheck (policy-named, so policy-sanctioned as a dependency; pin the version centrally in `Directory.Packages.props`) and one property test per function, e.g.: for all `int? n`, `IsSensitive(n) == (n is 2 or 3)`; for arbitrary `MessageDto`, `RedactMessage` output has exactly the protected set nulled/replaced, both flags set, and every mechanical field equal to the input; the equivalent for `RedactEvent` including `Categories` empty-not-null. Transforms must also be idempotent (`Redact(Redact(x)) == Redact(x)`) — a cheap extra property.
  - **Option B (fallback):** a dated, recorded exception in the policy audit trail documenting that the repository's property-testing harness bootstrap is deferred, with an issue reference for the repo-wide CsCheck adoption. Option B requires explicit orchestrator/owner acceptance.
- **Verification commands:** `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~PropertyTests"` (EXIT 0, Option A); full loop clean.

### Optional (Minor, may be bundled with Fix 1 at planner discretion — not exit-blocking)

- Exercise or remove the unused `ThrowOnProtectedAccess` capability in `SensitivityRedactionTestDoubles.cs` (one hard never-ingest test per item kind with the flag set `true` is the cheap strengthening path).
- No action required this cycle on the mechanical-field construction duplication and the duplicated `IsOrganizer` derivation (code-review Minor findings 3-4); recorded for future refactoring pressure only.

## Do-Not-Do List

- Do not modify production redaction/shaping logic to chase coverage; the Blocking finding is a test gap, and delivered production behavior is verified correct for all tested paths.
- Do not weaken, remove, or invert any existing assertion; do not relax `mailbridge.runsettings` coverage configuration or add coverage exclusions for production files.
- Do not add sleeps, wall-clock reads, temporary files, or live-COM dependencies to new tests (use the existing fake-COM double pattern and fixed-clock seam).
- Do not edit policy documents under `.claude/rules/` or the AC text in `spec.md`/`user-story.md`/`issue.md` (statuses are reconciled by the re-audit, not by rewording criteria).
- Do not grow `OutlookScanner.cs` (462/500 lines) or any file past the 500-line cap; new tests go in test files under the cap.
- No scope creep: no new production features, no refactors of untouched files, no dependency additions beyond CsCheck (Option A of Fix 2).

## Exit Condition

Re-audit (policy + code review + feature audit at a new exit timestamp) shows:
- `OutlookScanner.Redaction.cs` branch coverage >= 75% with per-file line AND branch evidence recorded;
- Fix 2 resolved via delivered property tests or an explicitly accepted exception;
- full toolchain single-pass clean at the new head; blocking_count == 0.
