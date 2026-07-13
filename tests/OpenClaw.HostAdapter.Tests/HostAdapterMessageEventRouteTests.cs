using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

/// <summary>
/// Tests for the message-to-event linkage route <c>GET /users/{id}/messages/{messageId}/event</c>
/// (issue #146), the <see cref="HostAdapterCommandBuilder.BuildGetEventForMessage"/> command, and the
/// null-tolerant <see cref="HostAdapterEventProjector.ProjectNullableEvent"/>. Confirms the
/// graceful-degradation contract: an ok/JSON-null RPC result maps to <c>ok:true</c> / <c>data:null</c>
/// / HTTP 200 (not a 502); an ok/event result maps to <c>data:event</c> / 200; a downstream
/// INVALID_REQUEST maps to HTTP 400; and bridge-not-ready maps to HTTP 409. A fake
/// <see cref="IHostAdapterProcessRunner"/> is used so no child process is spawned.
/// </summary>
[TestClass]
public sealed class HostAdapterMessageEventRouteTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static BridgeStatusDto ReadyBridge() =>
        new(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );

    private static BridgeStatusDto StartingBridge() =>
        new(
            BridgeState.starting.ToString(),
            BridgeMode.safe.ToString(),
            false,
            false,
            null,
            null,
            null
        );

    private static void EnqueueStatus(
        HostAdapterTestWebApplicationFactory factory,
        BridgeStatusDto bridge
    ) =>
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(bridge, "status-request", "test-version", bridge)
        );

    private static EventDto SampleEvent() =>
        new(
            "evt:linked",
            "gaid",
            "Linked meeting",
            DateTimeOffset.Parse("2026-06-15T09:00:00Z"),
            DateTimeOffset.Parse("2026-06-15T10:00:00Z"),
            "Room",
            2,
            1,
            false,
            0,
            "org@contoso.com",
            null,
            null,
            null,
            null,
            false,
            false
        );

    private static async Task<ApiEnvelope<EventDto>> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<EventDto>>(Json);
        body.Should().NotBeNull();
        return body!;
    }

    [TestMethod]
    public void BuildGetEventForMessage_should_produce_the_verb_with_the_id_option()
    {
        // Arrange
        var options = new HostAdapterOptions { ClientExecutablePath = "client.exe" };
        var builder = new HostAdapterCommandBuilder(Options.Create(options));

        // Act
        var startInfo = builder.BuildGetEventForMessage("mtg:abc");
        var args = startInfo.ArgumentList.ToArray();

        // Assert
        args[0].Should().Be("get-event-for-message");
        args.Should().ContainInOrder("get-event-for-message", "--id", "mtg:abc");
    }

    [TestMethod]
    public void ProjectNullableEvent_should_return_null_for_a_json_null_element()
    {
        // Arrange
        var nullElement = JsonSerializer.SerializeToElement<object?>(null, Json);

        // Act
        var result = HostAdapterEventProjector.ProjectNullableEvent(nullElement);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void ProjectNullableEvent_should_deserialize_an_object_element_into_an_event()
    {
        // Arrange
        var element = JsonSerializer.SerializeToElement(SampleEvent(), Json);

        // Act
        var result = HostAdapterEventProjector.ProjectNullableEvent(element);

        // Assert
        result.Should().NotBeNull();
        result!.BridgeId.Should().Be("evt:linked");
    }

    [TestMethod]
    public async Task Route_should_return_200_with_data_null_for_an_unlinked_message()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        var bridge = ReadyBridge();
        EnqueueStatus(factory, bridge);
        factory.ProcessRunner.EnqueueResponse(
            "get-event-for-message",
            HostAdapterResponses.Success<EventDto?>(null, "linkage-request", "test-version", bridge)
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync("/users/me/messages/mtg%3Aabc/event");
        var envelope = await ReadEnvelopeAsync(response);

        // Assert: clean not-linked degradation, not a 502.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Ok.Should().BeTrue();
        envelope.Data.Should().BeNull();
        envelope.Error.Should().BeNull();
    }

    [TestMethod]
    public async Task Route_should_return_200_with_event_for_a_linked_message()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        var bridge = ReadyBridge();
        EnqueueStatus(factory, bridge);
        factory.ProcessRunner.EnqueueResponse(
            "get-event-for-message",
            HostAdapterResponses.Success<EventDto?>(
                SampleEvent(),
                "linkage-request",
                "test-version",
                bridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync("/users/me/messages/mtg%3Aabc/event");
        var envelope = await ReadEnvelopeAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Ok.Should().BeTrue();
        envelope.Data.Should().NotBeNull();
        envelope.Data!.BridgeId.Should().Be("evt:linked");
        factory
            .ProcessRunner.Invocations[1]
            .Arguments.Should()
            .ContainInOrder("get-event-for-message", "--id", "mtg:abc");
    }

    [TestMethod]
    public async Task Route_should_map_downstream_invalid_request_to_400()
    {
        // Arrange: the RPC layer rejects a malformed message bridge id with INVALID_REQUEST, which
        // the process runner maps to a 400 envelope; the route surfaces it unchanged.
        using var factory = new HostAdapterTestWebApplicationFactory();
        var bridge = ReadyBridge();
        EnqueueStatus(factory, bridge);
        factory.ProcessRunner.EnqueueResponse(
            "get-event-for-message",
            HostAdapterResponses.InvalidRequest<EventDto?>(
                "linkage-request",
                "test-version",
                "The supplied message bridge ID is invalid.",
                bridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync("/users/me/messages/not-a-valid-id/event");
        var envelope = await ReadEnvelopeAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        envelope.Ok.Should().BeFalse();
        envelope.Error.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Route_should_return_409_when_the_bridge_is_not_ready()
    {
        // Arrange: a starting bridge is not ready; the route returns 409 before invoking the CLI.
        using var factory = new HostAdapterTestWebApplicationFactory();
        EnqueueStatus(factory, StartingBridge());
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync("/users/me/messages/mtg%3Aabc/event");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        // The linkage CLI verb must not have been invoked once the not-ready gate fired.
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .NotContain("get-event-for-message");
    }
}
