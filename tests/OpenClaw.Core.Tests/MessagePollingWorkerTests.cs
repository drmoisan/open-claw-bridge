using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for <see cref="MessagePollingWorker"/> covering scenarios not exercised by
/// the integration-style tests in <see cref="CorePollerTests"/>: specifically, the failure
/// path through <c>PersistPollResultAsync</c>, cursor-reuse behavior, SentUtc cursor
/// fallback, and health-state marking.
/// </summary>
[TestClass]
public class MessagePollingWorkerTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static IOptions<OpenClawOptions> DefaultOptions() =>
        Options.Create(
            new OpenClawOptions
            {
                Defaults = new DefaultOptions { Limit = 50 },
                Polling = new PollingOptions { MessageLookbackHours = 24 },
            }
        );

    private static BridgeStatusDto ReadyBridge() =>
        new(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );

    private static MessagePollingWorker BuildWorker(
        IHostAdapterClient client,
        CoreCacheRepository repository,
        CoreHealthState healthState,
        IOptions<OpenClawOptions>? options = null
    ) =>
        new(
            client,
            repository,
            healthState,
            options ?? DefaultOptions(),
            NullLogger<MessagePollingWorker>.Instance
        );

    /// <summary>
    /// Reads the most-recently recorded ingest run from the SQLite database so that tests
    /// can assert on outcome and error message without coupling to private methods.
    /// </summary>
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

    // ─── Failure path — messages ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when <c>ListMessagesAsync</c> returns a failed envelope the worker
    /// marks the host adapter as unreachable, records the error reason in the health state,
    /// and writes a "failed" ingest run to the repository.
    /// </summary>
    [TestMethod]
    public async Task RunMessagePollOnceAsync_WhenEnvelopeIndicatesFailure_MarksHealthUnreachableAndRecordsFailedRun()
    {
        // Arrange
        var connectionString =
            $"Data Source=msg-failure-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        using var repository = new CoreCacheRepository(connectionString);
        await repository.InitializeAsync();
        var healthState = new CoreHealthState();

        const string errorMessage = "Outlook COM server is unavailable.";
        client
            .Setup(c =>
                c.ListMessagesAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<MessageDto>>(
                    false,
                    null,
                    new ApiMeta("req-fail-1", "test-version", null),
                    new ApiError("OUTLOOK_UNAVAILABLE", errorMessage)
                )
            );

        var worker = BuildWorker(client.Object, repository, healthState);

        // Act
        await worker.RunMessagePollOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert: health state
        var snapshot = healthState.GetSnapshot();
        snapshot.HostAdapterReachable.Should().BeFalse();
        snapshot.LastFailureReason.Should().Be(errorMessage);

        // Assert: ingest run
        var run = await ReadLatestIngestRunAsync(connectionString);
        run.OperationName.Should().Be("messages");
        run.Outcome.Should().Be("failed");
        run.ErrorMessage.Should().Be(errorMessage);

        client.VerifyAll();
    }

    // ─── Failure path — meeting requests ─────────────────────────────────────────

    /// <summary>
    /// Verifies that when <c>ListMeetingRequestsAsync</c> returns a failed envelope the
    /// worker marks the host adapter as unreachable and records a "failed" ingest run.
    /// </summary>
    [TestMethod]
    public async Task RunMeetingRequestPollOnceAsync_WhenEnvelopeIndicatesFailure_MarksHealthUnreachableAndRecordsFailedRun()
    {
        // Arrange
        var connectionString =
            $"Data Source=mtg-failure-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        using var repository = new CoreCacheRepository(connectionString);
        await repository.InitializeAsync();
        var healthState = new CoreHealthState();

        const string errorMessage = "Bridge pipe timed out.";
        client
            .Setup(c =>
                c.ListMeetingRequestsAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<MessageDto>>(
                    false,
                    null,
                    new ApiMeta("req-mtg-fail", "test-version", null),
                    new ApiError("TIMEOUT", errorMessage)
                )
            );

        var worker = BuildWorker(client.Object, repository, healthState);

        // Act
        await worker.RunMeetingRequestPollOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert: health state
        var snapshot = healthState.GetSnapshot();
        snapshot.HostAdapterReachable.Should().BeFalse();
        snapshot.LastFailureReason.Should().Be(errorMessage);

        // Assert: ingest run
        var run = await ReadLatestIngestRunAsync(connectionString);
        run.OperationName.Should().Be("meeting_requests");
        run.Outcome.Should().Be("failed");
        run.ErrorMessage.Should().Be(errorMessage);

        client.VerifyAll();
    }

    // ─── Cursor reuse ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when a <c>messages_since_utc</c> cursor is already stored the worker
    /// passes that value as <c>sinceUtc</c> to the adapter client rather than falling back
    /// to the lookback window default.
    /// </summary>
    [TestMethod]
    public async Task RunMessagePollOnceAsync_WhenCursorExists_UsesCursorAsSinceUtc()
    {
        // Arrange
        var connectionString =
            $"Data Source=msg-cursor-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        using var repository = new CoreCacheRepository(connectionString);
        await repository.InitializeAsync();
        var healthState = new CoreHealthState();

        // Store a known cursor so the worker should not fall back to the lookback window
        var expectedSince = DateTimeOffset.Parse("2026-04-01T08:00:00Z");
        await repository.SetCursorAsync("messages_since_utc", expectedSince);

        DateTimeOffset capturedSince = default;
        client
            .Setup(c =>
                c.ListMessagesAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DateTimeOffset, int, string?, CancellationToken>(
                (since, _, _, _) => capturedSince = since
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<MessageDto>>(
                    true,
                    new ItemsResponse<MessageDto>([]),
                    new ApiMeta("cursor-req", "test-version", ReadyBridge()),
                    null
                )
            );

        var worker = BuildWorker(client.Object, repository, healthState);

        // Act
        await worker.RunMessagePollOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert: the worker must have used the stored cursor, not the lookback window
        capturedSince.Should().Be(expectedSince);
        client.VerifyAll();
    }

    // ─── SentUtc cursor fallback ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when messages have <c>ReceivedUtc = null</c> the worker falls back
    /// to <c>SentUtc</c> when advancing the cursor.  This ensures the cursor is always
    /// set to the most relevant available timestamp.
    /// </summary>
    [TestMethod]
    public async Task RunMessagePollOnceAsync_WhenMessageHasNullReceivedUtcButSetSentUtc_AdvancesCursorToSentUtc()
    {
        // Arrange
        var connectionString =
            $"Data Source=msg-sentutc-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        using var repository = new CoreCacheRepository(connectionString);
        await repository.InitializeAsync();
        var healthState = new CoreHealthState();

        var sentUtc = DateTimeOffset.Parse("2026-04-15T10:30:00Z");
        var message = new MessageDto(
            "msg-sentutc-only",
            "mail",
            "Subject",
            null, // ReceivedUtc is null
            sentUtc, // SentUtc is set
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

        client
            .Setup(c =>
                c.ListMessagesAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<MessageDto>>(
                    true,
                    new ItemsResponse<MessageDto>([message]),
                    new ApiMeta("sentutc-req", "test-version", ReadyBridge()),
                    null
                )
            );

        var worker = BuildWorker(client.Object, repository, healthState);

        // Act
        await worker.RunMessagePollOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert: cursor should be SentUtc because ReceivedUtc is null
        var cursor = await repository.GetCursorAsync("messages_since_utc");
        cursor.Should().Be(sentUtc);
        client.VerifyAll();
    }

    // ─── Health-state success marking ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that a successful message poll marks the host adapter as reachable in the
    /// health state and records a non-null <c>LastSuccessfulPollUtc</c>.
    /// </summary>
    [TestMethod]
    public async Task RunMessagePollOnceAsync_WhenSuccessful_MarksPollSuccessInHealthState()
    {
        // Arrange
        var connectionString =
            $"Data Source=msg-health-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        using var repository = new CoreCacheRepository(connectionString);
        await repository.InitializeAsync();
        var healthState = new CoreHealthState();

        client
            .Setup(c =>
                c.ListMessagesAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<MessageDto>>(
                    true,
                    new ItemsResponse<MessageDto>([]),
                    new ApiMeta("health-req", "test-version", ReadyBridge()),
                    null
                )
            );

        var worker = BuildWorker(client.Object, repository, healthState);

        var before = DateTimeOffset.UtcNow;

        // Act
        await worker.RunMessagePollOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert: health state should reflect a successful poll
        var snapshot = healthState.GetSnapshot();
        snapshot.HostAdapterReachable.Should().BeTrue();
        snapshot.LastSuccessfulPollUtc.Should().NotBeNull();
        snapshot.LastSuccessfulPollUtc.Should().BeOnOrAfter(before);
        snapshot.LastFailureReason.Should().BeNull();
        client.VerifyAll();
    }
}
