namespace OpenClaw.MailBridge;

/// <summary>
/// Model-agnostic projection of the message-source data that <see cref="OutlookScanner"/>
/// normalization needs to populate the issue-#73 <see cref="Contracts.Models.MessageDto"/> fields
/// (resolved sender SMTP, resolved from/on-behalf-of SMTP, conversation id, meeting type, and the
/// To/Cc recipient lists). The interface is purpose-specific and minimal; no concrete COM type
/// (<c>Microsoft.Office.Interop</c> or <c>Marshal</c>) appears on its surface (locked decision D-D).
///
/// A future Modern/Microsoft Graph message model is enabled by adding a second adapter that
/// implements this interface; core normalization, the scheduling DTO mapper, and both cache
/// repositories depend on this abstraction rather than on a concrete COM <c>MailItem</c>/
/// <c>MeetingItem</c>. The single COM implementation, <see cref="ComMessageSource"/>, keeps Outlook
/// COM confined to <c>OpenClaw.MailBridge</c> (architecture-boundaries rule 1).
/// </summary>
internal interface IMessageSource
{
    /// <summary>
    /// Fail-soft SMTP resolution of the sender address (locked decision D-C). Returns a resolved
    /// SMTP address when available, a graceful fallback (legacy Exchange DN / raw value) otherwise,
    /// or <see langword="null"/> when no sender address can be read. Never throws.
    /// </summary>
    string? SenderEmailResolved { get; }

    /// <summary>
    /// Fail-soft SMTP resolution of the on-behalf-of/delegate identity (locked decision D-A). When
    /// the message is delegate-sent this reflects the on-behalf-of identity; otherwise it falls back
    /// to <see cref="SenderEmailResolved"/>. Never throws.
    /// </summary>
    string? FromEmailAddress { get; }

    /// <summary>
    /// The source conversation identifier, passed through unmodified, or <see langword="null"/> when
    /// unavailable.
    /// </summary>
    string? ConversationId { get; }

    /// <summary>
    /// The raw <c>OlMeetingType</c> integer for a meeting item (0=request, 1=cancellation,
    /// 2=declined, 3=accepted, 4=tentative), or <see langword="null"/> for ordinary mail (locked
    /// decision D-B). Graph-vocabulary string mapping happens downstream in the scheduling DTO mapper.
    /// </summary>
    int? MeetingMessageType { get; }

    /// <summary>
    /// The associated appointment's Clean Global Object ID (<c>GlobalAppointmentID</c>) for a meeting
    /// item, read fail-soft (issue #146). This is the linkage key joined against
    /// <c>events.global_appointment_id</c> to resolve the calendar event a message is linked to.
    /// <see langword="null"/> for ordinary (non-meeting) mail or when the appointment cannot be read.
    /// No concrete COM type appears on this member's surface (architecture-boundaries rule 1).
    /// </summary>
    string? LinkedGlobalAppointmentId { get; }

    /// <summary>
    /// The To recipients (recipient type 1), each as a name/email pair using the existing attendee
    /// shape. Never null; an empty collection models a message with no To recipients.
    /// </summary>
    IReadOnlyList<OutlookScanner.Attendee> ToRecipients { get; }

    /// <summary>
    /// The Cc recipients (recipient type 2), each as a name/email pair using the existing attendee
    /// shape. Never null; an empty collection models a message with no Cc recipients.
    /// </summary>
    IReadOnlyList<OutlookScanner.Attendee> CcRecipients { get; }
}
