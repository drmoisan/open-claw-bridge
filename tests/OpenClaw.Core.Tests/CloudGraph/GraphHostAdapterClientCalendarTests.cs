using System;
using System.Linq;
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
/// Handler-level request-shape tests for interface members 5-6: the
/// <c>calendarView</c> URL with <c>startDateTime</c>/<c>endDateTime</c>/<c>$top</c>/
/// <c>$select</c>, the event-get URL with an escaped id, GET method and
/// bearer/client-request-id/Prefer headers, recorded-payload mapping to
/// <c>EventDto</c> items with parity fields spot-asserted, and the empty-page
/// success case.
/// </summary>
[TestClass]
public sealed class GraphHostAdapterClientCalendarTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowStart = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = new(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The spec event field list, pinned as a literal (D4).</summary>
    private const string ExpectedEventSelect =
        "id,iCalUId,seriesMasterId,subject,bodyPreview,body,organizer,attendees,"
        + "categories,isOrganizer,isOnlineMeeting,allowNewTimeProposals,sensitivity,"
        + "showAs,responseStatus,location,start,end,type,lastModifiedDateTime";

    private static GraphHostAdapterClient Client(FakeHttpHandler handler)
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-cal", Start.AddHours(1)));

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
    public async Task ListCalendarWindow_ComposesTheExactRequestShape()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Json(GraphPayloadFixtures.EventListPage));
        });
        var client = Client(handler);

        var result = await client.ListCalendarWindowAsync(
            WindowStart,
            WindowEnd,
            requestId: "req-cal"
        );

        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be("/v1.0/users/paula%40contoso.com/calendarView");
        Uri.UnescapeDataString(captured.RequestUri.Query)
            .Should()
            .Be(
                "?startDateTime=2026-07-06T00:00:00.0000000+00:00"
                    + "&endDateTime=2026-07-10T00:00:00.0000000+00:00"
                    + "&$top=50"
                    + $"&$select={ExpectedEventSelect}",
                "the query pins the window, min(limit, PageSize) top, and spec $select"
            );
        captured.Headers.Authorization!.ToString().Should().Be("Bearer tok-cal");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-cal");
        captured
            .Headers.GetValues("Prefer")
            .Should()
            .BeEquivalentTo("outlook.timezone=\"UTC\"", "outlook.body-content-type=\"text\"");
    }

    [TestMethod]
    public async Task ListCalendarWindow_MapsRecordedPayloadsToEventDtos()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Json(GraphPayloadFixtures.EventListPage))
        );
        var client = Client(handler);

        var result = await client.ListCalendarWindowAsync(
            WindowStart,
            WindowEnd,
            requestId: "req-map"
        );

        result.Ok.Should().BeTrue();
        result.Data!.Items.Select(e => e.BridgeId).Should().Equal("evt-001", "evt-002");

        var occurrence = result.Data.Items[0];
        occurrence.StartUtc.Should().Be(new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero));
        occurrence.EndUtc.Should().Be(new DateTimeOffset(2026, 7, 6, 11, 0, 0, TimeSpan.Zero));
        occurrence.Sensitivity.Should().Be(2, "private maps to 2");
        occurrence.SeriesMasterId.Should().Be("master-001");
        occurrence.ICalUId.Should().Be("ical-001");
        occurrence.IsRecurring.Should().BeTrue();
        occurrence.Organizer.Should().Be("olive@contoso.com");
        occurrence
            .RequiredAttendeesJson.Should()
            .Be("""[{"name":"Alice A","email":"alice@contoso.com"}]""");

        var single = result.Data.Items[1];
        single.IsRecurring.Should().BeFalse();
        single.BusyStatus.Should().Be(1, "tentative maps to 1");
    }

    [TestMethod]
    public async Task ListCalendarWindow_EmptyPage_YieldsEmptyItemsSuccess()
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(Json("""{ "value": [] }""")));
        var client = Client(handler);

        var result = await client.ListCalendarWindowAsync(
            WindowStart,
            WindowEnd,
            requestId: "req-empty"
        );

        result.Ok.Should().BeTrue("an empty window is a success, not an error");
        result.Data!.Items.Should().BeEmpty();
        result.Error.Should().BeNull();
    }

    [TestMethod]
    public async Task GetEvent_ComposesTheExactRequestShapeWithEscapedId()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Json(GraphPayloadFixtures.EventPrivateOccurrence));
        });
        var client = Client(handler);

        var result = await client.GetEventAsync("AAMk+evt=", requestId: "req-evt");

        result.Ok.Should().BeTrue();
        result.Data!.BridgeId.Should().Be("evt-001");
        result.Data.Sensitivity.Should().Be(2);
        captured!.Method.Should().Be(HttpMethod.Get);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be(
                "/v1.0/users/paula%40contoso.com/events/AAMk%2Bevt%3D",
                "the Graph event id is URL-escaped into the route segment"
            );
        Uri.UnescapeDataString(captured.RequestUri.Query)
            .Should()
            .Be($"?$select={ExpectedEventSelect}");
        captured.Headers.Authorization!.ToString().Should().Be("Bearer tok-cal");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-evt");
        captured
            .Headers.GetValues("Prefer")
            .Should()
            .BeEquivalentTo("outlook.timezone=\"UTC\"", "outlook.body-content-type=\"text\"");
    }
}
