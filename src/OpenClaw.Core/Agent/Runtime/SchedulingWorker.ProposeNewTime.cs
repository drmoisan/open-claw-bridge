using Microsoft.Extensions.Logging;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// Attendee propose-new-time orchestration for <see cref="SchedulingWorker"/> (issue #130,
/// the attendee-side calendar-write path mirroring F18). Holds the pure intent-computation
/// and propose ActingFlags-snapshot helpers (property-tested), the propose audit-record
/// builder, and the evaluation method that runs the spec's exact order:
/// intent -&gt; flag gate -&gt; dedupe -&gt; write -&gt; post-write bookkeeping. Unlike the F18
/// organizer path there is <b>no</b> move-guard step, no blocked result code, and no
/// <c>ISeriesMoveHistory</c>/<c>series_moves</c> interaction in any branch — a proposal
/// moves nothing. The partial is host-neutral: it references no <c>CloudGraph</c> type and
/// reaches Graph only through the <see cref="ISchedulingService"/> seam.
/// </summary>
public sealed partial class SchedulingWorker
{
    /// <summary>
    /// The computed attendee propose-new-time intent: the event id, the preserved-duration
    /// original interval, and the proposed interval whose start is the first proposed slot.
    /// </summary>
    internal readonly record struct ProposeNewTimeIntent(
        string EventId,
        DateTimeOffset OriginalStartUtc,
        DateTimeOffset OriginalEndUtc,
        DateTimeOffset ProposedStartUtc,
        DateTimeOffset ProposedEndUtc
    );

    /// <summary>
    /// Pure eligibility + target computation (extracted for property testing). An intent
    /// exists iff the hydrated <paramref name="meetingEvent"/> is non-null, the owner is
    /// <b>not</b> the organizer (<c>context.IsOrganizer == false</c>, the exact mirror of
    /// F18's organizer check), the event allows new-time proposals
    /// (<c>context.AllowNewTimeProposals == true</c>), the event's <c>Start</c>/<c>End</c>
    /// are non-null, the context event id is non-empty, and at least one proposed slot
    /// exists. The proposed start is the first slot's start; the duration is preserved from
    /// the original event (<c>End - Start</c>). Any missing precondition yields
    /// <see langword="null"/> (no intent, silent return in the caller).
    /// </summary>
    internal static ProposeNewTimeIntent? ComputeProposeNewTimeIntent(
        NormalizedMeetingContext context,
        SchedulingEventDto? meetingEvent,
        IReadOnlyList<CandidateSlot> slots
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(slots);

        if (meetingEvent is null || context.IsOrganizer || !context.AllowNewTimeProposals)
        {
            return null;
        }

        if (meetingEvent.Start is not { } originalStart || meetingEvent.End is not { } originalEnd)
        {
            return null;
        }

        if (string.IsNullOrEmpty(context.EventId) || slots.Count == 0)
        {
            return null;
        }

        var proposedStart = slots[0].Start;
        var duration = originalEnd - originalStart;
        return new ProposeNewTimeIntent(
            context.EventId,
            originalStart,
            originalEnd,
            proposedStart,
            proposedStart + duration
        );
    }

    /// <summary>
    /// Builds the propose-new-time-specific ActingFlags snapshot
    /// <c>CalendarWriteEnabled=&lt;bool&gt;;EnableAttendeeProposeNewTime=&lt;bool&gt;</c>. Pure
    /// static helper: neither <see cref="BuildActingFlags"/> nor
    /// <see cref="BuildRescheduleActingFlags"/> is widened, so the send path's and the F18
    /// reschedule path's persisted flags strings stay byte-identical to pre-F19.
    /// </summary>
    internal static string BuildProposeNewTimeActingFlags(AgentPolicyOptions policyOptions) =>
        $"CalendarWriteEnabled={policyOptions.CalendarWriteEnabled};EnableAttendeeProposeNewTime={policyOptions.EnableAttendeeProposeNewTime}";

    /// <summary>
    /// Composes the propose-new-time <see cref="ActionAuditRecord"/>, populating the event
    /// id, all four time columns from the <paramref name="intent"/> (<c>Original*</c> = the
    /// event's current times, <c>New*</c> = the proposed times), the correlation id, the
    /// attendee-propose-new-time action type, and the propose ActingFlags snapshot.
    /// </summary>
    private ActionAuditRecord BuildProposeNewTimeAuditRecord(
        string mailbox,
        NormalizedMeetingContext context,
        ProposeNewTimeIntent intent,
        string correlationId,
        string resultCode,
        string? errorDetail
    ) =>
        new(
            Mailbox: mailbox,
            MessageId: context.MessageId,
            EventId: intent.EventId,
            ActionType: SentActionKey.AttendeeProposeNewTime,
            ActingFlags: BuildProposeNewTimeActingFlags(options),
            CorrelationId: correlationId,
            ResultCode: resultCode,
            ErrorDetail: errorDetail,
            OriginalStartUtc: intent.OriginalStartUtc,
            OriginalEndUtc: intent.OriginalEndUtc,
            NewStartUtc: intent.ProposedStartUtc,
            NewEndUtc: intent.ProposedEndUtc,
            RecordedAtUtc: timeProvider.GetUtcNow()
        );

