using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Targeted regression tests for issue #45, acceptance criterion AC-4: the
/// <see cref="CacheRepository"/> must persist and retrieve the new
/// <see cref="EventDto.ResponseStatus"/> value, including the null case.
/// Uses an in-memory SQLite database (shared-cache mode) so no temp files are created.
/// </summary>
[TestClass]
public sealed class CacheRepositoryResponseStatusTests
{
    private static EventDto BuildEvent(string bridgeId, int? responseStatus)
    {
        var start = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);
        return new EventDto(
            BridgeId: bridgeId,
            GlobalAppointmentId: $"gid-{bridgeId}",
            Subject: "AC-4 round-trip",
            StartUtc: start,
            EndUtc: end,
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
    public async Task UpsertEvent_then_GetEvent_should_round_trip_response_status_when_declined()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=rs-declined-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        const string bridgeId = "bridge-rs-4";
        var evt = BuildEvent(bridgeId, responseStatus: 4);

        // Act
        await repo.UpsertEventAsync("entry-1", "store-1", "gid-1", evt);
        var loaded = await repo.GetEventAsync(bridgeId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.ResponseStatus.Should().Be(4, "Declined (4) must round-trip through SQLite");
    }

    [TestMethod]
    public async Task UpsertEvent_then_GetEvent_should_round_trip_response_status_when_null()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=rs-null-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        const string bridgeId = "bridge-rs-null";
        var evt = BuildEvent(bridgeId, responseStatus: null);

        // Act
        await repo.UpsertEventAsync("entry-2", "store-2", "gid-2", evt);
        var loaded = await repo.GetEventAsync(bridgeId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!
            .ResponseStatus.Should()
            .BeNull("null ResponseStatus must round-trip as NULL (not 0)");
    }
}
