using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Unit tests for <see cref="NotificationRequestProcessor"/> (AC-1): the validation
/// handshake echoes the URL-decoded token HTML-encoded as <c>text/plain</c> 200, the
/// clientState matrix drops unknown/mismatched items with a Warning while returning
/// 202 (D-1), and each valid change notification enqueues exactly one
/// <c>{Mailbox, MessageId, ChangeType}</c> work item. Notification payloads are
/// recorded in-code constants.
/// </summary>
[TestClass]
public sealed class NotificationRequestProcessorTests
{
    private const string StoredClientState = "client-state-secret-1";

    /// <summary>Recorded change-notification batch with a valid item (Graph webhook shape).</summary>
    private const string ValidChangeNotification = """
        {
          "value": [
            {
              "subscriptionId": "sub-1",
              "subscriptionExpirationDateTime": "2026-07-10T08:00:00+00:00",
              "clientState": "client-state-secret-1",
              "changeType": "created",
              "resource": "Users/paula@contoso.com/Messages/AAMkAGUw",
              "tenantId": "00000000-0000-0000-0000-000000000001",
              "resourceData": {
                "@odata.type": "#Microsoft.Graph.Message",
                "@odata.id": "Users/paula@contoso.com/Messages/AAMkAGUw",
                "@odata.etag": "W/\"CQAAABYAAAA=\"",
                "id": "AAMkAGUw"
              }
            }
          ]
        }
        """;

    private const string UnknownSubscriptionNotification = """
        {
          "value": [
            {
              "subscriptionId": "sub-unknown",
              "clientState": "client-state-secret-1",
              "changeType": "created",
              "resource": "Users/paula@contoso.com/Messages/AAMkAGUw",
              "resourceData": { "id": "AAMkAGUw" }
            }
          ]
        }
        """;

    private const string MismatchedClientStateNotification = """
        {
          "value": [
            {
              "subscriptionId": "sub-1",
              "clientState": "wrong-secret",
              "changeType": "created",
              "resource": "Users/paula@contoso.com/Messages/AAMkAGUw",
              "resourceData": { "id": "AAMkAGUw" }
            }
          ]
        }
        """;

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

    [TestMethod]
    public void Handshake_echoes_the_url_decoded_token_html_encoded_as_text_plain_200()
    {
        // Arrange: a token carrying both percent-escapes and HTML-active characters.
        const string Token = "abc%20token<script>alert(1)</script>";

        // Act
        var result = NotificationRequestProcessor.HandleHandshake(Token);

        // Assert
        result.StatusCode.Should().Be(200, "the handshake responds 200 within 10 seconds");
        result.ContentType.Should().Be("text/plain");
        result
            .Body.Should()
            .Be(
                "abc token&lt;script&gt;alert(1)&lt;/script&gt;",
                "%20 is URL-decoded and HTML-active characters are encoded"
            );
    }

    [TestMethod]
    public async Task Unknown_subscriptionId_is_dropped_with_warning_and_202()
    {
        // Arrange
        var queue = new RecordingNotificationQueue();
        var logger = new CapturingLogger<NotificationRequestProcessor>();
        var processor = new NotificationRequestProcessor(StoreWithSub1(), queue, logger);

        // Act
        var result = await processor.ProcessNotificationsAsync(
            UnknownSubscriptionNotification,
            CancellationToken.None
        );

        // Assert
        result.StatusCode.Should().Be(202, "invalid items still acknowledge with 202 (D-1)");
        queue.Enqueued.Should().BeEmpty("the unknown-subscription item is dropped");
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("sub-unknown")
            );
    }

    [TestMethod]
    public async Task Mismatched_clientState_is_dropped_with_warning_and_202()
    {
        // Arrange
        var queue = new RecordingNotificationQueue();
        var logger = new CapturingLogger<NotificationRequestProcessor>();
        var processor = new NotificationRequestProcessor(StoreWithSub1(), queue, logger);

        // Act
        var result = await processor.ProcessNotificationsAsync(
            MismatchedClientStateNotification,
            CancellationToken.None
        );

        // Assert
        result.StatusCode.Should().Be(202, "a clientState mismatch must not become a 401 oracle");
        queue.Enqueued.Should().BeEmpty("the mismatched item is dropped");
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(e =>
                e.Level == LogLevel.Warning
                && e.Message.Contains("sub-1")
                && e.Message.Contains("clientState")
            );
    }

    [TestMethod]
    public async Task Valid_notification_enqueues_exactly_one_work_item_and_returns_202()
    {
        // Arrange
        var queue = new RecordingNotificationQueue();
        var logger = new CapturingLogger<NotificationRequestProcessor>();
        var processor = new NotificationRequestProcessor(StoreWithSub1(), queue, logger);

        // Act
        var result = await processor.ProcessNotificationsAsync(
            ValidChangeNotification,
            CancellationToken.None
        );

        // Assert
        result.StatusCode.Should().Be(202);
        queue
            .Enqueued.Should()
            .ContainSingle("exactly one work item per valid change notification")
            .Which.Should()
            .Be(
                new NotificationWorkItem("paula@contoso.com", "AAMkAGUw", "created"),
                "the work item carries the stored mailbox, resourceData.id, and changeType"
            );
        logger.Entries.Should().BeEmpty("a valid item produces no Warning");
    }
}
