# Baseline — Untouched Surfaces (Pre-Change State)

Timestamp: 2026-07-02T16-17
Commit SHA: 88ed0f086cd2ae39820ea4f9d12ea8d4475264b7 (git rev-parse HEAD)

## CalendarWriteEnabled gate — `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` (line 288)

```csharp
        if (!options.CalendarWriteEnabled)
        {
            logger.LogInformation(
                "CalendarWriteEnabled is false; not writing the calendar for message {MessageId}.",
                messageId
            );
        }
```

## ActingFlags format — `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Audit.cs` (lines 19–20, usage line 39)

```csharp
    internal static string BuildActingFlags(AgentPolicyOptions policyOptions) =>
        $"SendEnabled={policyOptions.SendEnabled};CalendarWriteEnabled={policyOptions.CalendarWriteEnabled}";
```

Usage:

```csharp
            ActingFlags: BuildActingFlags(options),
```

Output Summary: Pre-change state recorded for both protected surfaces. The Phase 3 / P2-T5 no-behavior-change verification (AC-3, AC-U3) compares against these verbatim quotes; neither file may show a diff at completion.
