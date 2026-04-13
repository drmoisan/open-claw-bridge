using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterEnvelopeTests
{
    [TestMethod]
    public async Task HostAdapter_should_include_request_id_adapter_version_and_bridge_metadata_in_success_envelope()
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

        using var response = await client.GetAsync("/v1/status");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var meta = document.RootElement.GetProperty("meta");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        meta.GetProperty("requestId").GetString().Should().NotBeNullOrWhiteSpace();
        meta.GetProperty("adapterVersion").GetString().Should().Be("test-version");
        meta.GetProperty("bridge")
            .GetProperty("state")
            .GetString()
            .Should()
            .Be(BridgeState.ready.ToString());
    }
}
