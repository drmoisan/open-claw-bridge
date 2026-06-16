Timestamp: 2026-06-15T08-48
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
  Baseline ComMessageSource.cs coverage (P0-T5): line=80.1% / branch=60.9%
  Post-change ComMessageSource.cs coverage:       line=94.7% / branch=93.5%

  Test results: Passed=263, Failed=0, Skipped=3 (pre-existing COM/publish skips)

  PASS: ComMessageSource.cs line=94.7% >= 85% threshold
  PASS: ComMessageSource.cs branch=93.5% >= 75% threshold

  Excluded members: None. No [ExcludeFromCodeCoverage] attributes were added.
  Rationale: Coverage thresholds are met without exclusions. The
  PropertyAccessor.GetProperty and GetExchangeUser() invocations are fully
  exercised via reflection-readable fakes (success and throw paths). The only
  uncovered lines (111-114, 150-160) are the outer catch blocks in
  ResolveSenderSmtp and ResolveFromSmtp, which require ResolveAddressEntrySmtp
  to propagate an exception — impossible with fakes because all inner COM calls
  (GetOptionalMemberValue, GetOptionalString) are fail-soft and swallow
  exceptions internally. Applying [ExcludeFromCodeCoverage] to these methods
  wholesale would exclude well-covered code and is therefore not appropriate.
  Coverage passes at 94.7% / 93.5%.
