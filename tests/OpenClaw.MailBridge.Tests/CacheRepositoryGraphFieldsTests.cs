using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Round-trip persistence tests for issue #72 (AC4, AC6): the bridge
/// <see cref="CacheRepository"/> must write and read back all nine new
/// <see cref="EventDto"/> Graph-shaped fields identically, and its schema migration must be
/// idempotent. Uses in-memory shared-cache SQLite so no temp files are created.
/// </summary>
[TestClass]
public sealed class CacheRepositoryGraphFieldsTests
{
    private static EventDto BuildEvent(string bridgeId, string[]? categories)
    {
        var start = new DateTimeOffset(2026, 5, 2, 14, 0, 0, TimeSpan.Zero);
        return new EventDto(
            BridgeId: bridgeId,
            GlobalAppointmentId: $"gid-{bridgeId}",
            Subject: "Graph round-trip",
            StartUtc: start,
            EndUtc: start.AddHours(1),
            Location: "Test Room",
            BusyStatus: 2,
            MeetingStatus: 1,
            IsRecurring: true,
            Sensitivity: 2,
            Organizer: "organizer@test",
            RequiredAttendeesJson: null,
            OptionalAttendeesJson: null,
            ResourcesJson: null,
            BodyPreview: "preview",
            ProtectedFieldsAvailable: true,
            IsRedacted: false,
            ResponseStatus: 3,
            Categories: categories,
            IsOrganizer: true,
            IsOnlineMeeting: true,
            AllowNewTimeProposals: true,
            ICalUId: $"gid-{bridgeId}",
            SeriesMasterId: $"gid-{bridgeId}",
            LastModifiedDateTime: new DateTimeOffset(2026, 5, 1, 9, 30, 0, TimeSpan.Zero),
            BodyFull: "the full untruncated body",
            SensitivityLabel: "private"
        );
    }

    [TestMethod]
    public async Task UpsertEvent_then_GetEvent_should_round_trip_all_nine_graph_fields_populated()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=graph-pop-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var evt = BuildEvent("bridge-graph-pop", categories: ["Red", "Blue Sky", "Green"]);

        // Act
        await repo.UpsertEventAsync("entry-1", "store-1", "gid-1", evt);
        var loaded = await repo.GetEventAsync("bridge-graph-pop");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().Equal("Red", "Blue Sky", "Green");
        loaded.IsOrganizer.Should().BeTrue();
        loaded.IsOnlineMeeting.Should().BeTrue();
        loaded.AllowNewTimeProposals.Should().BeTrue();
        loaded.ICalUId.Should().Be("gid-bridge-graph-pop");
        loaded.SeriesMasterId.Should().Be("gid-bridge-graph-pop");
        loaded
            .LastModifiedDateTime.Should()
            .Be(new DateTimeOffset(2026, 5, 1, 9, 30, 0, TimeSpan.Zero));
        loaded.BodyFull.Should().Be("the full untruncated body");
        loaded.SensitivityLabel.Should().Be("private");
    }

    [TestMethod]
    public async Task UpsertEvent_then_GetEvent_should_round_trip_empty_categories()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=graph-empty-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var evt = BuildEvent("bridge-graph-empty", categories: Array.Empty<string>());

        // Act
        await repo.UpsertEventAsync("entry-2", "store-2", "gid-2", evt);
        var loaded = await repo.GetEventAsync("bridge-graph-empty");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().NotBeNull();
        loaded.Categories.Should().BeEmpty("an empty categories array round-trips as empty");
    }

    [TestMethod]
    public async Task InitializeAsync_should_be_idempotent_across_two_calls()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=graph-idem-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );

        // Act: initializing twice must not raise a "duplicate column" error.
        await repo.InitializeAsync();
        var secondInit = async () => await repo.InitializeAsync();

        // Assert
        await secondInit.Should().NotThrowAsync("the events schema migration must be idempotent");

        // And a round-trip still works after the second migration.
        var evt = BuildEvent("bridge-graph-idem", categories: ["X"]);
        await repo.UpsertEventAsync("entry-3", "store-3", "gid-3", evt);
        var loaded = await repo.GetEventAsync("bridge-graph-idem");
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().Equal("X");
    }
}
