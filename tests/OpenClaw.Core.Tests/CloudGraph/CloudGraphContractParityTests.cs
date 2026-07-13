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
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;
using IHostAdapterClient = OpenClaw.HostAdapter.Contracts.IHostAdapterClient;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Contract-parity tests (AC-4): <see cref="HostAdapterSchedulingService"/> — the
/// Agent/Runtime consumer of <c>IHostAdapterClient</c> — runs unchanged against
/// <see cref="GraphHostAdapterClient"/> backed by a mocked handler returning recorded
/// Graph payloads. Assertions compare the Runtime-visible outcomes (mapped domain
/// values), not transport details: mailbox-settings flow, free/busy flow,
/// calendar-window flow, and a failure-propagation flow.
/// </summary>
[TestClass]
public sealed class CloudGraphContractParityTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowStart = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = new(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the Runtime service on top of the Graph-backed client.</summary>
    private static HostAdapterSchedulingService Service(FakeHttpHandler handler) =>
        new(GraphClient(handler), new SchedulingDtoMapper());

    /// <summary>Builds the raw Graph-backed <see cref="GraphHostAdapterClient"/> for direct calls.</summary>
    private static GraphHostAdapterClient GraphClient(FakeHttpHandler handler)
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-parity", Start.AddHours(1)));

        var options = new GraphAdapterOptions
        {
            Enabled = true,
            PrincipalMailboxUpn = "paula@contoso.com",
            AssistantMailboxUpn = "amy@contoso.com",
        };
        // F15: the assistant sends on behalf of the principal, so the principal must be
        // allowlisted for the send path to reach Graph (fail-closed gate, issue #119).
        options.AllowedPrincipalMailboxUpns.Add("paula@contoso.com");

        return new GraphHostAdapterClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(options),
            tokenProvider.Object,
            new FakeTimeProvider(Start),
            NullLogger<GraphHostAdapterClient>.Instance
        );
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [TestMethod]
    public async Task MailboxSettingsFlow_YieldsTheMappedDomainSettings()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Json(GraphPayloadFixtures.MailboxSettings))
        );
        var service = Service(handler);

        var settings = await service.GetMailboxSettingsAsync(CancellationToken.None);

        settings.TimeZoneId.Should().Be("Pacific Standard Time");
        settings
            .WorkingDays.Should()
            .Equal(
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            );
        settings.WorkingHoursStart.Should().Be(new TimeOnly(8, 0));
        settings.WorkingHoursEnd.Should().Be(new TimeOnly(17, 0));
    }

    [TestMethod]
    public async Task FreeBusyFlow_YieldsTheD11BusyIntervals()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Json(GraphPayloadFixtures.GetScheduleResponse))
        );
        var service = Service(handler);

        var schedule = await service.GetFreeBusyAsync(
            WindowStart,
            WindowEnd,
            CancellationToken.None
        );

        schedule.MailboxUpn.Should().Be("paula@contoso.com");
        schedule
            .BusyIntervals.Should()
            .HaveCount(3, "busy/oof/tentative block; free/workingElsewhere do not (D11)");
        schedule
            .BusyIntervals[0]
            .Start.Should()
            .Be(new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero));
        schedule
            .BusyIntervals[2]
            .End.Should()
            .Be(new DateTimeOffset(2026, 7, 6, 14, 30, 0, TimeSpan.Zero));
    }

    [TestMethod]
    public async Task CalendarWindowFlow_YieldsMappedSchedulingEvents()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Json(GraphPayloadFixtures.EventListPage))
        );
        var service = Service(handler);

        var events = await service.GetCalendarViewAsync(
            WindowStart,
            WindowEnd,
            CancellationToken.None
        );

        events.Should().HaveCount(2);

        var occurrence = events[0];
        occurrence.Id.Should().Be("evt-001");
        occurrence.ICalUId.Should().Be("ical-001");
        occurrence.SeriesMasterId.Should().Be("master-001");
        occurrence
            .Sensitivity.Should()
            .Be("private", "the int Sensitivity round-trips to the agent vocabulary");
        occurrence.Start.Should().Be(new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero));
        occurrence.End.Should().Be(new DateTimeOffset(2026, 7, 6, 11, 0, 0, TimeSpan.Zero));
        occurrence.Organizer!.Email.Should().Be("olive@contoso.com");
        occurrence
            .RequiredAttendees.Should()
            .ContainSingle()
            .Which.Email.Should()
            .Be("alice@contoso.com");
        occurrence
            .OptionalAttendees.Should()
            .ContainSingle()
            .Which.Email.Should()
            .Be("bob@contoso.com");
        occurrence
            .ResourceAttendees.Should()
            .ContainSingle()
            .Which.Email.Should()
            .Be("room4@contoso.com");
        occurrence.Categories.Should().Equal("Focus", "OneOnOne");
        occurrence.IsOrganizer.Should().BeTrue();

        var single = events[1];
        single.Id.Should().Be("evt-002");
        single.Sensitivity.Should().Be("normal");
        single.RequiredAttendees.Should().ContainSingle();
    }

    [TestMethod]
    public async Task FailurePropagationFlow_SendMailFailureEnvelopeThrowsWithTheMappedCode()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{}"),
                }
            )
        );
        var service = Service(handler);
        var request = new SendMailRequest(
            Subject: "Re: scheduling",
            BodyContent: "Proposed times below.",
            BodyContentType: "Text",
            ToRecipients: [new AttendeeDto("Rex R", "rex@example.com")],
            CcRecipients: [],
            InReplyToMessageId: null
        );

        var act = () => service.SendMailAsync(request, ct: CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "*INVALID_REQUEST*",
                "the Graph-mapped error code propagates through the Runtime failure path"
            );
    }

    [TestMethod]
    public void ContractParity_BothImplementations_ExposeGetEventForMessageAsync()
    {
        // The message-to-event linkage read (issue #146) is part of the IHostAdapterClient contract,
        // so both the local HTTP backend and the cloud Graph backend must implement it. This gate
        // fails if either implementation drops the method.
        var interfaceMethod = typeof(IHostAdapterClient).GetMethod(
            nameof(IHostAdapterClient.GetEventForMessageAsync)
        );
        interfaceMethod.Should().NotBeNull("the linkage read is part of the client contract");

        foreach (
            var implementation in new[]
            {
                typeof(HostAdapterHttpClient),
                typeof(GraphHostAdapterClient),
            }
        )
        {
            var map = implementation.GetInterfaceMap(typeof(IHostAdapterClient));
            var index = Array.IndexOf(map.InterfaceMethods, interfaceMethod);
            index
                .Should()
                .BeGreaterThanOrEqualTo(
                    0,
                    $"{implementation.Name} must implement GetEventForMessageAsync"
                );
            map.TargetMethods[index].Should().NotBeNull();
        }
    }

    [TestMethod]
    public async Task GetEventForMessageFlow_GraphBackend_HonorsTheNullContract()
    {
        // The Graph client resolves the linkage read to a clean ok:true/data:null envelope with no
        // HTTP round-trip; the handler fails the test if invoked.
        var invoked = false;
        var handler = new FakeHttpHandler(_ =>
        {
            invoked = true;
            return Task.FromResult(Json("{}"));
        });
        var graphClient = GraphClient(handler);

        var result = await graphClient.GetEventForMessageAsync("mtg:abc", requestId: "req-link");

        result.Ok.Should().BeTrue("an unlinked message is a clean success, not an error");
        result.Data.Should().BeNull();
        result.Error.Should().BeNull();
        invoked.Should().BeFalse("the cloud linkage read performs no Graph HTTP call");
    }

    [TestMethod]
    public async Task CalendarWindowFlow_FailureEnvelope_DegradesToEmptyListLikeTheLocalBackend()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(GraphPayloadFixtures.ErrorItemNotFoundBody),
                }
            )
        );
        var service = Service(handler);

        var events = await service.GetCalendarViewAsync(
            WindowStart,
            WindowEnd,
            CancellationToken.None
        );

        events.Should().BeEmpty("the Runtime degradation contract is backend-independent");
    }
}
