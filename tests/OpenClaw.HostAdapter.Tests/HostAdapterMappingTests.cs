using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterMappingTests
{
    [TestMethod]
    public void HostAdapter_should_map_not_found_bridge_errors_to_404()
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
        var result = HostAdapterResponseMapper.MapFailure<MessageDto>(
            "message-request",
            "test-version",
            readyBridge,
            new RpcError(BridgeErrorCodes.NotFound, "The requested message could not be found."),
            string.Empty,
            1
        );

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        result.Envelope.Error.Should().NotBeNull();
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.NotFound);
    }

    [TestMethod]
    public void HostAdapter_should_map_outlook_unavailable_bridge_errors_to_503()
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
        var result = HostAdapterResponseMapper.MapFailure<MessageDto>(
            "message-request",
            "test-version",
            readyBridge,
            new RpcError(BridgeErrorCodes.OutlookUnavailable, "Outlook is currently unavailable."),
            string.Empty,
            4
        );

        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        result.Envelope.Error.Should().NotBeNull();
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.OutlookUnavailable);
    }

    [TestMethod]
    public async Task HostAdapter_should_return_200_for_degraded_cached_reads_and_mark_bridge_cache_as_stale()
    {
        using var factory = new HostAdapterTestWebApplicationFactory();
        var degradedBridge = new BridgeStatusDto(
            BridgeState.degraded.ToString(),
            BridgeMode.safe.ToString(),
            true,
            true,
            "cached-data-only",
            null,
            null
        );
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(
                degradedBridge,
                "status-request",
                "test-version",
                degradedBridge
            )
        );
        factory.ProcessRunner.EnqueueResponse(
            "list-messages",
            HostAdapterResponses.Success(
                new ItemsResponse<MessageDto>(Array.Empty<MessageDto>()),
                "messages-request",
                "test-version",
                degradedBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        using var response = await client.GetAsync(
            "/users/me/messages?$filter=receivedDateTime ge 2026-04-12T13:00:00Z&$top=1"
        );
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        document
            .RootElement.GetProperty("meta")
            .GetProperty("bridge")
            .GetProperty("cacheStale")
            .GetBoolean()
            .Should()
            .BeTrue();
    }

    [TestMethod]
    public async Task HostAdapter_should_dispatch_meeting_requests_branch_when_filter_contains_meeting_message_type_predicate()
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
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(readyBridge, "status-request", "test-version", readyBridge)
        );
        factory.ProcessRunner.EnqueueResponse(
            "list-meeting-requests",
            HostAdapterResponses.Success(
                new ItemsResponse<MessageDto>(Array.Empty<MessageDto>()),
                "meeting-requests-request",
                "test-version",
                readyBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        using var response = await client.GetAsync(
            "/users/me/messages?$filter=meetingMessageType ne null and receivedDateTime ge 2026-04-12T13:00:00Z&$top=5"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status", "list-meeting-requests");
    }

    [TestMethod]
    public async Task HostAdapter_should_dispatch_plain_messages_branch_when_filter_has_no_meeting_message_type_predicate()
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
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(readyBridge, "status-request", "test-version", readyBridge)
        );
        factory.ProcessRunner.EnqueueResponse(
            "list-messages",
            HostAdapterResponses.Success(
                new ItemsResponse<MessageDto>(Array.Empty<MessageDto>()),
                "messages-request",
                "test-version",
                readyBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        using var response = await client.GetAsync(
            "/users/me/messages?$filter=receivedDateTime ge 2026-04-12T13:00:00Z&$top=5"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status", "list-messages");
    }
}
