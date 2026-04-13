using System.Net;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

[TestClass]
public class CoreUiTests
{
    [TestMethod]
    public async Task Core_ui_should_surface_freshness_and_redaction_badges_without_message_body_or_attendee_leakage()
    {
        var bridgeStatus = new BridgeStatusDto(
            BridgeState.degraded.ToString(),
            BridgeMode.safe.ToString(),
            false,
            true,
            "Bridge cache is stale.",
            DateTimeOffset.Parse("2026-04-12T18:40:00Z"),
            DateTimeOffset.Parse("2026-04-12T18:45:00Z")
        );
        using var factory = new CoreTestWebApplicationFactory(healthState =>
        {
            healthState.MarkDatabaseReady();
            healthState.MarkPollFailure("Bridge cache is stale.", bridgeStatus);
        });
        using var client = factory.CreateClient();
        await factory.InitializeRepositoryAsync(async repository =>
        {
            var observedAtUtc = DateTimeOffset.Parse("2026-04-12T18:50:00Z");
            await repository.UpsertMessagesAsync(
                [
                    new MessageDto(
                        "mail-ui-visible",
                        "mail",
                        "Visible mail",
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
                        "do-not-render-this-body-preview",
                        true,
                        false
                    ),
                    new MessageDto(
                        "meeting-ui-redacted",
                        "meeting",
                        "Redacted meeting",
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
                        "still-do-not-render-body-preview",
                        false,
                        true
                    ),
                ],
                bridgeStatus,
                "ui-request",
                observedAtUtc
            );
            await repository.UpsertEventsAsync(
                [
                    new EventDto(
                        "event-ui-redacted",
                        null,
                        "Redacted event",
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.AddHours(1),
                        null,
                        null,
                        null,
                        false,
                        null,
                        null,
                        "secret-required-attendee@example.com",
                        "secret-optional-attendee@example.com",
                        null,
                        null,
                        false,
                        true
                    ),
                ],
                bridgeStatus,
                "ui-request",
                observedAtUtc
            );
        });

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Stale Cache");
        html.Should().Contain("Visible");
        html.Should().Contain("Redacted");
        html.Should().Contain("Visible mail");
        html.Should().Contain("Redacted meeting");
        html.Should().Contain("Redacted event");
        html.Should().NotContain("do-not-render-this-body-preview");
        html.Should().NotContain("still-do-not-render-body-preview");
        html.Should().NotContain("secret-required-attendee@example.com");
        html.Should().NotContain("secret-optional-attendee@example.com");
    }
}
