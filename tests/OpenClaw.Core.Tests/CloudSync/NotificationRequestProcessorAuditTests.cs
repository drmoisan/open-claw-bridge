using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Audit-emission tests for <see cref="NotificationRequestProcessor"/> (issue #124, AC2/AC4):
/// a valid lifecycle or change notification emits exactly one <c>WebhookReceived</c> audit
/// record with a freshly generated correlation id, and each of the four rejection branches
/// (no <c>subscriptionId</c>, unknown subscription, <c>clientState</c> mismatch, missing
/// <c>resourceData.id</c>) emits exactly one <c>WebhookRejected</c> audit record with the
/// matching <see cref="CloudSyncActivityResultCode"/>.
/// </summary>
[TestClass]
public sealed class NotificationRequestProcessorAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 7, 1, 0, 0, TimeSpan.Zero);

    private const string StoredClientState = "client-state-secret-1";

    private static FakeSubscriptionStore StoreWithSub1()
    {
        var store = new FakeSubscriptionStore();
        store.Records["sub-1"] = new GraphSubscriptionRecord(
            "sub-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            StoredClientState,
            new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero),
            SubscriptionStatus.Active
        );
        return store;
    }

    private static NotificationRequestProcessor NewProcessor(
        FakeSubscriptionStore store,
        RecordingNotificationQueue queue,
        FakeActionAuditLog auditLog
    ) =>
        new(
            store,
            queue,
            new CapturingLogger<NotificationRequestProcessor>(),
            auditLog,
            new FakeTimeProvider(Now)
        );

    private static string LifecycleNotification(string lifecycleEvent) =>
        $$"""
            {
              "value": [
                {
                  "subscriptionId": "sub-1",
                  "clientState": "{{StoredClientState}}",
                  "lifecycleEvent": "{{lifecycleEvent}}"
                }
              ]
            }
            """;

    private static string ChangeNotification(string subscriptionId, string clientState) =>
        $$"""
            {
              "value": [
                {
                  "subscriptionId": "{{subscriptionId}}",
                  "clientState": "{{clientState}}",
                  "changeType": "created",
                  "resource": "Users/paula@contoso.com/Messages/AAMkAGUw",
                  "resourceData": { "id": "AAMkAGUw" }
                }
              ]
            }
            """;

    private const string NoSubscriptionIdNotification = """
        {
          "value": [
            {
              "clientState": "client-state-secret-1",
              "changeType": "created",
              "resource": "Users/paula@contoso.com/Messages/AAMkAGUw",
              "resourceData": { "id": "AAMkAGUw" }
            }
          ]
        }
        """;

    private const string MissingResourceDataNotification = """
        {
          "value": [
            {
              "subscriptionId": "sub-1",
              "clientState": "client-state-secret-1",
              "changeType": "created",
              "resource": "Users/paula@contoso.com/Messages/AAMkAGUw"
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Valid_lifecycle_notification_emits_exactly_one_webhook_received_record()
    {
        // Arrange
        var auditLog = new FakeActionAuditLog();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditLog);

        // Act
        await processor.ProcessNotificationsAsync(
            LifecycleNotification("removed"),
            CancellationToken.None
        );

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.WebhookReceived);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
        record.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task Valid_change_notification_emits_exactly_one_webhook_received_record_with_resourceData_id()
    {
        // Arrange
        var auditLog = new FakeActionAuditLog();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditLog);

        // Act
        await processor.ProcessNotificationsAsync(
            ChangeNotification("sub-1", StoredClientState),
            CancellationToken.None
        );

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.WebhookReceived);
        record.MessageId.Should().Be("AAMkAGUw");
    }

    [TestMethod]
    public async Task No_subscriptionId_emits_exactly_one_webhook_rejected_record_with_unknown_subscription()
    {
        // Arrange
        var auditLog = new FakeActionAuditLog();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditLog);

        // Act
        await processor.ProcessNotificationsAsync(
            NoSubscriptionIdNotification,
            CancellationToken.None
        );

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.WebhookRejected);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.UnknownSubscription);
    }

    [TestMethod]
    public async Task Unknown_subscription_emits_exactly_one_webhook_rejected_record_with_unknown_subscription()
    {
        // Arrange
        var auditLog = new FakeActionAuditLog();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditLog);

        // Act
        await processor.ProcessNotificationsAsync(
            ChangeNotification("sub-unknown", StoredClientState),
            CancellationToken.None
        );

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.WebhookRejected);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.UnknownSubscription);
        record.MessageId.Should().Be("sub-unknown");
    }

    [TestMethod]
    public async Task ClientState_mismatch_emits_exactly_one_webhook_rejected_record_with_client_state_mismatch()
    {
        // Arrange
        var auditLog = new FakeActionAuditLog();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditLog);

        // Act
        await processor.ProcessNotificationsAsync(
            ChangeNotification("sub-1", "wrong-secret"),
            CancellationToken.None
        );

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.WebhookRejected);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.ClientStateMismatch);
    }

    [TestMethod]
    public async Task Missing_resourceData_id_emits_exactly_one_webhook_rejected_record_with_missing_resource_id()
    {
        // Arrange
        var auditLog = new FakeActionAuditLog();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditLog);

        // Act
        await processor.ProcessNotificationsAsync(
            MissingResourceDataNotification,
            CancellationToken.None
        );

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.WebhookRejected);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.MissingResourceId);
    }
}
