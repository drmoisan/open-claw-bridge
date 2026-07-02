# Mapper Tests Pass — MapSendMailRequest (P2-T4)

Timestamp: 2026-07-02T11-04
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingDtoMapper"
EXIT_CODE: 0
Output Summary:
- Passed! - Failed: 0, Passed: 31, Skipped: 0, Total: 31 (OpenClaw.Core.Tests.dll).
- Includes the 4 new example-based `MapSendMailRequest` tests in `SchedulingDtoMapperTests.cs` (full field mapping, empty/whitespace recipient name -> null, empty CC list -> null, null argument throws) and the CsCheck property test `MapSendMailRequest_PreservesRecipientsSetsSaveAndNeverMutatesInput` in `SchedulingDtoMapperPropertyTests.cs` (1000 iterations, seeded; failing seed printed on Sample failure per CsCheck default).
- Remaining passes are the pre-existing MapMessage/MapEvent mapper tests, all unaffected.
