using Microsoft.Extensions.Logging;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// Organizer-reschedule orchestration for <see cref="SchedulingWorker"/> (issue #128, the
/// first calendar-write path). Holds the pure intent-computation and reschedule
/// ActingFlags-snapshot helpers (property-tested), the reschedule audit-record builder,
/// and the evaluation method that runs the spec's exact order: intent -&gt; move-guard
/// consult -&gt; flag gate -&gt; dedupe -&gt; write -&gt; post-write bookkeeping. The partial
/// is host-neutral: it references no <c>CloudGraph</c> type and reaches Graph only through
/// the <see cref="ISchedulingService"/> seam.
/// </summary>
public sealed partial class SchedulingWorker
{
    /// <summary>
    /// The computed organizer-reschedule intent: the event id, the preserved-duration
    /// original interval, and the target interval whose start is the first proposed slot.
    /// </summary>
    internal readonly record struct RescheduleIntent(
        string EventId,
        DateTimeOffset OriginalStartUtc,
        DateTimeOffset OriginalEndUtc,
        DateTimeOffset NewStartUtc,
        DateTimeOffset NewEndUtc
    );

    /// <summary>
    /// Pure eligibility + target computation (extracted for property testing). An intent
    /// exists iff the hydrated <paramref name="meetingEvent"/> is non-null, the owner is
    /// the organizer, the event's <c>Start</c>/<c>End</c> are non-null, the context event
    /// id is non-empty, and at least one proposed slot exists. The target start is the
    /// first slot's start; the duration is preserved from the original event
    /// (<c>End - Start</c>). Any missing precondition yields <see langword="null"/> (no
    /// intent, silent return in the caller).
    /// </summary>
    internal static RescheduleIntent? ComputeRescheduleIntent(
        NormalizedMeetingContext context,
        SchedulingEventDto? meetingEvent,
        IReadOnlyList<CandidateSlot> slots
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(slots);

        if (meetingEvent is null || !context.IsOrganizer)
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

        var newStart = slots[0].Start;
        var duration = originalEnd - originalStart;
        return new RescheduleIntent(
            context.EventId,
            originalStart,
            originalEnd,
            newStart,
            newStart + duration
        );
    }

    /// <summary>
    /// Builds the reschedule-specific ActingFlags snapshot
    /// <c>CalendarWriteEnabled=&lt;bool&gt;;EnableOrganizerReschedule=&lt;bool&gt;</c>. Pure
    /// static helper: the existing <see cref="BuildActingFlags"/> is deliberately not
    /// widened, so the send path's persisted flags string stays byte-identical to pre-F18.
    /// </summary>
    internal static string BuildRescheduleActingFlags(AgentPolicyOptions policyOptions) =>
        $"CalendarWriteEnabled={policyOptions.CalendarWriteEnabled};EnableOrganizerReschedule={policyOptions.EnableOrganizerReschedule}";

    /// <summary>
    /// Composes the reschedule <see cref="ActionAuditRecord"/>, populating the event id,
    /// all four time columns from the <paramref name="intent"/>, the correlation id, the
    /// reschedule action type, and the reschedule ActingFlags snapshot.
    /// </summary>
    private ActionAuditRecord BuildRescheduleAuditRecord(
        string mailbox,
        NormalizedMeetingContext context,
        RescheduleIntent intent,
        string correlationId,
        string resultCode,
        string? errorDetail
    ) =>
        new(
            Mailbox: mailbox,
            MessageId: context.MessageId,
            EventId: intent.EventId,
            ActionType: SentActionKey.OrganizerReschedule,
            ActingFlags: BuildRescheduleActingFlags(options),
            CorrelationId: correlationId,
            ResultCode: resultCode,
            ErrorDetail: errorDetail,
            OriginalStartUtc: intent.OriginalStartUtc,
            OriginalEndUtc: intent.OriginalEndUtc,
            NewStartUtc: intent.NewStartUtc,
            NewEndUtc: intent.NewEndUtc,
            RecordedAtUtc: timeProvider.GetUtcNow()
        );

