using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Round-trip persistence tests for issue #72 (AC4, AC6): the Core
/// <see cref="OpenClaw.Core.CoreCacheRepository"/> must write and read back all nine new
/// <see cref="EventDto"/> Graph-shaped fields identically, and its new schema migration must be
/// idempotent across two <c>InitializeAsync</c> calls. Uses in-memory shared-cache SQLite so no
/// temp files are created.
/// </summary>
[TestClass]
public sealed class CoreCacheRepositoryGraphFieldsTests
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

    private static EventDto BuildEvent(string bridgeId, string[]? categories)
    {
        var start = new DateTimeOffset(2026, 5, 3, 15, 0, 0, TimeSpan.Zero);
        return new EventDto(
            BridgeId: bridgeId,
            GlobalAppointmentId: $"gid-{bridgeId}",
            Subject: "Core graph round-trip",
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
            ResponseStatus: null,
            Categories: categories,
            IsOrganizer: true,
            IsOnlineMeeting: true,
            AllowNewTimeProposals: true,
            ICalUId: $"gid-{bridgeId}",
            SeriesMasterId: $"gid-{bridgeId}",
            LastModifiedDateTime: new DateTimeOffset(2026, 5, 2, 10, 15, 0, TimeSpan.Zero),
            BodyFull: "the full untruncated body",
            SensitivityLabel: "private"
        );
    }

    [TestMethod]
    public async Task UpsertEvents_then_GetEvent_should_round_trip_all_nine_graph_fields_populated()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(
            $"Data Source=core-graph-pop-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var evt = BuildEvent("core-graph-pop", categories: ["Red", "Blue Sky", "Green"]);

        // Act
        await repo.UpsertEventsAsync(
            [evt],
            ReadyBridge,
            "req-1",
            new DateTimeOffset(2026, 5, 3, 15, 5, 0, TimeSpan.Zero)
        );
        var loaded = await repo.GetEventAsync("core-graph-pop");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().Equal("Red", "Blue Sky", "Green");
        loaded.IsOrganizer.Should().BeTrue();
        loaded.IsOnlineMeeting.Should().BeTrue();
        loaded.AllowNewTimeProposals.Should().BeTrue();
        loaded.ICalUId.Should().Be("gid-core-graph-pop");
        loaded.SeriesMasterId.Should().Be("gid-core-graph-pop");
        loaded
            .LastModifiedDateTime.Should()
            .Be(new DateTimeOffset(2026, 5, 2, 10, 15, 0, TimeSpan.Zero));
        loaded.BodyFull.Should().Be("the full untruncated body");
        loaded.SensitivityLabel.Should().Be("private");
    }

    [TestMethod]
    public async Task UpsertEvents_then_GetEvent_should_round_trip_empty_categories()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(
            $"Data Source=core-graph-empty-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var evt = BuildEvent("core-graph-empty", categories: Array.Empty<string>());

        // Act
        await repo.UpsertEventsAsync(
            [evt],
            ReadyBridge,
            "req-2",
            new DateTimeOffset(2026, 5, 3, 15, 6, 0, TimeSpan.Zero)
        );
        var loaded = await repo.GetEventAsync("core-graph-empty");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().NotBeNull();
        loaded.Categories.Should().BeEmpty("an empty categories array round-trips as empty");
    }

    [TestMethod]
    public async Task InitializeAsync_should_be_idempotent_across_two_calls()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(
            $"Data Source=core-graph-idem-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );

        // Act: initializing twice must not raise a "duplicate column" error.
        await repo.InitializeAsync();
        var secondInit = async () => await repo.InitializeAsync();

        // Assert
        await secondInit
            .Should()
            .NotThrowAsync("the Core events schema migration must be idempotent");

        // A round-trip still works after the second migration.
        var evt = BuildEvent("core-graph-idem", categories: ["X"]);
        await repo.UpsertEventsAsync(
            [evt],
            ReadyBridge,
            "req-3",
            new DateTimeOffset(2026, 5, 3, 15, 7, 0, TimeSpan.Zero)
        );
        var loaded = await repo.GetEventAsync("core-graph-idem");
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().Equal("X");
    }
}
