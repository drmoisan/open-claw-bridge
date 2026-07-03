using System.Net;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudSync;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Unit tests for <see cref="NotificationDispatchWorker"/> (D-2/D-3): a successful
/// fetch upserts the fetched message with the ready/graph status and the fetch
/// envelope's request id; a failed fetch (error envelope) logs Warning, performs no
/// upsert, and processing continues with the next item; a lifecycle work item invokes
/// the manager's lifecycle routing and no <c>GetMessageAsync</c> call.
/// <see cref="IHostAdapterClient"/> is mocked with Moq.
/// </summary>
[TestClass]
public sealed class NotificationDispatchWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 8, 0, 0, TimeSpan.Zero);

    private static MessageDto Message(string bridgeId) =>
        new(
            BridgeId: bridgeId,
            ItemKind: "mail",
            Subject: $"Fetched {bridgeId}",
            ReceivedUtc: Now.AddMinutes(-5),
            SentUtc: Now.AddMinutes(-6),
            Importance: 1,
            Sensitivity: 0,
            Unread: true,
            HasAttachments: false,
            MessageClass: null,
            SenderName: "Counterparty",
            SenderEmail: "counterparty@fabrikam.com",
            ToJson: null,
            CcJson: null,
            BodyPreview: "preview",
            ProtectedFieldsAvailable: true,
            IsRedacted: false,
            SenderEmailResolved: "counterparty@fabrikam.com",
            FromEmailAddress: "counterparty@fabrikam.com",
            ConversationId: null,
            MeetingMessageType: null
        );

    private static ApiEnvelope<MessageDto> SuccessEnvelope(string bridgeId, string requestId) =>
        new(true, Message(bridgeId), new ApiMeta(requestId, "cloudgraph", null), null);

    private static ApiEnvelope<MessageDto> ErrorEnvelope() =>
        new(
            false,
            null,
            new ApiMeta("req-fail", "cloudgraph", null),
            new ApiError("NOT_FOUND", "The message was not found.")
        );

    private static async Task<(
        NotificationDispatchWorker Worker,
        OpenClaw.Core.CoreCacheRepository Repository,
        string ConnectionString,
        RecordingNotificationQueue Queue,
        CapturingLogger<NotificationDispatchWorker> Logger,
        RecordingReconcileTrigger Trigger,
        List<HttpRequestMessage> GraphRequests
    )> BuildAsync(Mock<IHostAdapterClient> hostAdapterClient, string label)
    {
        var connectionString = GraphDeltaReconcilerTests.NewConnectionString($"dispatch-{label}");
        var repository = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await repository.InitializeAsync();
        var queue = new RecordingNotificationQueue();
        var logger = new CapturingLogger<NotificationDispatchWorker>();
        var trigger = new RecordingReconcileTrigger();
        var graphRequests = new List<HttpRequestMessage>();
        var handler = new FakeHttpHandler(request =>
        {
            graphRequests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var store = new FakeSubscriptionStore();
        store.Records["sub-1"] = new GraphSubscriptionRecord(
            "sub-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            "client-state",
            Now.AddDays(2),
            SubscriptionStatus.Active
        );
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            trigger
        );
        var worker = new NotificationDispatchWorker(
            queue,
            hostAdapterClient.Object,
            manager,
            repository,
            new FakeTimeProvider(Now),
            logger
        );
        return (worker, repository, connectionString, queue, logger, trigger, graphRequests);
    }

    [TestMethod]
    public async Task Successful_fetch_upserts_with_ready_graph_status_and_the_envelope_request_id()
    {
        // Arrange
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        client
            .Setup(c => c.GetMessageAsync("msg-ok", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessEnvelope("msg-ok", "req-fetch-1"));
        var (worker, repository, connectionString, _, logger, _, _) = await BuildAsync(
            client,
            "success"
        );
        using var _repo = repository;

        // Act
        await worker.ProcessItemAsync(
            new NotificationWorkItem("paula@contoso.com", "msg-ok", "created"),
            CancellationToken.None
        );

        // Assert
        (await repository.GetMessageAsync("msg-ok"))!
            .Subject.Should()
            .Be("Fetched msg-ok");
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT bridge_mode, cache_stale, adapter_request_id FROM messages WHERE bridge_id = 'msg-ok';";
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("graph", "the D-3 synthesized status mode is graph");
        reader.GetInt32(1).Should().Be(0, "the D-3 synthesized status is not stale");
        reader
            .GetString(2)
            .Should()
            .Be("req-fetch-1", "the upsert carries the fetch envelope's request id");
        logger.Entries.Should().BeEmpty("a successful dispatch produces no Warning");
    }

    [TestMethod]
    public async Task Failed_fetch_logs_warning_drops_the_item_and_the_loop_continues()
    {
        // Arrange: the first item's fetch fails; the second succeeds.
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        client
            .Setup(c => c.GetMessageAsync("msg-bad", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorEnvelope());
        client
            .Setup(c => c.GetMessageAsync("msg-good", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessEnvelope("msg-good", "req-fetch-2"));
        var (worker, repository, _, queue, logger, _, _) = await BuildAsync(client, "failure");
        using var _repo = repository;
        queue.TryEnqueue(new NotificationWorkItem("paula@contoso.com", "msg-bad", "created"));
        queue.TryEnqueue(new NotificationWorkItem("paula@contoso.com", "msg-good", "created"));

        // Act: run the worker loop until the second item lands, then stop it.
        await worker.StartAsync(CancellationToken.None);
        var safety = 0;
        while (await repository.GetMessageAsync("msg-good") is null)
        {
            if (++safety > 100_000)
            {
                throw new AssertFailedException("The worker did not process the second item.");
            }

            await Task.Yield();
        }

        await worker.StopAsync(CancellationToken.None);

        // Assert
        (await repository.GetMessageAsync("msg-bad"))
            .Should()
            .BeNull("a failed fetch performs no upsert — no fabricated healthy status");
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("msg-bad")
            );
    }

    [TestMethod]
    public async Task Lifecycle_item_routes_to_the_manager_and_makes_no_message_fetch()
    {
        // Arrange: a strict client with no setups — any GetMessageAsync call throws.
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        var (worker, repository, _, _, _, trigger, graphRequests) = await BuildAsync(
            client,
            "lifecycle"
        );
        using var _repo = repository;

        // Act: "missed" routes through the manager to the reconcile trigger seam.
        await worker.ProcessItemAsync(
            new LifecycleWorkItem("sub-1", LifecycleEvents.Missed),
            CancellationToken.None
        );

        // Assert
        trigger
            .TriggeredMailboxes.Should()
            .Equal(["paula@contoso.com"], "the lifecycle item reaches the manager's routing");
        client.Verify(
            c =>
                c.GetMessageAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "lifecycle items never trigger a message fetch"
        );
        graphRequests.Should().BeEmpty("missed routing issues no Graph call from the manager");
    }
}
