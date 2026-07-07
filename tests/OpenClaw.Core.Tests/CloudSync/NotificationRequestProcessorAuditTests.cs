using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core;
using OpenClaw.Core.Agent;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Audit-emission tests for <see cref="NotificationRequestProcessor"/> (issue #124, AC2/AC4;
/// revised in the Phase 9 architecture-boundary seam): a valid lifecycle or change
/// notification calls <see cref="ICloudSyncActivityAuditor.RecordWebhookReceivedAsync"/>
/// exactly once with a freshly generated correlation id, and each of the four rejection
/// branches (no <c>subscriptionId</c>, unknown subscription, <c>clientState</c> mismatch,
/// missing <c>resourceData.id</c>) calls
/// <see cref="ICloudSyncActivityAuditor.RecordWebhookRejectedAsync"/> exactly once with the
/// matching <see cref="CloudSyncActivityResultCode"/> value.
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
        Mock<ICloudSyncActivityAuditor> auditor
    ) =>
        new(
            store,
            queue,
            new CapturingLogger<NotificationRequestProcessor>(),
            auditor.Object,
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
        var auditor = new Mock<ICloudSyncActivityAuditor>();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditor);

        // Act
        await processor.ProcessNotificationsAsync(
            LifecycleNotification("removed"),
            CancellationToken.None
        );

        // Assert
        auditor.Verify(
            a =>
                a.RecordWebhookReceivedAsync(
                    "paula@contoso.com",
                    "sub-1",
                    It.Is<string>(c => !string.IsNullOrWhiteSpace(c)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }

    [TestMethod]
    public async Task Valid_change_notification_emits_exactly_one_webhook_received_record_with_resourceData_id()
    {
        // Arrange
        var auditor = new Mock<ICloudSyncActivityAuditor>();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditor);

        // Act
        await processor.ProcessNotificationsAsync(
            ChangeNotification("sub-1", StoredClientState),
            CancellationToken.None
        );

        // Assert
        auditor.Verify(
            a =>
                a.RecordWebhookReceivedAsync(
                    "paula@contoso.com",
                    "AAMkAGUw",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }

    [TestMethod]
    public async Task No_subscriptionId_emits_exactly_one_webhook_rejected_record_with_unknown_subscription()
    {
        // Arrange
        var auditor = new Mock<ICloudSyncActivityAuditor>();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditor);

        // Act
        await processor.ProcessNotificationsAsync(
            NoSubscriptionIdNotification,
            CancellationToken.None
        );

        // Assert
        auditor.Verify(
            a =>
                a.RecordWebhookRejectedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    CloudSyncActivityResultCode.UnknownSubscription,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }

    [TestMethod]
    public async Task Unknown_subscription_emits_exactly_one_webhook_rejected_record_with_unknown_subscription()
    {
        // Arrange
        var auditor = new Mock<ICloudSyncActivityAuditor>();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditor);

        // Act
        await processor.ProcessNotificationsAsync(
            ChangeNotification("sub-unknown", StoredClientState),
            CancellationToken.None
        );

        // Assert
        auditor.Verify(
            a =>
                a.RecordWebhookRejectedAsync(
                    "sub-unknown",
                    "sub-unknown",
                    CloudSyncActivityResultCode.UnknownSubscription,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }

    [TestMethod]
    public async Task ClientState_mismatch_emits_exactly_one_webhook_rejected_record_with_client_state_mismatch()
    {
        // Arrange
        var auditor = new Mock<ICloudSyncActivityAuditor>();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditor);

        // Act
        await processor.ProcessNotificationsAsync(
            ChangeNotification("sub-1", "wrong-secret"),
            CancellationToken.None
        );

        // Assert
        auditor.Verify(
            a =>
                a.RecordWebhookRejectedAsync(
                    It.IsAny<string>(),
                    "sub-1",
                    CloudSyncActivityResultCode.ClientStateMismatch,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }

    [TestMethod]
    public async Task Missing_resourceData_id_emits_exactly_one_webhook_rejected_record_with_missing_resource_id()
    {
        // Arrange
        var auditor = new Mock<ICloudSyncActivityAuditor>();
        var processor = NewProcessor(StoreWithSub1(), new RecordingNotificationQueue(), auditor);

        // Act
        await processor.ProcessNotificationsAsync(
            MissingResourceDataNotification,
            CancellationToken.None
        );

        // Assert
        auditor.Verify(
            a =>
                a.RecordWebhookRejectedAsync(
                    It.IsAny<string>(),
                    "sub-1",
                    CloudSyncActivityResultCode.MissingResourceId,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }
}
