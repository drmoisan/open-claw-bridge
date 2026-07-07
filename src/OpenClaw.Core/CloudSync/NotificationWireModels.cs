using System.Text.Json.Serialization;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Internal System.Text.Json wire records for Graph change- and lifecycle-notification
/// POSTs (master §8.2). Deserialized with
/// <see cref="OpenClaw.Core.CloudGraph.GraphRequestExecutor.JsonOptions"/> (web
/// defaults, case-insensitive); unmodeled fields are ignored, not fatal. One record
/// covers both notification kinds — a non-null <see cref="GraphNotification.LifecycleEvent"/>
/// marks a lifecycle notification.
/// </summary>
internal sealed record GraphNotificationCollection(
    [property: JsonPropertyName("value")] IReadOnlyList<GraphNotification>? Value
);

/// <summary>The notification's <c>resourceData</c>; only <c>id</c> is consumed.</summary>
internal sealed record GraphNotificationResourceData(string? Id);

/// <summary>
/// A single Graph notification item: change notifications carry
/// <see cref="ChangeType"/>/<see cref="Resource"/>/<see cref="ResourceData"/>;
/// lifecycle notifications carry <see cref="LifecycleEvent"/>. Both carry the
/// <see cref="SubscriptionId"/> and <see cref="ClientState"/> used for validation.
/// </summary>
internal sealed record GraphNotification(
    string? SubscriptionId,
    string? ClientState,
    string? ChangeType,
    string? Resource,
    GraphNotificationResourceData? ResourceData,
    string? LifecycleEvent
);
