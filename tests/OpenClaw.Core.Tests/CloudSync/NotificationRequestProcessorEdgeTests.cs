using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Edge-case tests for <see cref="NotificationRequestProcessor"/>: malformed JSON
/// returns 400 <c>INVALID_REQUEST</c>, a full queue drops with Warning while still
/// returning 202, valid lifecycle notifications enqueue lifecycle work items, and a
/// payload variant with missing <c>resourceData</c> is dropped fail-visible with a
/// Warning rather than thrown.
/// </summary>
[TestClass]
public sealed class NotificationRequestProcessorEdgeTests
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

    private static string ChangeNotification(string messageId) =>
        $$"""
            {
              "value": [
                {
                  "subscriptionId": "sub-1",
                  "clientState": "{{StoredClientState}}",
                  "changeType": "created",
                  "resource": "Users/paula@contoso.com/Messages/{{messageId}}",
                  "resourceData": { "id": "{{messageId}}" }
                }
              ]
            }
            """;

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

    /// <summary>A change notification whose resourceData block is absent entirely.</summary>
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
    public async Task Malformed_json_body_returns_400_invalid_request()
    {
        // Arrange
        var queue = new RecordingNotificationQueue();
        var logger = new CapturingLogger<NotificationRequestProcessor>();
        var processor = new NotificationRequestProcessor(
            StoreWithSub1(),
            queue,
            logger,
            new NoOpCloudSyncActivityAuditor(),
            new FakeTimeProvider(Now)
        );

        // Act
        var result = await processor.ProcessNotificationsAsync(
            "{ not json !!!",
            CancellationToken.None
        );

        // Assert
        result.StatusCode.Should().Be(400, "an unparseable body is the caller's error");
        result.ContentType.Should().Be("application/json");
        result.Body.Should().Contain("INVALID_REQUEST", "the repo error shape carries the code");
        queue.Enqueued.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Full_queue_drops_with_warning_but_still_returns_202()
    {
        // Arrange: a real bounded queue of capacity 1 receives two valid items; the
        // second write is dropped by the channel with a Warning from the queue.
        var queueLogger = new CapturingLogger<ChannelNotificationQueue>();
        var queue = new ChannelNotificationQueue(
            Options.Create(new CloudSyncOptions { QueueCapacity = 1 }),
            queueLogger
        );
        var processor = new NotificationRequestProcessor(
            StoreWithSub1(),
            queue,
            new CapturingLogger<NotificationRequestProcessor>(),
            new NoOpCloudSyncActivityAuditor(),
            new FakeTimeProvider(Now)
        );
        await processor.ProcessNotificationsAsync(
            ChangeNotification("msg-fills-queue"),
            CancellationToken.None
        );

        // Act
        var result = await processor.ProcessNotificationsAsync(
            ChangeNotification("msg-dropped"),
            CancellationToken.None
        );

        // Assert
        result.StatusCode.Should().Be(202, "queue pressure never turns into a webhook error");
        queueLogger
            .Entries.Should()
            .ContainSingle("the dropped write logs exactly one Warning")
            .Which.Level.Should()
            .Be(LogLevel.Warning);
    }

    [TestMethod]
    [DataRow("reauthorizationRequired")]
    [DataRow("removed")]
    [DataRow("missed")]
    public async Task Valid_lifecycle_notification_enqueues_a_lifecycle_work_item(
        string lifecycleEvent
    )
    {
        // Arrange
        var queue = new RecordingNotificationQueue();
        var logger = new CapturingLogger<NotificationRequestProcessor>();
        var processor = new NotificationRequestProcessor(
            StoreWithSub1(),
            queue,
            logger,
            new NoOpCloudSyncActivityAuditor(),
            new FakeTimeProvider(Now)
        );

        // Act
        var result = await processor.ProcessNotificationsAsync(
            LifecycleNotification(lifecycleEvent),
            CancellationToken.None
        );

        // Assert
        result.StatusCode.Should().Be(202);
        queue
            .Enqueued.Should()
            .ContainSingle("one lifecycle work item per valid lifecycle notification")
            .Which.Should()
            .Be(new LifecycleWorkItem("sub-1", lifecycleEvent));
        logger.Entries.Should().BeEmpty("a valid lifecycle item produces no Warning");
    }

    [TestMethod]
    public async Task Missing_resourceData_is_dropped_with_warning_not_thrown()
    {
        // Arrange
        var queue = new RecordingNotificationQueue();
        var logger = new CapturingLogger<NotificationRequestProcessor>();
        var processor = new NotificationRequestProcessor(
            StoreWithSub1(),
            queue,
            logger,
            new NoOpCloudSyncActivityAuditor(),
            new FakeTimeProvider(Now)
        );

        // Act
        var result = await processor.ProcessNotificationsAsync(
            MissingResourceDataNotification,
            CancellationToken.None
        );

        // Assert
        result
            .StatusCode.Should()
            .Be(202, "an unmodeled payload variant is fail-visible, not fatal");
        queue.Enqueued.Should().BeEmpty("no work item can be built without resourceData.id");
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("resourceData")
            );
    }
}
