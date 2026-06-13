using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for the two scheduling methods added to <see cref="HostAdapterHttpClient"/>
/// (issue #74): <c>GetMailboxSettingsAsync</c> and <c>GetFreeBusyAsync</c>. Kept in a separate
/// file from <see cref="HostAdapterHttpClientTests"/> to hold each test file under the 500-line
/// cap. Tests replace the <c>TokenReader</c> seam to avoid filesystem I/O and use
/// <see cref="FakeHttpHandler"/> (defined alongside <see cref="HostAdapterHttpClientTests"/>) to
/// intercept outbound HTTP calls; no real network is used.
/// </summary>
[TestClass]
public class HostAdapterHttpClientSchedulingTests
{
    private static HostAdapterHttpClient BuildClient(
        FakeHttpHandler handler,
        Func<string, CancellationToken, Task<string?>> tokenReader
    )
    {
        var opts = new OpenClawOptions();
        opts.HostAdapter.TokenFile = "/run/openclaw/hostadapter.token";
        opts.HostAdapter.BaseUrl = "http://localhost:4319/";
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:4319/"),
        };
        return new HostAdapterHttpClient(httpClient, Options.Create(opts))
        {
            TokenReader = tokenReader,
        };
    }

    private static Func<string, CancellationToken, Task<string?>> ConstantTokenReader(
        string? token
    ) => (_, _) => Task.FromResult(token);

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private static ApiEnvelope<MailboxSettingsDto> MakeMailboxSettingsEnvelope(string requestId) =>
        new(
            true,
            new MailboxSettingsDto(
                "Pacific Standard Time",
                [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
                new TimeOnly(8, 0),
                new TimeOnly(16, 30)
            ),
            new ApiMeta(requestId, "1.0", null),
            null
        );

    private static ApiEnvelope<FreeBusyScheduleDto> MakeFreeBusyEnvelope(string requestId) =>
        new(
            true,
            new FreeBusyScheduleDto(
                "me",
                [
                    new BusyIntervalDto(
                        new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero)
                    ),
                ]
            ),
            new ApiMeta(requestId, "1.0", null),
            null
        );

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.GetMailboxSettingsAsync"/> sends a GET
    /// request to the exact Graph-shaped <c>users/{id}/mailboxSettings</c> relative path.
    /// </summary>
    [TestMethod]
    public async Task GetMailboxSettingsAsync_SendsGetToMailboxSettingsPath()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeMailboxSettingsEnvelope("r-mbx")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        await client.GetMailboxSettingsAsync();

        capturedPath.Should().Be("/users/me/mailboxSettings");
    }

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.GetMailboxSettingsAsync"/> round-trips the
    /// <see cref="ApiEnvelope{MailboxSettingsDto}"/> response payload.
    /// </summary>
    [TestMethod]
    public async Task GetMailboxSettingsAsync_WhenResponseIsValidEnvelope_ReturnsDeserializedSettings()
    {
        var expected = MakeMailboxSettingsEnvelope("r-mbx-ok");
        var handler = new FakeHttpHandler(_ => Task.FromResult(JsonResponse(expected)));
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        var result = await client.GetMailboxSettingsAsync(requestId: "r-mbx-ok");

        result.Ok.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.TimeZoneId.Should().Be("Pacific Standard Time");
        result
            .Data.WorkingDays.Should()
            .Equal(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
        result.Data.WorkingHoursStart.Should().Be(new TimeOnly(8, 0));
        result.Data.WorkingHoursEnd.Should().Be(new TimeOnly(16, 30));
    }

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.GetFreeBusyAsync"/> sends a GET request to
    /// the exact Graph-shaped <c>users/{id}/calendar/getSchedule</c> path with the
    /// <c>startDateTime</c> and <c>endDateTime</c> "O"-format values percent-encoded.
    /// </summary>
    [TestMethod]
    public async Task GetFreeBusyAsync_SendsGetToGetSchedulePathWithEncodedWindow()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeFreeBusyEnvelope("r-fb")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        var start = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
        await client.GetFreeBusyAsync(start, end);

        var expectedPath =
            "/users/me/calendar/getSchedule?startDateTime="
            + Uri.EscapeDataString(start.ToString("O"))
            + "&endDateTime="
            + Uri.EscapeDataString(end.ToString("O"));
        capturedPath.Should().Be(expectedPath);
    }

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.GetFreeBusyAsync"/> round-trips the
    /// <see cref="ApiEnvelope{FreeBusyScheduleDto}"/> response payload, including
    /// <c>BusyIntervals</c>.
    /// </summary>
    [TestMethod]
    public async Task GetFreeBusyAsync_WhenResponseIsValidEnvelope_ReturnsDeserializedSchedule()
    {
        var expected = MakeFreeBusyEnvelope("r-fb-ok");
        var handler = new FakeHttpHandler(_ => Task.FromResult(JsonResponse(expected)));
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        var result = await client.GetFreeBusyAsync(
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero),
            requestId: "r-fb-ok"
        );

        result.Ok.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.MailboxUpn.Should().Be("me");
        result
            .Data.BusyIntervals.Should()
            .Equal(
                new BusyIntervalDto(
                    new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero)
                )
            );
    }
}
