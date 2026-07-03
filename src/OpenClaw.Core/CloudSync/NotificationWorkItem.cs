namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Base shape for items on <see cref="INotificationQueue"/> (D-4): one queue serves
/// both change notifications (<see cref="NotificationWorkItem"/>) and lifecycle
/// notifications (<see cref="LifecycleWorkItem"/>); consumers route by pattern
/// matching on the concrete record type.
/// </summary>
internal abstract record CloudSyncWorkItem;

/// <summary>
/// A validated Graph change notification awaiting dispatch (master §8.2 shape:
/// <c>{userId, messageId, changeType}</c>). Immutable.
/// </summary>
/// <param name="Mailbox">The subscription's mailbox UPN.</param>
/// <param name="MessageId">The Graph message id from <c>resourceData.id</c>.</param>
/// <param name="ChangeType">The Graph change type (<c>created</c>/<c>updated</c>).</param>
internal sealed record NotificationWorkItem(string Mailbox, string MessageId, string ChangeType)
    : CloudSyncWorkItem;

/// <summary>
/// A validated Graph lifecycle notification awaiting routing to the subscription
/// manager. Immutable.
/// </summary>
/// <param name="SubscriptionId">The Graph subscription id the event applies to.</param>
/// <param name="LifecycleEvent">
/// The lifecycle event value; see <see cref="LifecycleEvents"/> for the routed set.
/// </param>
internal sealed record LifecycleWorkItem(string SubscriptionId, string LifecycleEvent)
    : CloudSyncWorkItem;

/// <summary>The Graph lifecycle-event values routed by the subscription manager.</summary>
internal static class LifecycleEvents
{
    /// <summary>Renew the subscription now; auth failure marks it <c>reauthorize_failed</c>.</summary>
    internal const string ReauthorizationRequired = "reauthorizationRequired";

    /// <summary>Delete the local record and recreate the subscription.</summary>
    internal const string Removed = "removed";

    /// <summary>Trigger a delta reconciliation for the subscription's mailbox.</summary>
    internal const string Missed = "missed";
}
