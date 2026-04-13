using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

[TestClass]
public class CoreMessagesApiTests
{
    [TestMethod]
    public async Task Core_messages_recent_should_enforce_kind_and_limit_filters()
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
            healthState.MarkPollSuccess(bridgeStatus, DateTimeOffset.Parse("2026-04-12T18:15:00Z"));
        });
        using var client = factory.CreateClient();
        await factory.InitializeRepositoryAsync(repository =>
            repository.UpsertMessagesAsync(
                [
                    new MessageDto(
                        "mail-older",
                        "mail",
                        "Older mail",
                        DateTimeOffset.Parse("2026-04-12T08:00:00Z"),
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
                    new MessageDto(
                        "meeting-1",
                        "meeting",
                        "Meeting request",
                        DateTimeOffset.Parse("2026-04-12T09:00:00Z"),
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
                    new MessageDto(
                        "mail-newer",
                        "mail",
                        "Newest mail",
                        DateTimeOffset.Parse("2026-04-12T10:00:00Z"),
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
                bridgeStatus,
                "messages-request",
                DateTimeOffset.Parse("2026-04-12T18:15:00Z")
            )
        );

        using var response = await client.GetAsync("/api/messages/recent?kind=mail&limit=1");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        document
            .RootElement.GetProperty("items")[0]
            .GetProperty("bridgeId")
            .GetString()
            .Should()
            .Be("mail-newer");
        document
            .RootElement.GetProperty("items")[0]
            .GetProperty("itemKind")
            .GetString()
            .Should()
            .Be("mail");
    }

    [TestMethod]
    public async Task Core_message_detail_should_return_cached_detail_with_unchanged_bridge_id()
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
            healthState.MarkPollSuccess(bridgeStatus, DateTimeOffset.Parse("2026-04-12T18:20:00Z"));
        });
        using var client = factory.CreateClient();
        await factory.InitializeRepositoryAsync(repository =>
            repository.UpsertMessagesAsync(
                [
                    new MessageDto(
                        "message id+value",
                        "mail",
                        "Cached detail",
                        DateTimeOffset.Parse("2026-04-12T11:00:00Z"),
                        null,
                        null,
                        null,
                        false,
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
                "message-detail-request",
                DateTimeOffset.Parse("2026-04-12T18:20:00Z")
            )
        );

        using var response = await client.GetAsync("/api/messages/message%20id%2Bvalue");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("bridgeId").GetString().Should().Be("message id+value");
        document.RootElement.GetProperty("subject").GetString().Should().Be("Cached detail");
    }
}
