using System.Net;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterStatusCacheTests
{
    [TestMethod]
    public async Task HostAdapter_should_reuse_one_status_lookup_for_consecutive_data_requests_within_cache_ttl()
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
                "messages-request-1",
                "test-version",
                readyBridge
            )
        );
        factory.ProcessRunner.EnqueueResponse(
            "list-messages",
            HostAdapterResponses.Success(
                new ItemsResponse<MessageDto>(Array.Empty<MessageDto>()),
                "messages-request-2",
                "test-version",
                readyBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        using var firstResponse = await client.GetAsync(
            "/users/me/messages?$filter=receivedDateTime ge 2026-04-12T13:00:00Z&$top=1"
        );
        using var secondResponse = await client.GetAsync(
            "/users/me/messages?$filter=receivedDateTime ge 2026-04-12T13:05:00Z&$top=1"
        );

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status", "list-messages", "list-messages");
    }
}
