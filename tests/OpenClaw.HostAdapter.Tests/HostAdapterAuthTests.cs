using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterAuthTests
{
    [TestMethod]
    public async Task HostAdapter_should_return_401_for_request_without_authorization_header_and_not_invoke_cli()
    {
        using var factory = new HostAdapterTestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/v1/status");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        document
            .RootElement.GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(BridgeErrorCodes.Unauthorized);
        document
            .RootElement.GetProperty("meta")
            .GetProperty("requestId")
            .GetString()
            .Should()
            .NotBeNullOrWhiteSpace();
        factory
            .ProcessRunner.InvocationCount.Should()
            .Be(
                0,
                "the bearer-token middleware should reject missing Authorization headers before any CLI bridge call is attempted."
            );
    }

    [TestMethod]
    public async Task HostAdapter_should_return_401_for_invalid_bearer_token_without_exposing_expected_token()
    {
        using var factory = new HostAdapterTestWebApplicationFactory();
        using var client = factory.CreateAuthorizedClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "invalid-token"
        );

        using var response = await client.GetAsync("/v1/status");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        document
            .RootElement.GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(BridgeErrorCodes.Unauthorized);
        payload.Should().NotContain(factory.ExpectedToken);
        factory
            .ProcessRunner.InvocationCount.Should()
            .Be(
                0,
                "the bearer-token middleware should reject invalid Authorization headers before any CLI bridge call is attempted."
            );
    }
}
