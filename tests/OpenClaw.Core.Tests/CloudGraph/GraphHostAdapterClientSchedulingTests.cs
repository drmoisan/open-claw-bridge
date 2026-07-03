using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Handler-level tests for members 7-8: the mailboxSettings request shape (URL,
/// <c>$select</c>, GET, headers) and the getSchedule POST whose JSON body matches the
/// spec example field-for-field (schedules array, start/end dateTime+timeZone,
/// <c>availabilityViewInterval</c> from options) in camelCase, asserted via
/// parsed-JSON structural comparison; recorded responses map to the scheduling DTOs.
/// </summary>
[TestClass]
public sealed class GraphHostAdapterClientSchedulingTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowStart = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = new(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);

    private static GraphHostAdapterClient Client(FakeHttpHandler handler)
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-sched", Start.AddHours(1)));

        return new GraphHostAdapterClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(
                new GraphAdapterOptions
                {
                    Enabled = true,
                    PrincipalMailboxUpn = "paula@contoso.com",
                    AssistantMailboxUpn = "amy@contoso.com",
                }
            ),
            tokenProvider.Object,
            new FakeTimeProvider(Start),
            NullLogger<GraphHostAdapterClient>.Instance
        );
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [TestMethod]
    public async Task GetMailboxSettings_ComposesTheExactRequestShape()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Json(GraphPayloadFixtures.MailboxSettings));
        });
        var client = Client(handler);

        var result = await client.GetMailboxSettingsAsync(requestId: "req-mbx");

        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be("/v1.0/users/paula%40contoso.com/mailboxSettings");
        captured.RequestUri.Query.Should().Be("?$select=timeZone,workingHours");
        captured.Headers.Authorization!.ToString().Should().Be("Bearer tok-sched");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-mbx");
    }

    [TestMethod]
    public async Task GetMailboxSettings_MapsTheRecordedResponse()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Json(GraphPayloadFixtures.MailboxSettings))
        );
        var client = Client(handler);

        var result = await client.GetMailboxSettingsAsync(requestId: "req-mbx2");

        result.Ok.Should().BeTrue();
        result.Data!.TimeZoneId.Should().Be("Pacific Standard Time");
        result.Data.WorkingDays.Should().HaveCount(5);
        result.Data.WorkingHoursStart.Should().Be(new TimeOnly(8, 0));
        result.Data.WorkingHoursEnd.Should().Be(new TimeOnly(17, 0));
        result.Meta.RequestId.Should().Be("req-mbx2");
        result.Meta.AdapterVersion.Should().Be("cloudgraph");
    }

    [TestMethod]
    public async Task GetFreeBusy_PostsTheSpecBodyFieldForField()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            captured = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Json(GraphPayloadFixtures.GetScheduleResponse);
        });
        var client = Client(handler);

        var result = await client.GetFreeBusyAsync(WindowStart, WindowEnd, requestId: "req-fb");

        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be("/v1.0/users/paula%40contoso.com/calendar/getSchedule");
        captured
            .Headers.GetValues("Prefer")
            .Should()
            .ContainSingle("the spec example carries only the timezone Prefer")
            .Which.Should()
            .Be("outlook.timezone=\"UTC\"");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-fb");

        // Structural comparison of the POST body against the spec example (camelCase).
        using var body = JsonDocument.Parse(capturedBody!);
        var root = body.RootElement;
        root.GetProperty("schedules").GetArrayLength().Should().Be(1);
        root.GetProperty("schedules")[0].GetString().Should().Be("paula@contoso.com");
        root.GetProperty("startTime")
            .GetProperty("dateTime")
            .GetString()
            .Should()
            .Be("2026-07-06T00:00:00");
        root.GetProperty("startTime").GetProperty("timeZone").GetString().Should().Be("UTC");
        root.GetProperty("endTime")
            .GetProperty("dateTime")
            .GetString()
            .Should()
            .Be("2026-07-10T00:00:00");
        root.GetProperty("endTime").GetProperty("timeZone").GetString().Should().Be("UTC");
        root.GetProperty("availabilityViewInterval")
            .GetInt32()
            .Should()
            .Be(30, "the interval comes from GraphAdapterOptions");
        root.EnumerateObject()
            .Should()
            .HaveCount(4, "the body carries exactly the four spec fields");
    }

    [TestMethod]
    public async Task GetFreeBusy_MapsTheRecordedResponseToBusyIntervals()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Json(GraphPayloadFixtures.GetScheduleResponse))
        );
        var client = Client(handler);

        var result = await client.GetFreeBusyAsync(WindowStart, WindowEnd, requestId: "req-fb2");

        result.Ok.Should().BeTrue();
        result.Data!.MailboxUpn.Should().Be("paula@contoso.com");
        result
            .Data.BusyIntervals.Should()
            .Equal(
                new BusyIntervalDto(
                    new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 6, 11, 0, 0, TimeSpan.Zero)
                ),
                new BusyIntervalDto(
                    new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 6, 13, 0, 0, TimeSpan.Zero)
                ),
                new BusyIntervalDto(
                    new DateTimeOffset(2026, 7, 6, 14, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 6, 14, 30, 0, TimeSpan.Zero)
                )
            );
    }

    [TestMethod]
    public async Task GetFreeBusy_EmptyWindow_YieldsEmptyBusyIntervalsSuccess()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Json(GraphPayloadFixtures.GetScheduleEmptyResponse))
        );
        var client = Client(handler);

        var result = await client.GetFreeBusyAsync(WindowStart, WindowEnd, requestId: "req-fb3");

        result.Ok.Should().BeTrue("an empty window is a success, not an error");
        result.Data!.BusyIntervals.Should().BeEmpty();
    }
}
