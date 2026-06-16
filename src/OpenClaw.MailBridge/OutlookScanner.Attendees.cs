using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.MailBridge;

/// <summary>
/// Recipient enumeration and attendee-JSON shaping for <see cref="OutlookScanner"/> (issue #71).
/// These live in a partial-class file separate from <c>OutlookScanner.cs</c> (already at the
/// repository 500-line cap) and <c>OutlookScanner.GraphFields.cs</c>. The pure shaping function
/// (<see cref="ShapeAttendeeJson"/>) has no COM dependency and is unit-testable directly; the COM
/// enumeration method (<c>ReadAttendees</c>) reads the <c>Recipients</c> collection and delegates
/// to it.
/// </summary>
internal sealed partial class OutlookScanner
{
    /// <summary>
    /// Immutable carrier for the three serialized attendee-JSON strings produced from a single pass
    /// over the COM <c>Recipients</c> collection. Each value is a non-null JSON array string; a type
    /// with no recipients is the empty array <c>"[]"</c> (never null). Null is reserved for safe-mode
    /// redaction and unread/unpopulated state (spec SP-B1).
    /// </summary>
    internal readonly record struct AttendeeJsonSet(
        string RequiredJson,
        string OptionalJson,
        string ResourcesJson
    );

    /// <summary>
    /// A single attendee projected from a COM <c>Recipient</c>: the display name and resolved SMTP
    /// email. Both values are non-null; a missing source value is the empty string so the serialized
    /// object always carries both <c>name</c> and <c>email</c> keys (spec US-AC5). Internal so the
    /// pure shaping function can be unit-tested directly without live COM.
    /// </summary>
    internal readonly record struct Attendee(string Name, string Email);

    /// <summary>
    /// Shared, immutable serializer options: lowercase <c>name</c>/<c>email</c> property names via the
    /// <see cref="AttendeeJson"/> contract, no indentation, and culture-invariant defaults. Constructed
    /// once (static) to avoid per-call allocation and to guarantee deterministic output (spec design
    /// decision 8).
    /// </summary>
    private static readonly JsonSerializerOptions AttendeeJsonOptions = new(
        JsonSerializerDefaults.Web
    )
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// The Graph <c>emailAddress</c>-shaped projection of an attendee: lowercase <c>name</c> and
    /// <c>email</c> keys in a fixed, deterministic order.
    /// </summary>
    private sealed record AttendeeJson(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email
    );

    /// <summary>
    /// Pure JSON shaping: serializes three ordered attendee lists to three JSON-array strings of
    /// <c>{"name","email"}</c> objects. Input order is preserved; an empty list serializes to
    /// <c>"[]"</c> (never null). Has no COM dependency. (spec US-AC2, SP-B1, design decisions 2/5/8)
    /// </summary>
    internal static AttendeeJsonSet ShapeAttendeeJson(
        IReadOnlyList<Attendee> required,
        IReadOnlyList<Attendee> optional,
        IReadOnlyList<Attendee> resources
    ) =>
        new(
            SerializeAttendees(required),
            SerializeAttendees(optional),
            SerializeAttendees(resources)
        );

    /// <summary>
    /// Serializes a single ordered attendee list to a JSON array string of <c>{"name","email"}</c>
    /// objects, preserving order. An empty list yields <c>"[]"</c>.
    /// </summary>
    private static string SerializeAttendees(IReadOnlyList<Attendee> attendees)
    {
        var shaped = new AttendeeJson[attendees.Count];
        for (var i = 0; i < attendees.Count; i++)
        {
            shaped[i] = new AttendeeJson(attendees[i].Name, attendees[i].Email);
        }

        return JsonSerializer.Serialize(shaped, AttendeeJsonOptions);
    }

    /// <summary>
    /// Reads the COM <c>Recipients</c> collection from the appointment <paramref name="item"/> in a
    /// single pass, classifies each recipient by its <c>Type</c> (<c>OlMeetingRecipientType</c>:
    /// 1 = required, 2 = optional, 3 = resource), and returns the three serialized JSON-array strings
    /// via <see cref="ShapeAttendeeJson"/>. Out-of-range <c>Type</c> values are ignored (spec SP-B2);
    /// per-recipient COM read failures fail soft and do not abort the scan (spec SP-B3). A null or
    /// empty <c>Recipients</c> collection yields <c>"[]"</c> for all three fields (spec SP-B1).
    ///
    /// All COM wrappers obtained while enumerating — the <c>Recipients</c> collection, each
    /// <c>Recipient</c>, and any <c>AddressEntry</c> resolved for the email fallback — are released
    /// deterministically in a <c>finally</c> via <c>_com.ReleaseAll</c>, following the
    /// <c>GetStoreId</c> idiom (spec SP-B4; architecture-boundaries COM confinement).
    ///
    /// This method does not read or alter the <c>ProtectedFieldsAvailable</c> signal; that value
    /// remains derived solely from body availability in <see cref="BuildEventDto"/> (spec SP-B5,
    /// design decision 7). Attendee PII protection is enforced separately by safe-mode redaction in
    /// <c>ResponseShaper.ShapeEvent</c>.
    /// </summary>
    /// <summary>
    /// Immutable carrier for the To/Cc attendee lists produced from a single pass over a message's
    /// COM <c>Recipients</c> collection (issue #73). Both lists are non-null; a message with no
    /// recipients of a given class yields an empty list (the construction site serializes an empty
    /// list to <c>"[]"</c>, never null).
    /// </summary>
    internal readonly record struct MessageRecipientSet(
        IReadOnlyList<Attendee> To,
        IReadOnlyList<Attendee> Cc
    );

