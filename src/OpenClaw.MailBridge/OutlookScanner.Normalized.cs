using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Normalized-record partial for <see cref="OutlookScanner"/>. Split from the main file to keep
/// each source file within the project's 500-line guideline. These records are private
/// implementation details used by the scanner to carry Outlook identity fields (EntryID,
/// StoreID, GlobalAppointmentID) alongside the normalized DTO before it is persisted.
/// </summary>
internal sealed partial class OutlookScanner
{
    private sealed record NormalizedMessage(string EntryId, string? StoreId, MessageDto Dto);

    private sealed record NormalizedEvent(
        string EntryId,
        string? StoreId,
        string? GlobalAppointmentId,
        EventDto Dto
    );
}
