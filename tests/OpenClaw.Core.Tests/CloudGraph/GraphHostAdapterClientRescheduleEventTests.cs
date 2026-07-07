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
/// Handler-level contract tests for member 10 (<c>UpdateEventTimesAsync</c>, issue #128):
/// the organizer-reschedule <c>PATCH /users/{p}/events/{id}</c>. Covers method/route,
/// headers (bearer, <c>client-request-id</c>, content-type, no <c>Prefer</c>), the exact
/// two-property body with the absent-property guardrail, 200 -&gt; <see cref="EventDto"/>
/// mapping, and the D5 error/parse-failure samples (400/403/404, 429 exhaustion under
/// <see cref="FakeTimeProvider"/>, unparseable/mapping-gap bodies).
/// </summary>
[TestClass]
public sealed class GraphHostAdapterClientRescheduleEventTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NewStart = new(2026, 7, 9, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NewEnd = new(2026, 7, 9, 14, 30, 0, TimeSpan.Zero);

    /// <summary>200 response whose start/end differ from the request, to prove the mapper reads the payload.</summary>
    private const string UpdatedEventBody = """
        {
          "id": "evt-1",
          "subject": "Weekly 1:1",
          "isOrganizer": true,
          "type": "occurrence",
          "start": { "dateTime": "2026-07-09T14:00:00.0000000", "timeZone": "UTC" },
          "end": { "dateTime": "2026-07-09T14:30:00.0000000", "timeZone": "UTC" }
        }
        """;

    /// <summary>A 2xx body missing the required <c>end</c> field (GraphMappingException path).</summary>
    private const string EventMissingEndBody = """
        {
          "id": "evt-1",
          "start": { "dateTime": "2026-07-09T14:00:00.0000000", "timeZone": "UTC" }
        }
        """;

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
            .ReturnsAsync(new AppAccessToken("tok-resched", Start.AddHours(1)));

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

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    // ---- (a) method + URL-escaped principal route ----

    [TestMethod]
    public async Task UpdateEventTimes_PatchesTheEscapedPrincipalEventRoute()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Ok(UpdatedEventBody));
        });
        var client = Client(handler);

        var result = await client.UpdateEventTimesAsync(
            "evt-1",
            NewStart,
            NewEnd,
            requestId: "req-a"
        );

        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Patch);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be(
                "/v1.0/users/paula%40contoso.com/events/evt-1",
                "the PATCH targets the principal's own calendar event"
            );
    }

    // ---- (b) headers ----

    [TestMethod]
    public async Task UpdateEventTimes_SendsBearerRequestIdAndJsonWithoutPrefer()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Ok(UpdatedEventBody));
        });
        var client = Client(handler);

        await client.UpdateEventTimesAsync("evt-1", NewStart, NewEnd, requestId: "req-b");

        captured!.Headers.Authorization!.ToString().Should().Be("Bearer tok-resched");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-b");
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        captured
            .Headers.Contains("Prefer")
            .Should()
            .BeFalse("the write path sends no Prefer headers");
    }

    // ---- (c) exact body shape + absent-property guardrail ----

    [TestMethod]
    public async Task UpdateEventTimes_BodyCarriesExactlyStartAndEndUtcPairs()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Ok(UpdatedEventBody);
        });
        var client = Client(handler);

        await client.UpdateEventTimesAsync("evt-1", NewStart, NewEnd, requestId: "req-c");

        using var body = JsonDocument.Parse(capturedBody!);
        var root = body.RootElement;

        var propertyNames = new List<string>();
        foreach (var property in root.EnumerateObject())
        {
            propertyNames.Add(property.Name);
        }

        propertyNames
            .Should()
            .BeEquivalentTo(
                new[] { "start", "end" },
                "the body has exactly the start and end pairs and no other top-level property"
            );

        var start = root.GetProperty("start");
        start.GetProperty("dateTime").GetString().Should().Be("2026-07-09T14:00:00");
        start.GetProperty("timeZone").GetString().Should().Be("UTC");
        var end = root.GetProperty("end");
        end.GetProperty("dateTime").GetString().Should().Be("2026-07-09T14:30:00");
        end.GetProperty("timeZone").GetString().Should().Be("UTC");

        // Absent-property guardrail (online-meeting-blob protection, master §11.1).
        root.TryGetProperty("body", out _).Should().BeFalse();
        root.TryGetProperty("subject", out _).Should().BeFalse();
        root.TryGetProperty("location", out _).Should().BeFalse();
        root.TryGetProperty("attendees", out _).Should().BeFalse();
    }

    // ---- (d) 200 -> EventDto mapping reflecting the response ----

    [TestMethod]
    public async Task UpdateEventTimes_Ok200_MapsUpdatedEventTimesFromTheResponse()
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(Ok(UpdatedEventBody)));
        var client = Client(handler);

        var result = await client.UpdateEventTimesAsync(
            "evt-1",
            NewStart,
            NewEnd,
            requestId: "req-d"
        );

        result.Ok.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.BridgeId.Should().Be("evt-1");
        result.Data.StartUtc.Should().Be(NewStart, "the mapped Start reflects the 200 payload");
        result.Data.EndUtc.Should().Be(NewEnd, "the mapped End reflects the 200 payload");
    }

    // ---- (e) D5 error samples ----

    [DataTestMethod]
    [DataRow(HttpStatusCode.BadRequest, "INVALID_REQUEST", DisplayName = "400 -> INVALID_REQUEST")]
    [DataRow(HttpStatusCode.NotFound, "NOT_FOUND", DisplayName = "404 -> NOT_FOUND")]
    public async Task UpdateEventTimes_TerminalStatus_MapsPerTheD5Matrix(
        HttpStatusCode status,
        string expectedCode
    )
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") })
        );
        var client = Client(handler);

        var result = await client.UpdateEventTimesAsync(
            "evt-1",
            NewStart,
            NewEnd,
            requestId: "req-e"
        );

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
        result.Error.Retryable.Should().BeFalse();
    }

    [TestMethod]
    public async Task UpdateEventTimes_Forbidden_MapsToUnauthorizedWithGraphCodePassthrough()
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

        var result = await client.UpdateEventTimesAsync(
            "evt-1",
            NewStart,
            NewEnd,
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
    public async Task UpdateEventTimes_ThrottledExhaustion_MapsToThrottledWithRetryAfter()
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

        var task = client.UpdateEventTimesAsync("evt-1", NewStart, NewEnd, requestId: "req-429");
        var safety = 0;
        while (!task.IsCompleted)
        {
            if (++safety > 10_000)
            {
                throw new AssertFailedException("The reschedule did not complete under fake time.");
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

    // ---- (g) unparseable 2xx and mapping-gap bodies ----

    [TestMethod]
    public async Task UpdateEventTimes_Unparseable2xx_MapsToTransportFailure()
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(Ok("not-json")));
        var client = Client(handler);

        var result = await client.UpdateEventTimesAsync(
            "evt-1",
            NewStart,
            NewEnd,
            requestId: "req-g1"
        );

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("TRANSPORT_FAILURE");
    }

    [TestMethod]
    public async Task UpdateEventTimes_MissingRequiredEventField_MapsToInternalError()
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(Ok(EventMissingEndBody)));
        var client = Client(handler);

        var result = await client.UpdateEventTimesAsync(
            "evt-1",
            NewStart,
            NewEnd,
            requestId: "req-g2"
        );

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("INTERNAL_ERROR", "a mapping gap must not fabricate data");
    }
}
