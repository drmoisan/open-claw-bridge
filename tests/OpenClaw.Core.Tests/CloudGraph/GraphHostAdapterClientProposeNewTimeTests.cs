using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Handler-level contract tests for member 11 (<c>ProposeNewMeetingTimeAsync</c>, issue
/// #130): the attendee propose-new-time <c>POST /users/{p}/events/{id}/tentativelyAccept</c>.
/// Covers method/route (a), headers (b, bearer/<c>client-request-id</c>/content-type/no
/// <c>Prefer</c>), the exact two-property body with the absent-property guardrail (c),
/// 202-empty-body -&gt; <c>ok: true, data: null</c> (d), the D5 terminal error samples (e),
/// and 429 retry exhaustion under <see cref="FakeTimeProvider"/> (f).
/// </summary>
[TestClass]
public sealed class GraphHostAdapterClientProposeNewTimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ProposedStart = new(2026, 7, 9, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ProposedEnd = new(2026, 7, 9, 14, 30, 0, TimeSpan.Zero);

    private static readonly string AccessDeniedBody = """
        { "error": { "code": "ErrorAccessDenied", "message": "Access is denied." } }
        """;

    private static GraphHostAdapterClient Client(
        FakeHttpHandler handler,
        string principal = "paula@contoso.com",
        FakeTimeProvider? timeProvider = null,
        int maxAttempts = 4,
        ILogger<GraphHostAdapterClient>? logger = null
    )
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-propose", Start.AddHours(1)));

        var options = new GraphAdapterOptions
        {
            Enabled = true,
            PrincipalMailboxUpn = principal,
            AssistantMailboxUpn = "amy@contoso.com",
            MaxAttempts = maxAttempts,
        };

        return new GraphHostAdapterClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(options),
            tokenProvider.Object,
            timeProvider ?? new FakeTimeProvider(Start),
            logger ?? NullLogger<GraphHostAdapterClient>.Instance
        );
    }

    private static HttpResponseMessage Accepted() => new(HttpStatusCode.Accepted);

    // ---- (a) method + URL-escaped principal/event route with /tentativelyAccept suffix ----

    [TestMethod]
    public async Task ProposeNewMeetingTime_PostsToTheEscapedPrincipalEventTentativelyAcceptRoute()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Accepted());
        });
        var client = Client(handler);

        var result = await client.ProposeNewMeetingTimeAsync(
            "evt-1",
            ProposedStart,
            ProposedEnd,
            requestId: "req-a"
        );

        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be(
                "/v1.0/users/paula%40contoso.com/events/evt-1/tentativelyAccept",
                "the POST targets the principal's own invited event's meeting-response route"
            );
    }

    // ---- (b) headers ----

    [TestMethod]
    public async Task ProposeNewMeetingTime_SendsBearerRequestIdAndJsonWithoutPrefer()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Accepted());
        });
        var client = Client(handler);

        await client.ProposeNewMeetingTimeAsync(
            "evt-1",
            ProposedStart,
            ProposedEnd,
            requestId: "req-b"
        );

        captured!.Headers.Authorization!.ToString().Should().Be("Bearer tok-propose");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-b");
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        captured
            .Headers.Contains("Prefer")
            .Should()
            .BeFalse("the propose-new-time path sends no Prefer headers");
    }

    // ---- (c) exact body shape + absent-property guardrail ----

    [TestMethod]
    public async Task ProposeNewMeetingTime_BodyCarriesExactlySendResponseAndProposedNewTime()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Accepted();
        });
        var client = Client(handler);

        await client.ProposeNewMeetingTimeAsync(
            "evt-1",
            ProposedStart,
            ProposedEnd,
            requestId: "req-c"
        );

        using var body = JsonDocument.Parse(capturedBody!);
        var root = body.RootElement;

        var topLevelNames = new List<string>();
        foreach (var property in root.EnumerateObject())
        {
            topLevelNames.Add(property.Name);
        }

        topLevelNames
            .Should()
            .BeEquivalentTo(
                new[] { "sendResponse", "proposedNewTime" },
                "the body has exactly sendResponse and proposedNewTime and no other top-level property"
            );

        root.GetProperty("sendResponse")
            .GetBoolean()
            .Should()
            .BeTrue("sendResponse is hardcoded true");

        var proposed = root.GetProperty("proposedNewTime");
        var proposedNames = new List<string>();
        foreach (var property in proposed.EnumerateObject())
        {
            proposedNames.Add(property.Name);
        }

        proposedNames
            .Should()
            .BeEquivalentTo(
                new[] { "start", "end" },
                "proposedNewTime carries exactly the start and end pairs"
            );

        var start = proposed.GetProperty("start");
        start.GetProperty("dateTime").GetString().Should().Be("2026-07-09T14:00:00");
        start.GetProperty("timeZone").GetString().Should().Be("UTC");
        var end = proposed.GetProperty("end");
        end.GetProperty("dateTime").GetString().Should().Be("2026-07-09T14:30:00");
        end.GetProperty("timeZone").GetString().Should().Be("UTC");

        // Absent-property guardrail: a proposal structurally cannot rewrite the event.
        root.TryGetProperty("comment", out _).Should().BeFalse("no comment is sent");
        root.TryGetProperty("start", out _).Should().BeFalse();
        root.TryGetProperty("end", out _).Should().BeFalse();
        root.TryGetProperty("body", out _).Should().BeFalse();
        root.TryGetProperty("subject", out _).Should().BeFalse();
        root.TryGetProperty("attendees", out _).Should().BeFalse();
    }

    [TestMethod]
    public async Task ProposeNewMeetingTime_EscapesTheEventIdInTheRoute()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Accepted());
        });
        var client = Client(handler);

        await client.ProposeNewMeetingTimeAsync(
            "evt/1 a",
            ProposedStart,
            ProposedEnd,
            requestId: "req-esc"
        );

        captured!
            .RequestUri!.AbsolutePath.Should()
            .Be(
                "/v1.0/users/paula%40contoso.com/events/evt%2F1%20a/tentativelyAccept",
                "the event id is URL-escaped in the route segment"
            );
    }

    // ---- (d) 202 empty body -> ok: true, data: null ----

    [TestMethod]
    public async Task ProposeNewMeetingTime_Accepted202EmptyBody_YieldsOkTrueDataNull()
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(Accepted()));
        var client = Client(handler);

        var result = await client.ProposeNewMeetingTimeAsync(
            "evt-1",
            ProposedStart,
            ProposedEnd,
            requestId: "req-202"
        );

        result.Ok.Should().BeTrue("202 Accepted with no body is the success contract");
        result.Data.Should().BeNull("no data is fabricated for an empty-body response");
        result.Error.Should().BeNull();
        result.Meta.RequestId.Should().Be("req-202");
        result.Meta.AdapterVersion.Should().Be("cloudgraph");
    }

    // ---- (e) D5 terminal error samples ----

    [DataTestMethod]
    [DataRow(HttpStatusCode.BadRequest, "INVALID_REQUEST", DisplayName = "400 -> INVALID_REQUEST")]
    [DataRow(HttpStatusCode.NotFound, "NOT_FOUND", DisplayName = "404 -> NOT_FOUND")]
    public async Task ProposeNewMeetingTime_TerminalStatus_MapsPerTheD5Matrix(
        HttpStatusCode status,
        string expectedCode
    )
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") })
        );
        var client = Client(handler);

        var result = await client.ProposeNewMeetingTimeAsync(
            "evt-1",
            ProposedStart,
            ProposedEnd,
            requestId: "req-e"
        );

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
        result.Error.Retryable.Should().BeFalse();
    }

    [TestMethod]
    public async Task ProposeNewMeetingTime_Forbidden_MapsToUnauthorizedWithGraphCodePassthrough()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent(AccessDeniedBody),
                }
            )
        );
        var client = Client(handler);

        var result = await client.ProposeNewMeetingTimeAsync(
            "evt-1",
            ProposedStart,
            ProposedEnd,
            requestId: "req-403"
        );

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("UNAUTHORIZED");
        result.Error.Retryable.Should().BeFalse();
        result
            .Error.BridgeErrorCode.Should()
            .Be("ErrorAccessDenied", "the Graph error.code passes through to BridgeErrorCode");
    }

    // ---- (f) 429 retry exhaustion under FakeTimeProvider ----

    [TestMethod]
    public async Task ProposeNewMeetingTime_ThrottledExhaustion_MapsToThrottledWithRetryAfter()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attempts = 0;
        var handler = new FakeHttpHandler(_ =>
        {
            attempts++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(GraphPayloadFixtures.TooManyRequestsBody),
            };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                TimeSpan.FromSeconds(2)
            );
            return Task.FromResult(response);
        });
        var client = Client(handler, timeProvider: timeProvider, maxAttempts: 3);

        var task = client.ProposeNewMeetingTimeAsync(
            "evt-1",
            ProposedStart,
            ProposedEnd,
            requestId: "req-429"
        );
        var safety = 0;
        while (!task.IsCompleted)
        {
            if (++safety > 10_000)
            {
                throw new AssertFailedException("The proposal did not complete under fake time.");
            }

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        var result = await task;

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("THROTTLED");
        result.Error.Retryable.Should().BeTrue();
        result.Error.BridgeErrorCode.Should().Be("TooManyRequests");
        attempts.Should().Be(3, "the Retry-After precedence drives all attempts to exhaustion");
    }
}
