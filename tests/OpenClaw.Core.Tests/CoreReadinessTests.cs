using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

[TestClass]
public class CoreReadinessTests
{
    [TestMethod]
    public async Task Core_readiness_should_return_503_when_sqlite_cannot_initialize()
    {
        var readyBridge = new BridgeStatusDto(
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
            healthState.MarkDatabaseFailure("Simulated SQLite initialization failure.");
            healthState.MarkPollSuccess(readyBridge, DateTimeOffset.Parse("2026-04-12T17:00:00Z"));
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        document.RootElement.GetProperty("status").GetString().Should().Be("degraded");
        document.RootElement.GetProperty("sqliteReady").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("hostAdapterReachable").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("cacheStale").GetBoolean().Should().BeFalse();
    }

    [TestMethod]
    public async Task Core_readiness_should_return_503_on_hostadapter_outage_while_cached_reads_remain_available()
    {
        var degradedBridge = new BridgeStatusDto(
            BridgeState.degraded.ToString(),
            BridgeMode.safe.ToString(),
            false,
            true,
            "HostAdapter unavailable.",
            DateTimeOffset.Parse("2026-04-12T17:30:00Z"),
            DateTimeOffset.Parse("2026-04-12T17:35:00Z")
        );
        using var factory = new CoreTestWebApplicationFactory(healthState =>
        {
            healthState.MarkDatabaseReady();
            healthState.MarkPollFailure("HostAdapter unavailable.", degradedBridge);
        });
        using var client = factory.CreateClient();
        await factory.InitializeRepositoryAsync(async repository =>
        {
            var observedAtUtc = DateTimeOffset.Parse("2026-04-12T17:40:00Z");
            await repository.UpsertBridgeStatusSnapshotAsync(
                degradedBridge,
                "cached-request",
                observedAtUtc
            );
            await repository.UpsertMessagesAsync(
                [
                    new MessageDto(
                        "message-availability-1",
                        "mail",
                        "Cached subject",
                        observedAtUtc,
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
                    ),
                ],
                degradedBridge,
                "cached-request",
                observedAtUtc
            );
            await repository.UpsertEventsAsync(
                [
                    new EventDto(
                        "event-availability-1",
                        null,
                        "Cached event",
                        DateTimeOffset.Parse("2026-04-13T12:00:00Z"),
                        DateTimeOffset.Parse("2026-04-13T13:00:00Z"),
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
                degradedBridge,
                "cached-request",
                observedAtUtc
            );
        });

        using var readinessResponse = await client.GetAsync("/health/ready");
        var readinessPayload = await readinessResponse.Content.ReadAsStringAsync();
        using var readinessDocument = JsonDocument.Parse(readinessPayload);
        using var messagesResponse = await client.GetAsync("/api/messages/recent?kind=all&limit=5");
        var messagesPayload = await messagesResponse.Content.ReadAsStringAsync();
        using var messagesDocument = JsonDocument.Parse(messagesPayload);
        using var eventsResponse = await client.GetAsync(
            "/api/events/window?start=2026-04-13T00:00:00Z&end=2026-04-14T00:00:00Z&limit=5"
        );
        var eventsPayload = await eventsResponse.Content.ReadAsStringAsync();
        using var eventsDocument = JsonDocument.Parse(eventsPayload);

        readinessResponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        readinessDocument.RootElement.GetProperty("sqliteReady").GetBoolean().Should().BeTrue();
        readinessDocument
            .RootElement.GetProperty("hostAdapterReachable")
            .GetBoolean()
            .Should()
            .BeFalse();
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        messagesDocument.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        messagesDocument
            .RootElement.GetProperty("items")[0]
            .GetProperty("bridgeId")
            .GetString()
            .Should()
            .Be("message-availability-1");
        eventsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        eventsDocument.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        eventsDocument
            .RootElement.GetProperty("items")[0]
            .GetProperty("bridgeId")
            .GetString()
            .Should()
            .Be("event-availability-1");
    }
}
