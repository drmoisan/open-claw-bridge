using Microsoft.Extensions.Logging;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// Audit-emission helpers for <see cref="SchedulingWorker"/> (issue #107): the acting-flags
/// snapshot builder, the Stage 0 record builder, and <see cref="WriteAuditSafelyAsync"/> —
/// the one sanctioned resilience boundary for audit writes (spec D4).
/// </summary>
public sealed partial class SchedulingWorker
{
    /// <summary>
    /// Builds the deterministic acting-flags snapshot
    /// <c>SendEnabled=&lt;bool&gt;;CalendarWriteEnabled=&lt;bool&gt;</c> from the policy
    /// options (spec D3). Pure static helper: recording both Stage 0 switches captures the
    /// acting flag and its suppression context in one self-describing value.
    /// </summary>
    internal static string BuildActingFlags(AgentPolicyOptions policyOptions) =>
        $"SendEnabled={policyOptions.SendEnabled};CalendarWriteEnabled={policyOptions.CalendarWriteEnabled}";

    /// <summary>
    /// Composes the Stage 0 <see cref="ActionAuditRecord"/> for one outbound-action decision
    /// point. <see cref="ActionAuditRecord.RecordedAtUtc"/> comes from the injected
    /// <see cref="TimeProvider"/>; the Stage 2 time columns stay null for send actions.
    /// </summary>
    private ActionAuditRecord BuildAuditRecord(
        string mailbox,
        NormalizedMeetingContext context,
        string correlationId,
        string resultCode,
        string? errorDetail
    ) =>
        new(
            Mailbox: mailbox,
            MessageId: context.MessageId,
            EventId: context.EventId,
            ActionType: SentActionKey.ProposalReply,
            ActingFlags: BuildActingFlags(options),
            CorrelationId: correlationId,
            ResultCode: resultCode,
            ErrorDetail: errorDetail,
            OriginalStartUtc: null,
            OriginalEndUtc: null,
            NewStartUtc: null,
            NewEndUtc: null,
            RecordedAtUtc: timeProvider.GetUtcNow()
        );

    /// <summary>
    /// Writes the given audit record, swallowing any store failure so the audit sink never
    /// breaks message processing. This is the one sanctioned resilience boundary for audit
    /// writes (spec D4): the failure is logged at Error with the message id and result code,
    /// and processing continues. <see cref="OperationCanceledException"/> is excluded so
    /// shutdown still propagates.
    /// </summary>
    private async Task WriteAuditSafelyAsync(ActionAuditRecord record, CancellationToken ct)
    {
        try
        {
            await actionAuditLog.RecordAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Audit write failed for message {MessageId} with result {ResultCode}; continuing.",
                record.MessageId,
                record.ResultCode
            );
        }
    }
}
