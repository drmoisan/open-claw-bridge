using Microsoft.Extensions.Logging;
using OpenClaw.Core.Agent;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// The deterministic pipeline stages for <see cref="SchedulingWorker"/>, kept in a
/// separate partial to hold each file under the 500-line limit.
/// </summary>
public sealed partial class SchedulingWorker
{
    private async Task ProcessMessageAsync(string messageId, CancellationToken cancellationToken)
    {
        // Hydrate (I/O through the ISchedulingService seam only).
        var message = await schedulingService
            .GetSchedulingMessageAsync(messageId, cancellationToken)
            .ConfigureAwait(false);
        if (message is null)
        {
            logger.LogDebug("Message {MessageId} could not be hydrated; skipping.", messageId);
            return;
        }

        var meetingEvent = await schedulingService
            .GetEventForMessageAsync(messageId, cancellationToken)
            .ConfigureAwait(false);

        // Normalize (D1).
        var context = MeetingContextNormalizer.Normalize(MailboxUpn(), message, meetingEvent);

        // Triage (D2).
        var triagePolicy = TriagePolicy.FromOptions(options);
        var triage = TriageEngine.Triage(context, triagePolicy);
        logger.LogInformation(
            "Triage for message {MessageId}: {Decision} ({Reasons}).",
            messageId,
            triage.Decision,
            string.Join("; ", triage.Reasons)
        );

        // Priority/recurrence/move layer (D3) runs only for AUTO_COORDINATE/HUMAN_APPROVAL.
        if (!SchedulingGate.RequiresPriorityLayer(triage.Decision))
        {
            return;
        }

        var ownerPolicy = OwnerSchedulingPolicy.FromOptions(options);
        var priority = OwnerPriorityClassifier.Classify(context, ownerPolicy);
        var recurringKind = RecurringMeetingClassifier.Classify(context, MailboxUpn());
        logger.LogInformation(
            "Priority for message {MessageId}: {Priority}; recurring kind {Kind}.",
            messageId,
            priority,
            recurringKind
        );

        await ProposeAndActAsync(messageId, context, priority, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ProposeAndActAsync(
        string messageId,
        NormalizedMeetingContext context,
        OwnerPriority priority,
        CancellationToken cancellationToken
    )
    {
        // Slot proposal (D4) requires mailbox settings and free/busy. These endpoints are
        // deferred (#74/#75); when unavailable the worker logs and stops short of the
        // proposal rather than failing the whole cycle.
        MailboxSettingsDto mailboxSettings;
        FreeBusyScheduleDto freeBusy;
        try
        {
            mailboxSettings = await schedulingService
                .GetMailboxSettingsAsync(cancellationToken)
                .ConfigureAwait(false);
            freeBusy = await schedulingService
                .GetFreeBusyAsync(
                    timeProvider.GetUtcNow(),
                    timeProvider.GetUtcNow().AddDays(5),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            logger.LogInformation(
                exception,
                "Slot proposal for message {MessageId} is deferred until the mailbox-settings "
                    + "and free/busy endpoints land (#74/#75); decision computed without proposal.",
                messageId
            );
            return;
        }

        var workingHours = WorkingHoursPolicy.FromOptions(options);
        var request = new SchedulingRequest(
            Duration: TimeSpan.FromMinutes(30),
            RequestedPriority: priority,
            Horizon: TimeSpan.FromDays(5),
            RequesterEmail: context.MessageFrom
        );
        var slots = SlotProposer.ProposeTimes(
            request,
            mailboxSettings,
            freeBusy,
            workingHours,
            timeProvider
        );
        logger.LogInformation(
            "Proposed {SlotCount} candidate slots for message {MessageId}.",
            slots.Count,
            messageId
        );

        // Kill switches gate all side effects. The deterministic pipeline above always
        // computes and logs; only the outbound actions are gated.
        if (!options.SendEnabled)
        {
            logger.LogInformation(
                "SendEnabled is false; not sending mail for message {MessageId}.",
                messageId
            );
        }
        else
        {
            // Send idempotency (issue #101): consult the durable store before sending and
            // record after a successful send so a restart does not resend the proposal. A
            // thrown send exception propagates before the record step, preserving the
            // ProcessMessageSafelyAsync per-message isolation.
            var dedupeKey = SentActionKey.Build(
                MailboxUpn(),
                messageId,
                SentActionKey.ProposalReply
            );
            if (
                await sentActionStore
                    .IsRecordedAsync(dedupeKey, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                logger.LogInformation(
                    "Send for message {MessageId} already recorded under dedupe key {DedupeKey}; skipping.",
                    messageId,
                    dedupeKey
                );
                return;
            }

            await schedulingService
                .SendMailAsync(BuildProposalReply(context, slots), cancellationToken)
                .ConfigureAwait(false);
            await sentActionStore
                .RecordAsync(dedupeKey, timeProvider.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (!options.CalendarWriteEnabled)
        {
            logger.LogInformation(
                "CalendarWriteEnabled is false; not writing the calendar for message {MessageId}.",
                messageId
            );
        }
    }

    private string MailboxUpn() =>
        options.InternalDomain.Length > 0 ? $"owner@{options.InternalDomain}" : "owner";

    private static SendMailRequest BuildProposalReply(
        NormalizedMeetingContext context,
        IReadOnlyList<CandidateSlot> slots
    )
    {
        var body =
            slots.Count == 0
                ? "No candidate times are currently available."
                : "Proposed times: "
                    + string.Join(
                        "; ",
                        slots.Select(slot => $"{slot.Start:yyyy-MM-dd HH:mm} {slot.TimeZoneId}")
                    );
        var recipient = new AttendeeDto(string.Empty, context.MessageFrom);
        return new SendMailRequest(
            Subject: $"Re: {context.Subject}",
            BodyContent: body,
            BodyContentType: "text",
            ToRecipients: new[] { recipient },
            CcRecipients: Array.Empty<AttendeeDto>(),
            InReplyToMessageId: context.MessageId
        );
    }
}
