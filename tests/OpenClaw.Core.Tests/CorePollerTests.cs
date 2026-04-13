using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

[TestClass]
public class CorePollerTests
{
    [TestMethod]
    public async Task Core_message_poller_should_insert_new_rows_and_advance_the_message_cursor()
    {
        var hostAdapterClient = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        var options = Options.Create(
            new OpenClaw.Core.OpenClawOptions
            {
                Defaults = new OpenClaw.Core.DefaultOptions { Limit = 100 },
                Polling = new OpenClaw.Core.PollingOptions { MessageLookbackHours = 48 },
            }
        );
        using var repository = new OpenClaw.Core.CoreCacheRepository(
            $"Data Source=core-poller-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repository.InitializeAsync();
        var healthState = new OpenClaw.Core.CoreHealthState();
        var readyBridge = new BridgeStatusDto(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );
        var receivedUtc = DateTimeOffset.Parse("2026-04-12T14:00:00Z");
        var message = new MessageDto(
            "message-1",
            "mail",
            "Subject",
            receivedUtc,
            null,
            null,
            null,
            true,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            true
        );
        hostAdapterClient
            .Setup(client =>
                client.ListMessagesAsync(
                    It.IsAny<DateTimeOffset>(),
                    100,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<MessageDto>>(
                    true,
                    new ItemsResponse<MessageDto>([message]),
                    new ApiMeta("messages-request", "test-version", readyBridge),
                    null
                )
            );

        var worker = new OpenClaw.Core.MessagePollingWorker(
            hostAdapterClient.Object,
            repository,
            healthState,
            options,
            NullLogger<OpenClaw.Core.MessagePollingWorker>.Instance
        );

        await worker.RunMessagePollOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        var storedMessage = await repository.GetMessageAsync("message-1");
        var cursor = await repository.GetCursorAsync("messages_since_utc");
        var counts = await repository.GetCountsAsync();

        storedMessage.Should().NotBeNull();
        storedMessage!.BridgeId.Should().Be("message-1");
        cursor.Should().Be(receivedUtc);
        counts.Messages.Should().Be(1);
        hostAdapterClient.VerifyAll();
    }

    [TestMethod]
    public async Task Core_meeting_request_poller_should_preserve_kind_and_redaction_state()
    {
        var hostAdapterClient = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        var options = Options.Create(
            new OpenClaw.Core.OpenClawOptions
            {
                Defaults = new OpenClaw.Core.DefaultOptions { Limit = 100 },
                Polling = new OpenClaw.Core.PollingOptions { MessageLookbackHours = 48 },
            }
        );
        using var repository = new OpenClaw.Core.CoreCacheRepository(
            $"Data Source=core-meeting-poller-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repository.InitializeAsync();
        var healthState = new OpenClaw.Core.CoreHealthState();
        var readyBridge = new BridgeStatusDto(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );
        var receivedUtc = DateTimeOffset.Parse("2026-04-12T15:00:00Z");
        var meetingRequest = new MessageDto(
            "meeting-1",
            "meeting",
            "Meeting request",
            receivedUtc,
            null,
            null,
            null,
            false,
            false,
            "IPM.Schedule.Meeting.Request",
            null,
            null,
            null,
            null,
            null,
            false,
            true
        );
        hostAdapterClient
            .Setup(client =>
                client.ListMeetingRequestsAsync(
                    It.IsAny<DateTimeOffset>(),
                    100,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<MessageDto>>(
                    true,
                    new ItemsResponse<MessageDto>([meetingRequest]),
                    new ApiMeta("meeting-request", "test-version", readyBridge),
                    null
                )
            );

        var worker = new OpenClaw.Core.MessagePollingWorker(
            hostAdapterClient.Object,
            repository,
            healthState,
            options,
            NullLogger<OpenClaw.Core.MessagePollingWorker>.Instance
        );

        await worker.RunMeetingRequestPollOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        var storedMessage = await repository.GetMessageAsync("meeting-1");
        var counts = await repository.GetCountsAsync();

        storedMessage.Should().NotBeNull();
        storedMessage!.ItemKind.Should().Be("meeting");
        storedMessage.IsRedacted.Should().BeTrue();
        storedMessage.ProtectedFieldsAvailable.Should().BeFalse();
        counts.MeetingRequests.Should().Be(1);
        hostAdapterClient.VerifyAll();
    }

    [TestMethod]
    public async Task Core_calendar_poller_should_persist_bounded_window_events_and_ingest_run_status()
    {
        var hostAdapterClient = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        var options = Options.Create(
            new OpenClaw.Core.OpenClawOptions
            {
                Defaults = new OpenClaw.Core.DefaultOptions { Limit = 25 },
                Polling = new OpenClaw.Core.PollingOptions
                {
                    CalendarPastDays = 2,
                    CalendarFutureDays = 5,
                },
            }
        );
        var connectionString =
            $"Data Source=core-calendar-poller-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var repository = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await repository.InitializeAsync();
        var healthState = new OpenClaw.Core.CoreHealthState();
        var readyBridge = new BridgeStatusDto(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );
        var eventStartUtc = DateTimeOffset.Parse("2026-04-14T09:00:00Z");
        var calendarEvent = new EventDto(
            "event-1",
            "global-1",
            "Quarterly review",
            eventStartUtc,
            eventStartUtc.AddHours(1),
            "Room 101",
            2,
            1,
            false,
            0,
            "OpenClaw",
            null,
            null,
            null,
            null,
            true,
            false
        );
        DateTimeOffset capturedStartUtc = default;
        DateTimeOffset capturedEndUtc = default;
        var beforeRunUtc = DateTimeOffset.UtcNow;
        hostAdapterClient
            .Setup(client =>
                client.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    25,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DateTimeOffset, DateTimeOffset, int, string?, CancellationToken>(
                (startUtc, endUtc, _, _, _) =>
                {
                    capturedStartUtc = startUtc;
                    capturedEndUtc = endUtc;
                }
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    true,
                    new ItemsResponse<EventDto>([calendarEvent]),
                    new ApiMeta("calendar-request", "test-version", readyBridge),
                    null
                )
            );

        var worker = new OpenClaw.Core.CalendarPollingWorker(
            hostAdapterClient.Object,
            repository,
            healthState,
            options,
            NullLogger<OpenClaw.Core.CalendarPollingWorker>.Instance
        );

        await worker.RunCalendarPollOnceAsync(CancellationToken.None);

        var afterRunUtc = DateTimeOffset.UtcNow;
        var storedEvent = await repository.GetEventAsync("event-1");
        var storedEvents = await repository.ListEventsAsync(
            eventStartUtc.AddHours(-1),
            eventStartUtc.AddHours(2),
            10
        );
        var cursor = await repository.GetCursorAsync("calendar_window_last_run_utc");
        var lastSuccessfulPollUtc = await repository.GetLastSuccessfulPollUtcAsync();
        var counts = await repository.GetCountsAsync();
        var ingestRun = await ReadLatestIngestRunAsync(connectionString);

        capturedStartUtc.Should().BeOnOrAfter(beforeRunUtc.AddDays(-2).AddSeconds(-5));
        capturedStartUtc.Should().BeOnOrBefore(afterRunUtc.AddDays(-2).AddSeconds(5));
        capturedEndUtc.Should().BeOnOrAfter(beforeRunUtc.AddDays(5).AddSeconds(-5));
        capturedEndUtc.Should().BeOnOrBefore(afterRunUtc.AddDays(5).AddSeconds(5));
        storedEvent.Should().NotBeNull();
        storedEvent!.BridgeId.Should().Be("event-1");
        storedEvents.Should().ContainSingle(evt => evt.BridgeId == "event-1");
        cursor.Should().NotBeNull();
        lastSuccessfulPollUtc.Should().NotBeNull();
        counts.Events.Should().Be(1);
        ingestRun.OperationName.Should().Be("calendar_window");
        ingestRun.Outcome.Should().Be("success");
        ingestRun.RequestId.Should().Be("calendar-request");
        ingestRun.ErrorMessage.Should().BeNull();
        hostAdapterClient.VerifyAll();
    }

    private static async Task<(
        string OperationName,
        string Outcome,
        string? RequestId,
        string? ErrorMessage
    )> ReadLatestIngestRunAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            @"
SELECT operation_name, outcome, request_id, error_message
FROM ingest_runs
ORDER BY id DESC
LIMIT 1;";

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        return (
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3)
        );
    }
}
