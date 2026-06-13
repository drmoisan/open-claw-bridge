using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Graph-field derivation helpers and event construction for <see cref="OutlookScanner"/>
/// (issue #72). These live in a partial-class file separate from <c>OutlookScanner.cs</c> so the
/// latter (already at the repository 500-line cap) does not grow further. The derivation helpers
/// are pure transforms over already-read COM values; <see cref="BuildEventDto"/> performs the COM
/// reads for the nine new Graph-shaped fields, reading <c>Body</c> exactly once.
/// </summary>
internal sealed partial class OutlookScanner
{
    /// <summary>
    /// Builds the <see cref="EventDto"/> for a calendar item, reading the COM <c>Body</c> once and
    /// reusing it for both the redacted preview and the raw <c>BodyFull</c> value (spec "Limits"),
    /// and populating the nine issue-#72 Graph-shaped fields from their COM analogs/derivations.
    /// </summary>
    private EventDto BuildEventDto(
        object item,
        string bridgeId,
        string? globalAppointmentId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc
    )
    {
        var body = OutlookComHelpers.GetOptionalString(item, "Body");
        var sensitivity = OutlookComHelpers.GetOptionalInt(item, "Sensitivity");
        var recurrenceState = OutlookComHelpers.GetOptionalInt(item, "RecurrenceState");
        var responseStatus = OutlookComHelpers.GetOptionalInt(item, "ResponseStatus");

        return new EventDto(
            bridgeId,
            globalAppointmentId,
            OutlookComHelpers.GetOptionalString(item, "Subject"),
            startUtc,
            endUtc,
            OutlookComHelpers.GetOptionalString(item, "Location"),
            OutlookComHelpers.GetOptionalInt(item, "BusyStatus"),
            OutlookComHelpers.GetOptionalInt(item, "MeetingStatus"),
            OutlookComHelpers.GetOptionalBool(item, "IsRecurring"),
            sensitivity,
            OutlookComHelpers.GetOptionalString(item, "Organizer"),
            null,
            null,
            null,
            ResponseShaper.ShapePreview(body, _settings),
            !string.IsNullOrWhiteSpace(body),
            false,
            responseStatus,
            Categories: SplitCategories(OutlookComHelpers.GetOptionalString(item, "Categories")),
            IsOrganizer: responseStatus == 1,
            IsOnlineMeeting: OutlookComHelpers.GetOptionalBool(item, "IsOnlineMeeting"),
            AllowNewTimeProposals: OutlookComHelpers.GetOptionalBool(item, "AllowNewTimeProposal"),
            ICalUId: globalAppointmentId,
            SeriesMasterId: DeriveSeriesMasterId(recurrenceState, globalAppointmentId),
            LastModifiedDateTime: OutlookComHelpers.GetOptionalUtcDateTimeOffset(
                item,
                "LastModificationTime"
            ),
            BodyFull: body,
            SensitivityLabel: EventSensitivityLabel.FromSensitivity(sensitivity)
        );
    }

    /// <summary>
    /// Splits the Outlook <c>Categories</c> string (comma-space delimited) into a category array.
    /// Each token is trimmed and empty tokens are dropped. Returns an empty array (never null) for
    /// null or whitespace input, satisfying the <c>categories</c> non-null invariant.
    /// </summary>
    private static string[] SplitCategories(string? categories)
    {
        if (string.IsNullOrWhiteSpace(categories))
        {
            return Array.Empty<string>();
        }

        return categories
            .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// Derives <c>seriesMasterId</c> from the Outlook <c>RecurrenceState</c>
    /// (<c>OlRecurrenceState</c>) value (OQ-1): olApptNotRecurring=0 and olApptMaster=1 yield
    /// null; olApptOccurrence=2 and olApptException=3 yield the supplied
    /// <paramref name="globalAppointmentId"/>. Any other or null state yields null. The logic
    /// returns null for the master and non-recurring cases and the global appointment id for the
    /// occurrence and exception cases, so it is insensitive to alternate non-master integer
    /// assignments as long as 0/1 mean non-recurring/master.
    /// </summary>
    private static string? DeriveSeriesMasterId(
        int? recurrenceState,
        string? globalAppointmentId
    ) =>
        recurrenceState switch
        {
            // 0 = olApptNotRecurring, 1 = olApptMaster -> no series-master pointer.
            null or 0 or 1 => null,
            // 2 = olApptOccurrence, 3 = olApptException -> point at the master via GlobalAppointmentID.
            _ => globalAppointmentId,
        };
}
