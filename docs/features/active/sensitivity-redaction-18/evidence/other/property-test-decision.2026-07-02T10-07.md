# Property-Testing Decision Record — Fix 2 (remediation cycle 1, issue #18)

Timestamp: 2026-07-02T10-07

## Decision

T2 property-test density for the three new pure functions in `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` — `IsSensitive(int?)`, `RedactMessage(MessageDto)`, and `RedactEvent(EventDto)` — is satisfied this cycle by **deterministic exhaustive/parameterized invariant tests** (option (b) of remediation-inputs Fix 2, as directed by the remediation directive) rather than by introducing CsCheck.

## Rationale

- No property-testing library (CsCheck or otherwise) is referenced anywhere in the repository.
- Adding a new package dependency mid-remediation conflicts with the dependency-minimization policy in `.claude/rules/general-code-change.md` ("Use only libraries already approved in the project unless explicitly told to add more").
- Repo-wide CsCheck adoption (the tool named by `.claude/rules/csharp.md`) is a deliberate decision that belongs in its own tracked change, not a remediation cycle.
- The deterministic tests verify the same invariants a property test would, satisfying the T2 gate's intent pending the repo-wide adoption decision.

## Test file

`tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionInvariantTests.cs`

## Invariants verified per function

- **`IsSensitive(int?)`** — full-domain equivalence: `IsSensitive(n) == (n is 2 or 3)` asserted exhaustively over the defined Outlook `olSensitivity` enum range `0..3`, `null`, and out-of-range boundary representatives (`int.MinValue`, `-1`, `4`, `5`, `99`, `int.MaxValue`) — eleven values total.
- **`RedactMessage(MessageDto)`** — for every variant of a parameterized input matrix (fully populated protected fields; all-protected-already-null; `ItemKind` "mail" and "meeting"; `Sensitivity` 2 and 3; differing mechanical flag combinations):
  1. exactly the protected set is transformed (`Subject == "Private message"`; `SenderName`, `SenderEmail`, `SenderEmailResolved`, `FromEmailAddress`, `ToJson`, `CcJson`, `BodyPreview` null; `IsRedacted == true`; `ProtectedFieldsAvailable == false`);
  2. every mechanical field (`BridgeId`, `ItemKind`, `MessageClass`, `ReceivedUtc`, `SentUtc`, `Importance`, `Sensitivity`, `Unread`, `HasAttachments`, `ConversationId`, `MeetingMessageType`) equals the input value;
  3. idempotence: `RedactMessage(RedactMessage(x))` is structurally equivalent to `RedactMessage(x)` (asserted via `Should().BeEquivalentTo`).
- **`RedactEvent(EventDto)`** — for every variant of a parameterized input matrix (populated vs already-null protected fields; `Sensitivity` 2 and 3; populated `Categories` vs empty):
  1. exactly the protected set is transformed (`Subject == "Private appointment"`; `Location`, `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, `BodyPreview`, `BodyFull` null; `Categories` empty and non-null; `IsRedacted == true`; `ProtectedFieldsAvailable == false`);
  2. every mechanical field (`BridgeId`, `GlobalAppointmentId`, `StartUtc`, `EndUtc`, `BusyStatus`, `MeetingStatus`, `IsRecurring`, `Sensitivity`, `SensitivityLabel`, `ResponseStatus`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `ICalUId`, `SeriesMasterId`, `LastModifiedDateTime`) equals the input value;
  3. idempotence: `RedactEvent(RedactEvent(x))` is structurally equivalent to `RedactEvent(x)` (asserted via `Should().BeEquivalentTo`).
