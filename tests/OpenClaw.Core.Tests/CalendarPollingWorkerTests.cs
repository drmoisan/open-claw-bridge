using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for <see cref="CalendarPollingWorker.RunCalendarPollOnceAsync"/>.
/// Each test exercises a single branch or observable side-effect of one poll cycle using an
/// in-memory SQLite repository and a Moq-stubbed <see cref="IHostAdapterClient"/>.
/// <c>ExecuteAsync</c> (the background loop) is not tested here; it is an infrastructure
/// concern covered by the integration test layer.
/// </summary>
[TestClass]
public class CalendarPollingWorkerTests
{
    // ─── Arrange helpers ──────────────────────────────────────────────────────────

    private static readonly BridgeStatusDto ReadyBridge = new(
        BridgeState.ready.ToString(),
        BridgeMode.safe.ToString(),
        true,
        false,
        null,
        null,
        null
    );

    private static readonly BridgeStatusDto DegradedBridge = new(
        BridgeState.degraded.ToString(),
        BridgeMode.safe.ToString(),
        false,
        true,
        "Bridge cache is stale.",
        null,
        null
    );

    private static readonly EventDto SampleEvent = new(
        "event-unit-1",
        "global-unit-1",
        "Unit Test Meeting",
        new DateTimeOffset(2026, 4, 20, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 4, 20, 10, 0, 0, TimeSpan.Zero),
        "Room A",
        2,
        1,
        false,
        0,
        "organizer@example.com",
        null,
        null,
        null,
        null,
        false,
        false
    );

    /// <summary>
    /// Creates a unique in-memory SQLite repository, initialized and ready for use.
    /// Each test gets its own database to remain fully isolated.
    /// </summary>
    private static async Task<CoreCacheRepository> CreateRepositoryAsync()
    {
        var repo = new CoreCacheRepository(
            $"Data Source=calendar-worker-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        return repo;
    }

    /// <summary>
    /// Builds an <see cref="IOptions{OpenClawOptions}"/> with configurable polling settings.
    /// Defaults produce a small window (2 past / 5 future days) and a limit of 100.
    /// </summary>
    private static IOptions<OpenClawOptions> BuildOptions(
        int pastDays = 2,
        int futureDays = 5,
        int limit = 100
    ) =>
        Options.Create(
            new OpenClawOptions
            {
                Polling = new PollingOptions
                {
                    CalendarPastDays = pastDays,
                    CalendarFutureDays = futureDays,
                },
                Defaults = new DefaultOptions { Limit = limit },
            }
        );

    /// <summary>
    /// Builds a <see cref="CalendarPollingWorker"/> wired to the supplied client and repository
    /// with a null logger (output is irrelevant for these unit tests).
    /// </summary>
    private static CalendarPollingWorker BuildWorker(
        IHostAdapterClient client,
        CoreCacheRepository repository,
        CoreHealthState? healthState = null,
        IOptions<OpenClawOptions>? options = null
    ) =>
        new(
            client,
            repository,
            healthState ?? new CoreHealthState(),
            options ?? BuildOptions(),
            NullLogger<CalendarPollingWorker>.Instance
        );

    /// <summary>
    /// Configures the mock to return a successful envelope carrying the given bridge status
    /// and event list.
    /// </summary>
    private static Mock<IHostAdapterClient> SuccessClientMock(
        BridgeStatusDto bridge,
        IReadOnlyList<EventDto>? events = null,
        string requestId = "test-req"
    )
    {
        var mock = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        mock.Setup(c =>
                c.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    true,
                    new ItemsResponse<EventDto>(events ?? [SampleEvent]),
                    new ApiMeta(requestId, "1.0", bridge),
                    null
                )
            );
        return mock;
    }

