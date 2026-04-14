using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

[TestClass]
public class CoreStatusTests
{
    [TestMethod]
    public async Task Core_status_should_report_last_successful_poll_time_bridge_freshness_and_stale_cache_state()
    {
        var bridgeStatus = new BridgeStatusDto(
            BridgeState.degraded.ToString(),
            BridgeMode.safe.ToString(),
            false,
            true,
            "Bridge cache is stale.",
            DateTimeOffset.Parse("2026-04-12T17:45:00Z"),
            DateTimeOffset.Parse("2026-04-12T17:50:00Z")
        );
        var observedAtUtc = DateTimeOffset.Parse("2026-04-12T18:00:00Z");
        using var factory = new CoreTestWebApplicationFactory(healthState =>
        {
            healthState.MarkDatabaseReady();
            healthState.MarkPollSuccess(bridgeStatus, observedAtUtc);
        });
        using var client = factory.CreateClient();
        await factory.InitializeRepositoryAsync(async repository =>
        {
            await repository.UpsertBridgeStatusSnapshotAsync(
                bridgeStatus,
                "status-request",
                observedAtUtc
            );
            await repository.AddIngestRunAsync(
                "calendar_window",
                "success",
                "status-request",
                observedAtUtc.AddMinutes(-5),
                observedAtUtc,
                null
            );
        });

        using var response = await client.GetAsync("/api/status");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var lastSuccessfulPollUtc = DateTimeOffset.Parse(
            document.RootElement.GetProperty("lastSuccessfulPollUtc").GetString()!
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        lastSuccessfulPollUtc.Should().Be(observedAtUtc);
        document
            .RootElement.GetProperty("bridgeFreshness")
            .GetProperty("cacheStale")
            .GetBoolean()
            .Should()
            .BeTrue();
        document
            .RootElement.GetProperty("bridgeFreshness")
            .GetProperty("staleReason")
            .GetString()
            .Should()
            .Be("Bridge cache is stale.");
        document
            .RootElement.GetProperty("bridge")
            .GetProperty("cacheStale")
            .GetBoolean()
            .Should()
            .BeTrue();
    }
}
