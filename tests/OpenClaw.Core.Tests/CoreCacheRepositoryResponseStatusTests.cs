using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Targeted regression tests for issue #80: the Core
/// <see cref="OpenClaw.Core.CoreCacheRepository"/> must persist and retrieve
/// <see cref="EventDto.ResponseStatus"/>, including the null case (NULL must round-trip as null,
/// never coerce to 0), and its schema migration must add the <c>response_status</c> column to an
/// existing database idempotently. Uses in-memory shared-cache SQLite so no temp files are created.
/// </summary>
[TestClass]
public sealed class CoreCacheRepositoryResponseStatusTests
{
    private static readonly BridgeStatusDto ReadyBridge = new(
        BridgeState.ready.ToString(),
        BridgeMode.enhanced.ToString(),
        true,
        false,
        null,
        null,
        null
    );

    /// <summary>
    /// The Core <c>events</c> DDL as it existed before issue #80, without the
    /// <c>response_status</c> column. Test 3 seeds an existing database with this shape to
    /// exercise the guarded-ALTER upgrade path.
    /// </summary>
    private const string PreFixEventsDdl =
        @"
CREATE TABLE IF NOT EXISTS events(
    bridge_id TEXT PRIMARY KEY,
    global_appointment_id TEXT NULL,
    subject TEXT NULL,
    start_utc TEXT NOT NULL,
    end_utc TEXT NOT NULL,
    location TEXT NULL,
    busy_status INTEGER NULL,
    meeting_status INTEGER NULL,
    is_recurring INTEGER NOT NULL,
    sensitivity INTEGER NULL,
    organizer TEXT NULL,
    required_attendees_json TEXT NULL,
    optional_attendees_json TEXT NULL,
    resources_json TEXT NULL,
    body_preview TEXT NULL,
    protected_fields_available INTEGER NOT NULL,
    is_redacted INTEGER NOT NULL,
    bridge_mode TEXT NOT NULL,
    cache_stale INTEGER NOT NULL,
    stale_reason TEXT NULL,
    adapter_request_id TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    last_modified_utc TEXT NULL,
    categories_json TEXT NULL,
    is_organizer INTEGER NOT NULL DEFAULT 0,
    is_online_meeting INTEGER NOT NULL DEFAULT 0,
    allow_new_time_proposals INTEGER NOT NULL DEFAULT 0,
    ical_uid TEXT NULL,
    series_master_id TEXT NULL,
    body_full TEXT NULL,
    sensitivity_label TEXT NULL
);";

    private static EventDto BuildEvent(string bridgeId, int? responseStatus)
    {
        var start = new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero);
        return new EventDto(
            BridgeId: bridgeId,
            GlobalAppointmentId: $"gid-{bridgeId}",
            Subject: "Core response_status round-trip",
            StartUtc: start,
            EndUtc: start.AddHours(1),
            Location: "Test Room",
            BusyStatus: 2,
            MeetingStatus: 1,
            IsRecurring: false,
            Sensitivity: 0,
            Organizer: "organizer@test",
            RequiredAttendeesJson: null,
            OptionalAttendeesJson: null,
            ResourcesJson: null,
            BodyPreview: null,
            ProtectedFieldsAvailable: false,
            IsRedacted: false,
            ResponseStatus: responseStatus
        );
    }

    [TestMethod]
    public async Task UpsertEvents_then_GetEvent_should_round_trip_response_status_when_declined()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(
            $"Data Source=core-rs-declined-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        const string bridgeId = "core-rs-4";
        var evt = BuildEvent(bridgeId, responseStatus: 4);

        // Act
        await repo.UpsertEventsAsync(
            [evt],
            ReadyBridge,
            "req-rs-1",
            new DateTimeOffset(2026, 7, 1, 14, 5, 0, TimeSpan.Zero)
        );
        var loaded = await repo.GetEventAsync(bridgeId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.ResponseStatus.Should().Be(4, "Declined (4) must round-trip through SQLite");
    }

    [TestMethod]
    public async Task UpsertEvents_then_GetEvent_should_round_trip_response_status_when_null()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(
            $"Data Source=core-rs-null-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        const string bridgeId = "core-rs-null";
        var evt = BuildEvent(bridgeId, responseStatus: null);

        // Act
        await repo.UpsertEventsAsync(
            [evt],
            ReadyBridge,
            "req-rs-2",
            new DateTimeOffset(2026, 7, 1, 14, 6, 0, TimeSpan.Zero)
        );
        var loaded = await repo.GetEventAsync(bridgeId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!
            .ResponseStatus.Should()
            .BeNull("null ResponseStatus must round-trip as NULL (not 0)");
    }

    [TestMethod]
    public async Task InitializeAsync_should_add_response_status_column_to_existing_database()
    {
        // Arrange: seed an existing database (pre-#80 events shape, no response_status column)
        // on the shared-cache connection string. The anchor connection stays open for the whole
        // test so the in-memory database survives across the repository's own connections.
        var connectionString =
            $"Data Source=core-rs-migrate-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        var seed = anchor.CreateCommand();
        seed.CommandText = PreFixEventsDdl;
        await seed.ExecuteNonQueryAsync();

        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);

        // Act: first initialization runs the guarded ALTER upgrade path; the second call must be
        // idempotent (no "duplicate column" error).
        await repo.InitializeAsync();
        var secondInit = async () => await repo.InitializeAsync();

        // Assert
        await secondInit.Should().NotThrowAsync("the response_status migration must be idempotent");

        // A response_status round-trip works on the migrated database.
        var evt = BuildEvent("core-rs-migrate", responseStatus: 4);
        await repo.UpsertEventsAsync(
            [evt],
            ReadyBridge,
            "req-rs-3",
            new DateTimeOffset(2026, 7, 1, 14, 7, 0, TimeSpan.Zero)
        );
        var loaded = await repo.GetEventAsync("core-rs-migrate");
        loaded.Should().NotBeNull();
        loaded!
            .ResponseStatus.Should()
            .Be(4, "the migrated column must persist and return the written value");
    }
}