    /// <summary>
    /// Evaluates the attendee propose-new-time path in the spec's exact order (steps 1-5).
    /// No intent returns silently with no audit row (identical to today's behavior for
    /// non-proposable messages). The attendee path has no move guard and never touches
    /// <c>series_moves</c>: a dry-run, dedupe hit, or write failure records no dedupe row and
    /// makes no <c>ISeriesMoveHistory</c> call in any branch.
    /// </summary>
    private async Task EvaluateProposeNewTimeAsync(
        string messageId,
        NormalizedMeetingContext context,
        SchedulingEventDto? meetingEvent,
        IReadOnlyList<CandidateSlot> slots,
        CancellationToken cancellationToken
    )
    {
        // Step 1: intent computation. No intent -> silent, no audit row.
        if (ComputeProposeNewTimeIntent(context, meetingEvent, slots) is not { } intent)
        {
            return;
        }

        var auditMailbox = MailboxUpn();
        // One correlation id per propose-new-time evaluation, forwarded as the Graph
        // client-request-id (issue #107 rule).
        var correlationId = Guid.NewGuid().ToString();

        // Step 2: flag gate. Off -> dry-run: log the intended proposal and audit
        // propose_new_time_disabled; no Graph call, no write-path token, no dedupe row.
        if (!CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options))
        {
            logger.LogInformation(
                "Attendee propose-new-time for message {MessageId} is a dry-run (flag off); "
                    + "intended proposal {OldStart:o}..{OldEnd:o} -> {NewStart:o}..{NewEnd:o}.",
                messageId,
                intent.OriginalStartUtc,
                intent.OriginalEndUtc,
                intent.ProposedStartUtc,
                intent.ProposedEndUtc
            );
            await WriteAuditSafelyAsync(
                    BuildProposeNewTimeAuditRecord(
                        auditMailbox,
                        context,
                        intent,
                        correlationId,
                        ActionAuditResultCode.ProposeNewTimeDisabled,
                        errorDetail: null
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            return;
        }

        // Step 3: dedupe. Already recorded -> dedupe_skipped, no write.
        var dedupeKey = SentActionKey.Build(
            auditMailbox,
            messageId,
            SentActionKey.AttendeeProposeNewTime
        );
        if (
            await sentActionStore
                .IsRecordedAsync(dedupeKey, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            logger.LogInformation(
                "Attendee propose-new-time for message {MessageId} already recorded under dedupe "
                    + "key {DedupeKey}; skipping.",
                messageId,
                dedupeKey
            );
            await WriteAuditSafelyAsync(
                    BuildProposeNewTimeAuditRecord(
                        auditMailbox,
                        context,
                        intent,
                        correlationId,
                        ActionAuditResultCode.DedupeSkipped,
                        errorDetail: null
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            return;
        }

        // Step 4: write. On failure, the propose_new_time_failed record is durable before
        // the exception propagates; no dedupe row is written, so a retry on the next cycle
        // remains possible.
        try
        {
            await schedulingService
                .ProposeNewMeetingTimeAsync(
                    intent.EventId,
                    intent.ProposedStartUtc,
                    intent.ProposedEndUtc,
                    correlationId,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await WriteAuditSafelyAsync(
                    BuildProposeNewTimeAuditRecord(
                        auditMailbox,
                        context,
                        intent,
                        correlationId,
                        ActionAuditResultCode.ProposeNewTimeFailed,
                        $"{exception.GetType().Name}: {exception.Message}"
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            throw;
        }

        // Step 5: post-write bookkeeping, in order: audit proposed_new_time; record the
        // dedupe key. Audit-first mirrors the send path's rule that the audit reflects the
        // actual side effect. No series_moves interaction.
        await WriteAuditSafelyAsync(
                BuildProposeNewTimeAuditRecord(
                    auditMailbox,
                    context,
                    intent,
                    correlationId,
                    ActionAuditResultCode.ProposedNewTime,
                    errorDetail: null
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        await sentActionStore
            .RecordAsync(dedupeKey, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }
}
