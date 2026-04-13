using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

[TestClass]
public class CoreEventsApiTests
{
    [TestMethod]
    public async Task Core_events_window_should_enforce_start_end_and_limit_filters()
    {
        var bridgeStatus = new BridgeStatusDto(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );
        using var factory = new CoreTestWebApplicationFactory(healthState =>
        {
            healthState.MarkDatabaseReady();
            healthState.MarkPollSuccess(bridgeStatus, DateTimeOffset.Parse("2026-04-12T18:30:00Z"));
        });
        using var client = factory.CreateClient();
        await factory.InitializeRepositoryAsync(repository =>
            repository.UpsertEventsAsync(
                [
                    new EventDto(
                        "event-before-window",
                        null,
                        "Before window",
                        DateTimeOffset.Parse("2026-04-12T07:00:00Z"),
                        DateTimeOffset.Parse("2026-04-12T08:00:00Z"),
                        null,
                        null,
                        null,
                        false,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        false,
                        true
                    ),
                    new EventDto(
                        "event-first-in-window",
                        null,
                        "First in window",
                        DateTimeOffset.Parse("2026-04-12T09:00:00Z"),
                        DateTimeOffset.Parse("2026-04-12T10:00:00Z"),
                        null,
                        null,
                        null,
                        false,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        false,
                        true
                    ),
                    new EventDto(
                        "event-second-in-window",
                        null,
                        "Second in window",
                        DateTimeOffset.Parse("2026-04-12T11:00:00Z"),
                        DateTimeOffset.Parse("2026-04-12T12:00:00Z"),
                        null,
                        null,
                        null,
                        false,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        false,
                        true
                    ),
                ],
                bridgeStatus,
                "events-window-request",
                DateTimeOffset.Parse("2026-04-12T18:30:00Z")
            )
        );

        using var response = await client.GetAsync(
            "/api/events/window?start=2026-04-12T08:30:00Z&end=2026-04-12T12:30:00Z&limit=1"
        );
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        document
            .RootElement.GetProperty("items")[0]
            .GetProperty("bridgeId")
            .GetString()
            .Should()
            .Be("event-first-in-window");
        document
            .RootElement.GetProperty("items")[0]
            .GetProperty("subject")
            .GetString()
            .Should()
            .Be("First in window");
    }

    [TestMethod]
    public async Task Core_event_detail_should_return_cached_detail_with_unchanged_bridge_id()
    {
        var bridgeStatus = new BridgeStatusDto(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );
        using var factory = new CoreTestWebApplicationFactory(healthState =>
        {
            healthState.MarkDatabaseReady();
            healthState.MarkPollSuccess(bridgeStatus, DateTimeOffset.Parse("2026-04-12T18:35:00Z"));
        });
        using var client = factory.CreateClient();
        await factory.InitializeRepositoryAsync(repository =>
            repository.UpsertEventsAsync(
                [
                    new EventDto(
                        "event id+value",
                        "global-detail",
                        "Cached event detail",
                        DateTimeOffset.Parse("2026-04-13T09:00:00Z"),
                        DateTimeOffset.Parse("2026-04-13T10:00:00Z"),
                        "Room 204",
                        null,
                        null,
                        false,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        false,
                        true
                    ),
                ],
                bridgeStatus,
                "event-detail-request",
                DateTimeOffset.Parse("2026-04-12T18:35:00Z")
            )
        );

        using var response = await client.GetAsync("/api/events/event%20id%2Bvalue");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("bridgeId").GetString().Should().Be("event id+value");
        document.RootElement.GetProperty("subject").GetString().Should().Be("Cached event detail");
    }
}
