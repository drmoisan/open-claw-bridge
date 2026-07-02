using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Sensitivity-based redaction for <see cref="OutlookScanner"/> (issue #18, co-delivers #20).
/// Lives in its own partial-class file because <c>OutlookScanner.cs</c> is at its 465-line
/// baseline and must not grow. The members here are pure transforms over already-constructed
/// DTOs and already-read integers: no COM object is accepted and no COM helper is invoked.
/// </summary>
internal sealed partial class OutlookScanner
{
    /// <summary>Placeholder subject stored for redacted messages (master §2.4).</summary>
    internal const string RedactedMessageSubject = "Private message";

    /// <summary>Placeholder subject stored for redacted events (master §2.4).</summary>
    internal const string RedactedEventSubject = "Private appointment";

    /// <summary>
    /// Returns <c>true</c> only for Outlook <c>Sensitivity</c> 2 (Private) and 3 (Confidential).
    /// Values 0, 1, <c>null</c>, and anything out of range are non-sensitive; the implementation
    /// must not assume exhaustive enum coverage (spec Group C boundary contract).
    /// </summary>
    internal static bool IsSensitive(int? sensitivity) => sensitivity is 2 or 3;

    /// <summary>
    /// Applies the Group A message redaction disposition: placeholder subject; sender identity,
    /// recipient lists, and body preview nulled; <c>IsRedacted = true</c>;
    /// <c>ProtectedFieldsAvailable = false</c>. All mechanical fields (<c>BridgeId</c>,
    /// <c>ItemKind</c>, <c>MessageClass</c>, dates, flags, ids, <c>Sensitivity</c>) are retained
    /// unchanged by the record <c>with</c> expression.
    /// </summary>
    internal static MessageDto RedactMessage(MessageDto message) =>
        message with
        {
            Subject = RedactedMessageSubject,
            SenderName = null,
            SenderEmail = null,
            SenderEmailResolved = null,
            FromEmailAddress = null,
            ToJson = null,
            CcJson = null,
            BodyPreview = null,
            IsRedacted = true,
            ProtectedFieldsAvailable = false,
        };

    /// <summary>
    /// Never-ingest normalization for a Sensitivity 2/3 message (issue #18, spec Group A):
    /// constructs the DTO from mechanical COM members only — no <c>ShapePreview</c>, no COM
    /// <c>Body</c> read, no recipient enumeration, no sender SMTP resolution — then applies
    /// <see cref="RedactMessage"/> so the redacted disposition has a single source of truth.
    /// </summary>
    private NormalizedMessage NormalizeSensitiveMessage(
        object item,
        string entryId,
        int? sensitivity
    )
    {
        var messageClass = OutlookComHelpers.GetOptionalString(item, "MessageClass");
        var isMeeting = IsMeetingItem(item, messageClass);
        var dto = new MessageDto(
            BridgeIdCodec.MessageId(entryId, isMeeting),
            isMeeting ? "meeting" : "mail",
            null,
            OutlookComHelpers.GetOptionalDateTimeOffset(item, "ReceivedTime"),
            OutlookComHelpers.GetOptionalDateTimeOffset(item, "SentOn"),
            OutlookComHelpers.GetOptionalInt(item, "Importance"),
            sensitivity,
            OutlookComHelpers.GetOptionalBool(item, "Unread"),
            OutlookComHelpers.GetOptionalBool(item, "Attachments")
                || OutlookComHelpers.GetOptionalBool(item, "HasAttachments"),
            messageClass,
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            ConversationId: OutlookComHelpers.GetOptionalString(item, "ConversationID"),
            MeetingMessageType: isMeeting
                ? OutlookComHelpers.GetOptionalInt(item, "MeetingType")
                : null
        );

        var redacted = RedactMessage(dto);
        LogRedaction(redacted.BridgeId);
        return new NormalizedMessage(entryId, GetStoreId(item), redacted);
    }

    /// <summary>
    /// Logs one Information-level line per redacted item, recording the bridge id only — never
    /// subject, sender, body, attendee, location, or category data (master §2.4 busy-only
    /// logging).
    /// </summary>
    private void LogRedaction(string bridgeId) =>
        _logger.LogInformation(
            "Sensitivity redaction applied; item {BridgeId} retained as busy-only.",
            bridgeId
        );

    /// <summary>
    /// Never-ingest event construction for a Sensitivity 2/3 calendar item (issue #18, spec
    /// Group A): reads mechanical COM members only — no <c>Body</c>, <c>Organizer</c>, attendee
    /// enumeration, <c>Location</c>, or <c>Categories</c> read — preserves the derived mechanical
    /// fields, and applies <see cref="RedactEvent"/> for the redacted disposition.
    /// </summary>
    private EventDto BuildSensitiveEventDto(
        object item,
        string bridgeId,
        string? globalAppointmentId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int? sensitivity
    )
    {
        var recurrenceState = OutlookComHelpers.GetOptionalInt(item, "RecurrenceState");
        var responseStatus = OutlookComHelpers.GetOptionalInt(item, "ResponseStatus");
        var dto = new EventDto(
            bridgeId,
            globalAppointmentId,
            null,
            startUtc,
            endUtc,
            null,
            OutlookComHelpers.GetOptionalInt(item, "BusyStatus"),
            OutlookComHelpers.GetOptionalInt(item, "MeetingStatus"),
            OutlookComHelpers.GetOptionalBool(item, "IsRecurring"),
            sensitivity,
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            responseStatus,
            Categories: Array.Empty<string>(),
            IsOrganizer: responseStatus == 1,
            IsOnlineMeeting: OutlookComHelpers.GetOptionalBool(item, "IsOnlineMeeting"),
            AllowNewTimeProposals: OutlookComHelpers.GetOptionalBool(item, "AllowNewTimeProposal"),
            ICalUId: globalAppointmentId,
            SeriesMasterId: DeriveSeriesMasterId(recurrenceState, globalAppointmentId),
            LastModifiedDateTime: OutlookComHelpers.GetOptionalUtcDateTimeOffset(
                item,
                "LastModificationTime"
            ),
            BodyFull: null,
            SensitivityLabel: EventSensitivityLabel.FromSensitivity(sensitivity)
        );

        var redacted = RedactEvent(dto);
        LogRedaction(redacted.BridgeId);
        return redacted;
    }

    /// <summary>
    /// Message-kind probe shared by the sensitive and unredacted construction paths: an item is a
    /// meeting message when its runtime type name or its <c>MessageClass</c> contains "Meeting".
    /// </summary>
    private static bool IsMeetingItem(object item, string? messageClass)
    {
        if (item.GetType().Name.Contains("Meeting", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(messageClass)
            && messageClass.Contains("Meeting", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies the Group A event redaction disposition: placeholder subject; location, organizer,
    /// attendee/resource JSON, and body fields nulled; <c>Categories</c> emptied (never null,
    /// preserving the non-null invariant); <c>IsRedacted = true</c>;
    /// <c>ProtectedFieldsAvailable = false</c>. All scheduling-mechanical fields (times, busy
    /// status, recurrence flags, ids, <c>Sensitivity</c>, <c>SensitivityLabel</c>) are retained
    /// unchanged so the item still works as a busy block.
    /// </summary>
    internal static EventDto RedactEvent(EventDto evt) =>
        evt with
        {
            Subject = RedactedEventSubject,
            Location = null,
            Organizer = null,
            RequiredAttendeesJson = null,
            OptionalAttendeesJson = null,
            ResourcesJson = null,
            BodyPreview = null,
            BodyFull = null,
            Categories = Array.Empty<string>(),
            IsRedacted = true,
            ProtectedFieldsAvailable = false,
        };
}
