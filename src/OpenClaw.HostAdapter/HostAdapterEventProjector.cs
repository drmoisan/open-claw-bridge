using System.Text.Json;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

/// <summary>
/// Null-tolerant payload projector for the message-to-event linkage route (issue #146). The
/// <c>get_event_for_message</c> RPC returns <c>Success(null)</c> for a genuinely unlinked message, so
/// the ok-path payload element can be a JSON null. The default
/// <see cref="HostAdapterProcessRunner.DeserializePayload{T}"/> throws a <see cref="JsonException"/>
/// on a null element (which the process runner would surface as a 502 TRANSPORT_FAILURE); this
/// projector instead maps a JSON null element to a <see langword="null"/> <see cref="EventDto"/> so
/// the route produces an <c>ok:true</c> / <c>data:null</c> / HTTP 200 envelope. A non-null element is
/// deserialized normally.
/// </summary>
internal static class HostAdapterEventProjector
{
    internal static EventDto? ProjectNullableEvent(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null
            ? null
            : HostAdapterProcessRunner.DeserializePayload<EventDto>(element);
}