    /// <summary>
    /// Reads the COM <c>Recipients</c> collection from a message <paramref name="item"/> in a single
    /// pass and classifies each recipient by its <c>Type</c> (<c>OlMailRecipientType</c>:
    /// 1 = To, 2 = Cc; 3 = Bcc is ignored per spec). Email values reuse the same fail-soft
    /// <c>Address</c> -> <c>AddressEntry.Address</c> chain as <see cref="ReadAttendees"/>; per-recipient
    /// COM read failures fail soft and do not abort the scan. A null or empty <c>Recipients</c>
    /// collection yields empty To/Cc lists (issue #73 AC-04).
    ///
    /// All COM wrappers obtained while enumerating are released deterministically in a
    /// <c>finally</c> via <paramref name="com"/> (<see cref="ComActiveObject.ReleaseAll"/>), following
    /// the <see cref="ReadAttendees"/> idiom (architecture-boundaries COM confinement). Static so the
    /// <see cref="ComMessageSource"/> adapter can call it without exposing additional scanner state.
    /// </summary>
    internal static MessageRecipientSet ReadMessageRecipients(object item, ComActiveObject com)
    {
        var to = new List<Attendee>();
        var cc = new List<Attendee>();

        object? recipients = OutlookComHelpers.GetOptionalMemberValue(item, "Recipients");
        if (recipients is null)
        {
            return new MessageRecipientSet(to, cc);
        }

        try
        {
            var count = OutlookComHelpers.GetOptionalInt(recipients, "Count") ?? 0;
            for (var index = 1; index <= count; index++)
            {
                object? recipient = null;
                object? addressEntry = null;
                try
                {
                    recipient = OutlookComHelpers.GetOptionalIndexedItem(recipients, index);
                    if (recipient is null)
                    {
                        continue;
                    }

                    var type = OutlookComHelpers.GetOptionalInt(recipient, "Type");
                    var target = type switch
                    {
                        1 => to,
                        2 => cc,
                        _ => null,
                    };
                    if (target is null)
                    {
                        // To = type 1, Cc = type 2; Bcc (3) and out-of-range values are ignored.
                        continue;
                    }

                    var name =
                        OutlookComHelpers.GetOptionalString(recipient, "Name") ?? string.Empty;
                    var email = OutlookComHelpers.GetOptionalString(recipient, "Address");
                    if (string.IsNullOrEmpty(email))
                    {
                        addressEntry = OutlookComHelpers.GetOptionalMemberValue(
                            recipient,
                            "AddressEntry"
                        );
                        if (addressEntry is not null)
                        {
                            email = OutlookComHelpers.GetOptionalString(addressEntry, "Address");
                        }
                    }

                    target.Add(new Attendee(name, email ?? string.Empty));
                }
                finally
                {
                    com.ReleaseAll(addressEntry, recipient);
                }
            }
        }
        finally
        {
            com.ReleaseAll(recipients);
        }

        return new MessageRecipientSet(to, cc);
    }

    private AttendeeJsonSet ReadAttendees(object item)
    {
        var required = new List<Attendee>();
        var optional = new List<Attendee>();
        var resources = new List<Attendee>();

        object? recipients = OutlookComHelpers.GetOptionalMemberValue(item, "Recipients");
        if (recipients is null)
        {
            return ShapeAttendeeJson(required, optional, resources);
        }

        try
        {
            var count = OutlookComHelpers.GetOptionalInt(recipients, "Count") ?? 0;
            for (var index = 1; index <= count; index++)
            {
                object? recipient = null;
                object? addressEntry = null;
                try
                {
                    recipient = OutlookComHelpers.GetOptionalIndexedItem(recipients, index);
                    if (recipient is null)
                    {
                        continue;
                    }

                    var type = OutlookComHelpers.GetOptionalInt(recipient, "Type");
                    var target = type switch
                    {
                        1 => required,
                        2 => optional,
                        3 => resources,
                        _ => null,
                    };
                    if (target is null)
                    {
                        // Type values outside {1,2,3} are ignored (spec SP-B2).
                        continue;
                    }

                    var name =
                        OutlookComHelpers.GetOptionalString(recipient, "Name") ?? string.Empty;
                    var email = OutlookComHelpers.GetOptionalString(recipient, "Address");
                    if (string.IsNullOrEmpty(email))
                    {
                        addressEntry = OutlookComHelpers.GetOptionalMemberValue(
                            recipient,
                            "AddressEntry"
                        );
                        if (addressEntry is not null)
                        {
                            email = OutlookComHelpers.GetOptionalString(addressEntry, "Address");
                        }
                    }

                    target.Add(new Attendee(name, email ?? string.Empty));
                }
                finally
                {
                    _com.ReleaseAll(addressEntry, recipient);
                }
            }
        }
        finally
        {
            _com.ReleaseAll(recipients);
        }

        return ShapeAttendeeJson(required, optional, resources);
    }
}
