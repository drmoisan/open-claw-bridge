using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Tests for the D2 status substitute: the probe request shape
/// (<c>/users/{p}/mailboxSettings?$select=timeZone</c>), the synthesized
/// <c>ready</c>/<c>graph</c> snapshot on probe success, and failure envelopes (404
/// and 503-after-exhaustion) with no fabricated healthy status.
/// </summary>
[TestClass]
public sealed class GraphHostAdapterClientStatusTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

    private static GraphHostAdapterClient Client(
        FakeHttpHandler handler,
        FakeTimeProvider? timeProvider = null,
        int maxAttempts = 4
    )
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-status", Start.AddHours(1)));

        return new GraphHostAdapterClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(
                new GraphAdapterOptions
                {
                    Enabled = true,
                    PrincipalMailboxUpn = "paula@contoso.com",
                    AssistantMailboxUpn = "amy@contoso.com",
                    MaxAttempts = maxAttempts,
                }
            ),
            tokenProvider.Object,
            timeProvider ?? new FakeTimeProvider(Start),
            NullLogger<GraphHostAdapterClient>.Instance
        );
    }

    [TestMethod]
    public async Task GetStatus_ProbesMailboxSettingsTimeZone()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "timeZone": "UTC" }"""),
                }
            );
        });
        var client = Client(handler);

        var result = await client.GetStatusAsync(requestId: "req-status");

        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be("/v1.0/users/paula%40contoso.com/mailboxSettings");
        captured.RequestUri.Query.Should().Be("?$select=timeZone", "the cheapest stable read");
        captured.Headers.Authorization!.ToString().Should().Be("Bearer tok-status");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-status");
    }

    [TestMethod]
    public async Task GetStatus_ProbeSuccess_SynthesizesReadyGraphSnapshot()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "timeZone": "UTC" }"""),
                }
            )
        );
        var client = Client(handler);

        var result = await client.GetStatusAsync(requestId: "req-snap");

        result.Ok.Should().BeTrue();
        result.Data!.State.Should().Be("ready");
        result.Data.Mode.Should().Be("graph");
        result
            .Data.OutlookConnected.Should()
            .BeTrue("OutlookConnected is interpreted as backend reachable");
        result.Data.CacheStale.Should().BeFalse("Graph reads are live; there is no cache");
        result.Data.StaleReason.Should().BeNull();
        result.Data.LastInboxScanUtc.Should().BeNull("there are no scan timestamps");
        result.Data.LastCalendarScanUtc.Should().BeNull();
        result.Meta.RequestId.Should().Be("req-snap");
        result.Meta.AdapterVersion.Should().Be("cloudgraph");
    }

    [TestMethod]
    public async Task GetStatus_ProbeNotFound_ReturnsFailureWithNoFabricatedStatus()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(GraphPayloadFixtures.ErrorItemNotFoundBody),
                }
            )
        );
        var client = Client(handler);

        var result = await client.GetStatusAsync(requestId: "req-404");

        result.Ok.Should().BeFalse("a failed probe must not report a healthy status");
        result.Data.Should().BeNull("no fabricated status snapshot");
        result.Error!.Code.Should().Be("NOT_FOUND");
        result.Error.BridgeErrorCode.Should().Be("ErrorItemNotFound");
    }

    [TestMethod]
    public async Task GetStatus_ProbeUnavailableAfterExhaustion_ReturnsRetryableFailure()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attempts = 0;
        var handler = new FakeHttpHandler(_ =>
        {
            attempts++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{}"),
                }
            );
        });
        var client = Client(handler, timeProvider, maxAttempts: 2);

        var task = client.GetStatusAsync(requestId: "req-503");
        var safety = 0;
        while (!task.IsCompleted)
        {
            if (++safety > 10_000)
            {
                throw new AssertFailedException("The probe did not complete under fake time.");
            }

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        var result = await task;

        result.Ok.Should().BeFalse();
        result.Data.Should().BeNull("no fabricated status snapshot");
        result.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Error.Retryable.Should().BeTrue();
        attempts.Should().Be(2, "the probe goes through the shared retry pipeline");
    }
}
