using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Message-to-event linkage population for <see cref="OutlookScanner"/> normalization (issue #146).
/// Split into a partial-class file so the linkage read is added without growing the main
/// <c>OutlookScanner.cs</c> file. The linkage key is read through the model-agnostic
/// <see cref="IMessageSource"/> seam (fail-soft in <see cref="ComMessageSource"/>): a meeting item
/// carries its associated appointment's <c>GlobalAppointmentID</c>, while ordinary mail yields
/// <see langword="null"/>.
/// </summary>
internal sealed partial class OutlookScanner
{
    /// <summary>
    /// Produces the normalized-message result for the non-sensitive path with the linkage key
    /// applied. The linkage value comes from <see cref="IMessageSource.LinkedGlobalAppointmentId"/>,
    /// which is <see langword="null"/> for ordinary mail and the appointment
    /// <c>GlobalAppointmentID</c> for a meeting item.
    /// </summary>
    private static NormalizedMessage BuildLinkedNormalizedMessage(
        string entryId,
        string? storeId,
        MessageDto dto,
        IMessageSource source
    ) =>
        new(
            entryId,
            storeId,
            dto with
            {
                LinkedGlobalAppointmentId = source.LinkedGlobalAppointmentId,
            }
        );
}