    /// <summary>
    /// Configures the mock to return a failed envelope. Bridge status in the meta is optional.
    /// </summary>
    private static Mock<IHostAdapterClient> FailureClientMock(
        BridgeStatusDto? bridgeInMeta = null,
        string? errorCode = "TRANSPORT_FAILURE",
        string? errorMessage = "HostAdapter unreachable.",
        string requestId = "test-req"
    )
    {
        var mock = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        mock.Setup(c =>
                c.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    false,
                    null,
                    new ApiMeta(requestId, "1.0", bridgeInMeta),
                    errorCode is not null
                        ? new ApiError(errorCode, errorMessage ?? string.Empty)
                        : null
                )
            );
        return mock;
    }

    // ─── Success path ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a successful poll persists the returned events to the cache repository.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenSuccessful_UpsertsReturnedEvents()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var client = SuccessClientMock(ReadyBridge, [SampleEvent]);
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: the event is retrievable from the repository
        var stored = await repo.GetEventAsync(SampleEvent.BridgeId);
        stored.Should().NotBeNull("the event returned by the client must be persisted");
        stored!.BridgeId.Should().Be(SampleEvent.BridgeId);
        stored.Subject.Should().Be(SampleEvent.Subject);
    }

    /// <summary>
    /// Verifies that a successful poll creates an ingest run record with outcome "success".
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenSuccessful_RecordsSuccessIngestRun()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var client = SuccessClientMock(ReadyBridge, requestId: "ok-req");
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: ingest run records the correct operation and outcome
        var run = await ReadLatestIngestRunAsync(repo);
        run.OperationName.Should().Be("calendar_window");
        run.Outcome.Should().Be("success");
        run.ErrorMessage.Should().BeNull("a successful poll must not record an error message");
    }

    /// <summary>
    /// Verifies that a successful poll advances the calendar cursor in the repository.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenSuccessful_SetsCursorForCalendarWindow()
    {
        // Arrange
        var beforeRunUtc = DateTimeOffset.UtcNow;
        using var repo = await CreateRepositoryAsync();
        var client = SuccessClientMock(ReadyBridge);
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: cursor is set to a timestamp within the test window
        var cursor = await repo.GetCursorAsync("calendar_window_last_run_utc");
        cursor.Should().NotBeNull("cursor must be written after a successful poll");
        cursor!.Value.Should().BeOnOrAfter(beforeRunUtc);
    }

    /// <summary>
    /// Verifies that a successful poll upserts the bridge status snapshot returned in the envelope.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenSuccessful_UpsertsBridgeStatusSnapshot()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var client = SuccessClientMock(ReadyBridge);
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: a bridge status snapshot is stored
        var snapshot = await repo.GetLatestBridgeStatusSnapshotAsync();
        snapshot.Should().NotBeNull();
        snapshot!.BridgeStatus.State.Should().Be(ReadyBridge.State);
    }

    /// <summary>
    /// Verifies that a successful poll marks the health state as reachable.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenSuccessful_MarksHealthStateAsPollSuccess()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var healthState = new CoreHealthState();
        healthState.MarkDatabaseReady();
        var client = SuccessClientMock(ReadyBridge);
        var worker = BuildWorker(client.Object, repo, healthState);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: health snapshot reflects a successful poll
        var snapshot = healthState.GetSnapshot();
        snapshot.HostAdapterReachable.Should().BeTrue();
        snapshot.LastSuccessfulPollUtc.Should().NotBeNull();
        snapshot.LastFailureReason.Should().BeNull();
    }

    /// <summary>
    /// Verifies that a successful poll with null <c>Data</c> (no items) does not throw and
    /// still writes the cursor and ingest run.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenSuccessfulWithNullData_CompletesWithoutThrowing()
    {
        // Arrange: mock returns Ok=true but Data=null (valid edge case from the adapter)
        var mock = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        mock.Setup(c =>
                c.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    true,
                    null, // null Data — code guards with Data?.Items ?? Array.Empty
                    new ApiMeta("null-data-req", "1.0", ReadyBridge),
                    null
                )
            );
        using var repo = await CreateRepositoryAsync();
        var worker = BuildWorker(mock.Object, repo);

        // Act + Assert: must not throw
        Func<Task> act = () => worker.RunCalendarPollOnceAsync();
        await act.Should().NotThrowAsync("null Data must be treated as an empty event list");

        var run = await ReadLatestIngestRunAsync(repo);
        run.Outcome.Should().Be("success");
        var counts = await repo.GetCountsAsync();
        counts.Events.Should().Be(0, "no events to store when Data is null");
    }

    /// <summary>
    /// Verifies that a successful poll passes the configured limit option to the client.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_PassesConfiguredLimitToClient()
    {
        // Arrange
        int? capturedLimit = null;
        var mock = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        mock.Setup(c =>
                c.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DateTimeOffset, DateTimeOffset, int, string?, CancellationToken>(
                (_, _, limit, _, _) => capturedLimit = limit
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    true,
                    new ItemsResponse<EventDto>([]),
                    new ApiMeta("lim-req", "1.0", ReadyBridge),
                    null
                )
            );
        using var repo = await CreateRepositoryAsync();
        // Configure a non-default limit of 42 to confirm the value is forwarded
        var worker = BuildWorker(mock.Object, repo, options: BuildOptions(limit: 42));

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert
        capturedLimit.Should().Be(42, "worker must forward Defaults.Limit to the client");
    }

    /// <summary>
    /// Verifies that the worker sends a non-empty GUID string as the request ID to the client.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_SendsGuidRequestIdToClient()
    {
        // Arrange
        string? capturedRequestId = null;
        var mock = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        mock.Setup(c =>
                c.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DateTimeOffset, DateTimeOffset, int, string?, CancellationToken>(
                (_, _, _, requestId, _) => capturedRequestId = requestId
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    true,
                    new ItemsResponse<EventDto>([]),
                    new ApiMeta("req", "1.0", ReadyBridge),
                    null
                )
            );
        using var repo = await CreateRepositoryAsync();
        var worker = BuildWorker(mock.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: worker generates a GUID as the per-request correlation ID
        capturedRequestId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(capturedRequestId, out _)
            .Should()
            .BeTrue($"request ID '{capturedRequestId}' must be a valid GUID");
    }

    // ─── Edge case: success envelope but null Bridge in Meta ─────────────────────

    /// <summary>
    /// Verifies that when <c>Ok = true</c> but <c>Meta.Bridge</c> is null the worker falls
    /// through to the failure path — no events are stored and the poll is recorded as failed.
    /// This matches the guard condition <c>envelope.Ok &amp;&amp; envelope.Meta.Bridge is not null</c>.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenOkTrueButBridgeNull_FallsThroughToFailurePath()
    {
        // Arrange: the client returns Ok=true but no bridge status snapshot in the meta.
        // The worker requires both conditions to treat a poll as successful.
        var mock = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        mock.Setup(c =>
                c.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    true,
                    new ItemsResponse<EventDto>([SampleEvent]),
                    new ApiMeta("no-bridge-req", "1.0", null), // Bridge is null
                    null
                )
            );
        using var repo = await CreateRepositoryAsync();
        var healthState = new CoreHealthState();
        var worker = BuildWorker(mock.Object, repo, healthState);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: no events stored because the success branch was not taken
        var counts = await repo.GetCountsAsync();
        counts.Events.Should().Be(0, "events must not be stored when Meta.Bridge is null");

        // The cursor must not be written either
        var cursor = await repo.GetCursorAsync("calendar_window_last_run_utc");
        cursor.Should().BeNull("cursor must not be set when the success branch is not taken");

        // Ingest run records "failed" outcome
        var run = await ReadLatestIngestRunAsync(repo);
        run.Outcome.Should().Be("failed");

        // Health state reflects the failure
        healthState
            .GetSnapshot()
            .HostAdapterReachable.Should()
            .BeFalse("MarkPollFailure must be called when Bridge is absent even on Ok=true");
    }

    // ─── Failure path ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a failed poll records the ingest run with outcome "failed" and the
    /// adapter error message.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenFailed_RecordsFailedIngestRun()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var client = FailureClientMock(errorMessage: "Connection refused.");
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert
        var run = await ReadLatestIngestRunAsync(repo);
        run.OperationName.Should().Be("calendar_window");
        run.Outcome.Should().Be("failed");
        run.ErrorMessage.Should().Be("Connection refused.");
    }

    /// <summary>
    /// Verifies that a failed poll does NOT advance the calendar cursor.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenFailed_DoesNotSetCalendarCursor()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var client = FailureClientMock();
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert
        var cursor = await repo.GetCursorAsync("calendar_window_last_run_utc");
        cursor.Should().BeNull("a failed poll must not advance the calendar cursor");
    }

    /// <summary>
    /// Verifies that a failed poll marks the health state as unreachable with the error reason.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenFailed_MarksHealthStateAsFailure()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var healthState = new CoreHealthState();
        healthState.MarkDatabaseReady();
        var client = FailureClientMock(errorMessage: "Timeout occurred.");
        var worker = BuildWorker(client.Object, repo, healthState);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert
        var snapshot = healthState.GetSnapshot();
        snapshot.HostAdapterReachable.Should().BeFalse();
        snapshot.LastFailureReason.Should().Be("Timeout occurred.");
    }

    /// <summary>
    /// Verifies that a failed poll with a bridge status in the meta still upserts that
    /// bridge status snapshot (the partial information is preserved for diagnostics).
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenFailed_WithBridgeInMeta_UpsertsBridgeStatus()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var client = FailureClientMock(bridgeInMeta: DegradedBridge);
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: bridge status is still stored for diagnostics even on failure
        var snapshot = await repo.GetLatestBridgeStatusSnapshotAsync();
        snapshot.Should().NotBeNull();
        snapshot!.BridgeStatus.State.Should().Be(DegradedBridge.State);
    }

    /// <summary>
    /// Verifies that a failed poll with no bridge status in the meta does NOT attempt to
    /// upsert a bridge status snapshot.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenFailed_WithNullBridgeInMeta_DoesNotUpsertBridgeStatus()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var client = FailureClientMock(bridgeInMeta: null);
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: no bridge status snapshot is written
        var snapshot = await repo.GetLatestBridgeStatusSnapshotAsync();
        snapshot
            .Should()
            .BeNull(
                "bridge status must not be upserted when Meta.Bridge is null on a failed envelope"
            );
    }

    /// <summary>
    /// Verifies that when the envelope carries a null <c>Error</c>, the failure path falls
    /// back to the "Unknown HostAdapter failure." message rather than throwing.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenFailed_WithNullError_UsesUnknownFallbackMessage()
    {
        // Arrange: Ok=false, Error=null — client did not supply an error detail
        var mock = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        mock.Setup(c =>
                c.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    false,
                    null,
                    new ApiMeta("null-err-req", "1.0", null),
                    null // Error is null
                )
            );
        using var repo = await CreateRepositoryAsync();
        var healthState = new CoreHealthState();
        var worker = BuildWorker(mock.Object, repo, healthState);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert: fallback message used and no exception thrown
        var snapshot = healthState.GetSnapshot();
        snapshot
            .LastFailureReason.Should()
            .Be(
                "Unknown HostAdapter failure.",
                "null Error must be replaced by the fallback message"
            );
    }

    /// <summary>
    /// Verifies that a failed poll does not persist any events from the previous run or
    /// from the current (failed) response.
    /// </summary>
    [TestMethod]
    public async Task RunCalendarPollOnceAsync_WhenFailed_DoesNotPersistEvents()
    {
        // Arrange
        using var repo = await CreateRepositoryAsync();
        var client = FailureClientMock();
        var worker = BuildWorker(client.Object, repo);

        // Act
        await worker.RunCalendarPollOnceAsync();

        // Assert
        var counts = await repo.GetCountsAsync();
        counts.Events.Should().Be(0, "a failed poll must not write any events to the cache");
    }

    // ─── Repository read helper ───────────────────────────────────────────────────

    /// <summary>
    /// Reads the most recently inserted ingest run row from the repository's underlying
    /// database using the internal connection string.
    /// </summary>
    private static async Task<(
        string OperationName,
        string Outcome,
        string? RequestId,
        string? ErrorMessage
    )> ReadLatestIngestRunAsync(CoreCacheRepository repository)
    {
        // CoreCacheRepository exposes an internal constructor that accepts a connection string.
        // Since the repository holds an in-memory anchor connection, we create a second
        // connection on the same shared cache to read back the persisted rows.
        // The connection string is available via the internal test seam built into the class.
        var connectionString = repository.ConnectionString;
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT operation_name, outcome, request_id, error_message "
            + "FROM ingest_runs ORDER BY id DESC LIMIT 1;";

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return (string.Empty, string.Empty, null, null);

        return (
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3)
        );
    }
}
