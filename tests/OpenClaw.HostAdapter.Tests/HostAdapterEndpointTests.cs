using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterEndpointTests
{
    [TestMethod]
    public void HostAdapter_should_deserialize_list_payloads_from_bridge_items_objects()
    {
        var element = JsonSerializer.SerializeToElement(
            new
            {
                items = new[]
                {
                    new MessageDto(
                        "msg-1",
                        "mail",
                        "Subject",
                        DateTimeOffset.Parse("2026-04-12T13:00:00Z"),
                        DateTimeOffset.Parse("2026-04-12T12:55:00Z"),
                        1,
                        0,
                        true,
                        false,
                        "IPM.Note",
                        null,
                        null,
                        null,
                        null,
                        null,
                        true,
                        true
                    ),
                },
            }
        );

        var response = Program.DeserializeItemsResponse<MessageDto>(element);

        response
            .Should()
            .BeEquivalentTo(
                new ItemsResponse<MessageDto>([
                    new MessageDto(
                        "msg-1",
                        "mail",
                        "Subject",
                        DateTimeOffset.Parse("2026-04-12T13:00:00Z"),
                        DateTimeOffset.Parse("2026-04-12T12:55:00Z"),
                        1,
                        0,
                        true,
                        false,
                        "IPM.Note",
                        null,
                        null,
                        null,
                        null,
                        null,
                        true,
                        true
                    ),
                ])
            );
    }

    [TestMethod]
    public async Task HostAdapter_should_pass_url_decoded_bridge_id_through_unchanged_for_message_and_event_detail_routes()
    {
        using var factory = new HostAdapterTestWebApplicationFactory();
        var readyBridge = new BridgeStatusDto(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );
        var expectedBridgeId = "bridge id+value";
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(readyBridge, "status-request", "test-version", readyBridge)
        );
        factory.ProcessRunner.EnqueueResponse(
            "get-message",
            HostAdapterResponses.Success(
                new MessageDto(
                    expectedBridgeId,
                    "message",
                    "Subject",
                    null,
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
                "message-request",
                "test-version",
                readyBridge
            )
        );
        factory.ProcessRunner.EnqueueResponse(
            "get-event",
            HostAdapterResponses.Success(
                new EventDto(
                    expectedBridgeId,
                    null,
                    "Event",
                    DateTimeOffset.Parse("2026-04-12T13:00:00Z"),
                    DateTimeOffset.Parse("2026-04-12T14:00:00Z"),
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
                "event-request",
                "test-version",
                readyBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        using var messageResponse = await client.GetAsync("/users/me/messages/bridge%20id%2Bvalue");
        using var eventResponse = await client.GetAsync("/users/me/events/bridge%20id%2Bvalue");

        messageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        eventResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status", "get-message", "get-event");
        factory
            .ProcessRunner.Invocations[1]
            .Arguments.Should()
            .ContainInOrder("get-message", "--id", expectedBridgeId);
        factory
            .ProcessRunner.Invocations[2]
            .Arguments.Should()
            .ContainInOrder("get-event", "--id", expectedBridgeId);
    }
}
