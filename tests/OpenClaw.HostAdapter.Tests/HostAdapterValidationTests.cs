using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterValidationTests
{
    [TestMethod]
    public async Task HostAdapter_should_return_400_invalid_request_for_non_utc_since_query()
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
        using var client = factory.CreateAuthorizedClient();

        using var response = await client.GetAsync(
            "/users/me/messages?$filter=receivedDateTime ge 2026-04-12T09:15:00-04:00"
        );
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        document
            .RootElement.GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(BridgeErrorCodes.InvalidRequest);
        document
            .RootElement.GetProperty("error")
            .GetProperty("message")
            .GetString()
            .Should()
            .Contain("receivedDateTime")
            .And.Contain("UTC");
        document
            .RootElement.GetProperty("meta")
            .GetProperty("requestId")
            .GetString()
            .Should()
            .NotBeNullOrWhiteSpace();
        factory.ProcessRunner.InvocationCount.Should().Be(1);
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status");
    }

    [TestMethod]
    public async Task HostAdapter_should_return_400_invalid_request_when_calendar_end_is_not_later_than_start()
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
        using var client = factory.CreateAuthorizedClient();

        using var response = await client.GetAsync(
            "/users/me/calendarView?startDateTime=2026-04-12T13:00:00Z&endDateTime=2026-04-12T13:00:00Z"
        );
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        document
            .RootElement.GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(BridgeErrorCodes.InvalidRequest);
        document
            .RootElement.GetProperty("error")
            .GetProperty("message")
            .GetString()
            .Should()
            .Contain("endDateTime")
            .And.Contain("startDateTime");
        document
            .RootElement.GetProperty("meta")
            .GetProperty("requestId")
            .GetString()
            .Should()
            .NotBeNullOrWhiteSpace();
        factory.ProcessRunner.InvocationCount.Should().Be(1);
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status");
    }

    [TestMethod]
    public async Task HostAdapter_should_apply_default_limit_and_reject_limit_values_above_maximum()
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
            HostAdapterResponses.Success(
                readyBridge,
                "status-request-1",
                "test-version",
                readyBridge
            )
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
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(
                readyBridge,
                "status-request-2",
                "test-version",
                readyBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        using var defaultLimitResponse = await client.GetAsync(
            "/users/me/messages?$filter=receivedDateTime ge 2026-04-12T13:00:00Z"
        );
        using var overMaxResponse = await client.GetAsync(
            "/users/me/messages?$filter=receivedDateTime ge 2026-04-12T13:00:00Z&$top=251"
        );

        var overMaxPayload = await overMaxResponse.Content.ReadAsStringAsync();
        using var overMaxDocument = JsonDocument.Parse(overMaxPayload);

        defaultLimitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        overMaxResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        overMaxDocument
            .RootElement.GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(BridgeErrorCodes.InvalidRequest);
        overMaxDocument
            .RootElement.GetProperty("meta")
            .GetProperty("requestId")
            .GetString()
            .Should()
            .NotBeNullOrWhiteSpace();

        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status", "list-messages");
        factory
            .ProcessRunner.Invocations[1]
            .Arguments.Should()
            .ContainInOrder(
                "list-messages",
                "--since",
                "2026-04-12T13:00:00.0000000+00:00",
                "--limit",
                "100"
            );
        factory
            .ProcessRunner.InvocationCount.Should()
            .Be(
                2,
                "the over-max request should be rejected at the adapter boundary without triggering another CLI command."
            );
    }
}