    /// <summary>
    /// Evaluates the organizer-reschedule path in the spec's exact order (steps 1-6). No
    /// intent returns silently with no audit row (identical to today's behavior for
    /// non-reschedulable messages). The move guard is consulted before the flag gate so a
    /// dry-run audit reflects the true guard decision. A dry-run, guard block, dedupe hit,
    /// or write failure records no <c>series_moves</c> row and no dedupe row.
    /// </summary>
    private async Task EvaluateRescheduleAsync(
        string messageId,
        NormalizedMeetingContext context,
        SchedulingEventDto? meetingEvent,
        OwnerPriority priority,
        IReadOnlyList<CandidateSlot> slots,
        CancellationToken cancellationToken
    )
    {
        // Step 1: intent computation. No intent -> silent, no audit row.
        if (ComputeRescheduleIntent(context, meetingEvent, slots) is not { } intent)
        {
            return;
        }

        var auditMailbox = MailboxUpn();
        // One correlation id per reschedule evaluation, forwarded as the Graph
        // client-request-id (issue #107 rule).
        var correlationId = Guid.NewGuid().ToString();

        // Step 2: move-guard consult, before the flag gate so the dry-run reports the
        // true decision. Occurrence starts are conservatively empty (D2): no calendar-view
        // data is threaded into this path, and fewer known anchors only block more.
        var seriesKey = OneOnOneMoveGuard.ResolveSeriesKey(context);
        var movedStarts = await seriesMoveHistory
            .GetMovedOccurrenceStartsAsync(seriesKey, cancellationToken)
            .ConfigureAwait(false);
        var answers = OneOnOneMoveGuard.ComputeAnswers(
            movedStarts,
            Array.Empty<DateTimeOffset>(),
            intent.OriginalStartUtc
        );
        var ownerPolicy = OwnerSchedulingPolicy.FromOptions(options);
        var canMove = OneOnOneMoveGuard.CanMove(
            context,
            auditMailbox,
            context.MessageFrom,
            priority,
            ownerPolicy,
            answers
        );
        if (!canMove)
        {
            logger.LogInformation(
                "Organizer reschedule for message {MessageId} blocked by the move-history guard; "
                    + "no write regardless of flags.",
                messageId
            );
            await WriteAuditSafelyAsync(
                    BuildRescheduleAuditRecord(
                        auditMailbox,
                        context,
                        intent,
                        correlationId,
                        ActionAuditResultCode.RescheduleBlocked,
                        errorDetail: null
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            return;
        }

        // Step 3: flag gate. Off -> dry-run: log the intended move and audit
        // reschedule_disabled; no Graph call, no write-path token, no series_moves row, no
        // dedupe row.
        if (!CalendarWritePolicy.OrganizerRescheduleAllowed(options))
        {
            logger.LogInformation(
                "Organizer reschedule for message {MessageId} is a dry-run (flag off); intended "
                    + "move {OldStart:o}..{OldEnd:o} -> {NewStart:o}..{NewEnd:o}.",
                messageId,
                intent.OriginalStartUtc,
                intent.OriginalEndUtc,
                intent.NewStartUtc,
                intent.NewEndUtc
            );
            await WriteAuditSafelyAsync(
                    BuildRescheduleAuditRecord(
                        auditMailbox,
                        context,
                        intent,
                        correlationId,
                        ActionAuditResultCode.RescheduleDisabled,
                        errorDetail: null
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            return;
        }

        // Step 4: dedupe. Already recorded -> dedupe_skipped, no write.
        var dedupeKey = SentActionKey.Build(
            auditMailbox,
            messageId,
            SentActionKey.OrganizerReschedule
        );
        if (
            await sentActionStore
                .IsRecordedAsync(dedupeKey, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            logger.LogInformation(
                "Organizer reschedule for message {MessageId} already recorded under dedupe key "
                    + "{DedupeKey}; skipping.",
                messageId,
                dedupeKey
            );
            await WriteAuditSafelyAsync(
                    BuildRescheduleAuditRecord(
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

        // Step 5: write. On failure, the reschedule_failed record is durable before the
        // exception propagates; no series_moves row and no dedupe row are written, so a
        // retry on the next cycle remains possible.
        try
        {
            await schedulingService
                .RescheduleEventAsync(
                    intent.EventId,
                    intent.NewStartUtc,
                    intent.NewEndUtc,
                    correlationId,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await WriteAuditSafelyAsync(
                    BuildRescheduleAuditRecord(
                        auditMailbox,
                        context,
                        intent,
                        correlationId,
                        ActionAuditResultCode.RescheduleFailed,
                        $"{exception.GetType().Name}: {exception.Message}"
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            throw;
        }

        // Step 6: post-write bookkeeping, in order: audit rescheduled; record the pre-move
        // occurrence start in move history; record the dedupe key. Audit-first mirrors the
        // send path's rule that the audit reflects the actual side effect.
        await WriteAuditSafelyAsync(
                BuildRescheduleAuditRecord(
                    auditMailbox,
                    context,
                    intent,
                    correlationId,
                    ActionAuditResultCode.Rescheduled,
                    errorDetail: null
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        await seriesMoveHistory
            .RecordMoveAsync(
                seriesKey,
                intent.OriginalStartUtc,
                timeProvider.GetUtcNow(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await sentActionStore
            .RecordAsync(dedupeKey, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }
}
