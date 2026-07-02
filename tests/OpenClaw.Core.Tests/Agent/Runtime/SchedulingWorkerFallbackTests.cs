using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.HostAdapter.Contracts;
using SendMailRequest = OpenClaw.Core.Agent.SendMailRequest;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Unit tests for the calendar-view fallback in the <see cref="SchedulingWorker"/>
/// pipeline (#103 AC-4, AC-5): a direct event-lookup miss fetches the forward calendar
/// window from the injected <see cref="TimeProvider"/> now, a matcher hit hydrates the
/// event context through the same Normalize call formal traffic uses, a no-match or
/// empty window falls through to message-only triage, and a non-positive
/// <c>CalendarViewFallbackDays</c> skips the fetch entirely.
/// </summary>
[TestClass]
public sealed class SchedulingWorkerFallbackTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);

    private static AgentPolicyOptions Options(
        bool sendEnabled = false,
        int calendarViewFallbackDays = 14
    ) =>
        new()
        {
            InternalDomains = new[] { "contoso.com" },
            InternalDomain = "contoso.com",
            SendEnabled = sendEnabled,
            CalendarViewFallbackDays = calendarViewFallbackDays,
            CalendarWriteEnabled = false,
            NoMeetingBlocks = Array.Empty<string>(),
            MinNoticeMinutes = 0,
            PreferredDays = Array.Empty<string>(),
        };

    private static SchedulingMessageDto Message(string id = "msg-1") =>
        new(
            Id: id,
            Subject: "Project sync",
            BodyPreview: "Can we move this?",
            BodyContent: null,
            BodyContentType: null,
            From: new AttendeeDto("Colleague", "colleague@contoso.com"),
            Sender: new AttendeeDto("Colleague", "colleague@contoso.com"),
            ToRecipients: Array.Empty<AttendeeDto>(),
            CcRecipients: Array.Empty<AttendeeDto>(),
            ConversationId: "conv-1",
            ReceivedDateTime: Now,
            MeetingMessageType: null,
            Importance: "normal"
        );

    private static SchedulingEventDto WindowEvent(string? subject, string id = "evt-1") =>
        new(
            Id: id,
            ICalUId: "ical-1",
            SeriesMasterId: null,
            Subject: subject,
            BodyPreview: null,
            BodyContent: null,
            BodyContentType: null,
            Organizer: new AttendeeDto("Organizer", "organizer@contoso.com"),
            RequiredAttendees: Array.Empty<AttendeeDto>(),
            OptionalAttendees: Array.Empty<AttendeeDto>(),
            ResourceAttendees: Array.Empty<AttendeeDto>(),
            Categories: Array.Empty<string>(),
            IsOrganizer: false,
            IsOnlineMeeting: false,
            AllowNewTimeProposals: true,
            Sensitivity: "normal",
            Start: Now.AddDays(2),
            StartTimeZone: null,
            End: Now.AddDays(2).AddHours(1),
            EndTimeZone: null,
            LastModifiedDateTime: null,
            Type: null
        );

    /// <summary>
    /// Builds a service mock whose direct event lookup misses and whose calendar-view
    /// fallback returns the given window (explicit stub — never Moq loose defaults).
    /// </summary>
    private static Mock<ISchedulingService> ServiceWithLookupMiss(
        IReadOnlyList<SchedulingEventDto> windowEvents
    )
    {
        var service = new Mock<ISchedulingService>();
        service
            .Setup(s => s.GetSchedulingMessageAsync("msg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Message());
        service
            .Setup(s => s.GetEventForMessageAsync("msg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SchedulingEventDto?)null);
        service
            .Setup(s =>
                s.GetCalendarViewAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(windowEvents);
        service
            .Setup(s => s.GetMailboxSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MailboxSettingsDto(
                    "UTC",
                    new[]
                    {
                        DayOfWeek.Monday,
                        DayOfWeek.Tuesday,
                        DayOfWeek.Wednesday,
                        DayOfWeek.Thursday,
                        DayOfWeek.Friday,
                    },
                    new TimeOnly(9, 0),
                    new TimeOnly(17, 0)
                )
            );
        service
            .Setup(s =>
                s.GetFreeBusyAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FreeBusyScheduleDto("owner@contoso.com", Array.Empty<BusyIntervalDto>())
            );
        return service;
    }

    private static Mock<ISchedulingCandidateSource> CandidateSource(params string[] ids)
    {
        var source = new Mock<ISchedulingCandidateSource>();
        source
            .Setup(s => s.GetCandidateMessageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);
        return source;
    }

    private static SchedulingWorker Worker(
        Mock<ISchedulingService> service,
        AgentPolicyOptions options
    )
    {
        var sentActionStore = new Mock<ISentActionStore>();
        sentActionStore
            .Setup(s => s.IsRecordedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return new(
            service.Object,
            sentActionStore.Object,
            new Mock<IActionAuditLog>().Object,
            CandidateSource("msg-1").Object,
            Microsoft.Extensions.Options.Options.Create(options),
            new FakeTimeProvider(Now),
            NullLogger<SchedulingWorker>.Instance
        );
    }

    [TestMethod]
    public async Task RunCycle_LookupMiss_FetchesCalendarViewFromNowToNowPlusFourteenDays()
    {
        // Arrange: direct lookup misses; the default fallback window is 14 days.
        var service = ServiceWithLookupMiss(Array.Empty<SchedulingEventDto>());
        var worker = Worker(service, Options());

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert: window bounds come from the injected TimeProvider, not wall clock.
        service.Verify(
            s => s.GetCalendarViewAsync(Now, Now.AddDays(14), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task RunCycle_WindowEventClearsThreshold_HydratesEventContextIntoSendPath()
    {
        // Arrange: the window event shares two subject tokens with the message
        // ("project", "sync" -> score 4), so the matcher accepts it. Normalize prefers
        // the event subject, which is observable in the outbound reply subject.
        var matched = WindowEvent(subject: "Project sync planning");
        var service = ServiceWithLookupMiss([matched]);
        var captured = new List<SendMailRequest>();
        service
            .Setup(s =>
                s.SendMailAsync(
                    Capture.In(captured),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        var worker = Worker(service, Options(sendEnabled: true));

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert: the pipeline behaved event-linked — the reply subject carries the
        // matched event's subject through MeetingContextNormalizer.Normalize.
        captured
            .Should()
            .ContainSingle()
            .Which.Subject.Should()
            .Be("Re: Project sync planning", "a matched window event hydrates the context");
    }

    [TestMethod]
    public async Task RunCycle_EmptyWindow_ProceedsMessageOnlyWithoutThrowing()
    {
        // Arrange: the fallback window is empty; triage must proceed message-only.
        var service = ServiceWithLookupMiss(Array.Empty<SchedulingEventDto>());
        var captured = new List<SendMailRequest>();
        service
            .Setup(s =>
                s.SendMailAsync(
                    Capture.In(captured),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        var worker = Worker(service, Options(sendEnabled: true));

        // Act
        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert: no exception, the window was consulted, and the reply subject is the
        // message subject (message-only context).
        await act.Should().NotThrowAsync("an empty window degrades to message-only triage");
        service.Verify(
            s =>
                s.GetCalendarViewAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        captured
            .Should()
            .ContainSingle()
            .Which.Subject.Should()
            .Be("Re: Project sync", "message-only triage uses the message subject");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task RunCycle_NonPositiveFallbackDays_NeverFetchesCalendarView(int days)
    {
        // Arrange: the documented opt-out — a non-positive window skips the fetch.
        var service = ServiceWithLookupMiss(Array.Empty<SchedulingEventDto>());
        var worker = Worker(service, Options(calendarViewFallbackDays: days));

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        service.Verify(
            s =>
                s.GetCalendarViewAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [TestMethod]
    public async Task RunCycle_AllWindowEventsBelowThreshold_FallsThroughToMessageOnly()
    {
        // Arrange: one shared token ("sync" -> score 2) stays below the threshold of 4,
        // so the matcher rejects every window event.
        var belowThreshold = WindowEvent(subject: "Design sync");
        var service = ServiceWithLookupMiss([belowThreshold]);
        var captured = new List<SendMailRequest>();
        service
            .Setup(s =>
                s.SendMailAsync(
                    Capture.In(captured),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        var worker = Worker(service, Options(sendEnabled: true));

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert: the reply subject is message-derived, proving message-only triage.
        captured
            .Should()
            .ContainSingle()
            .Which.Subject.Should()
            .Be("Re: Project sync", "sub-threshold window events must not hydrate the context");
    }

    [TestMethod]
    public async Task RunCycle_FailureEnvelopeMappedToEmptyWindow_ProceedsWithoutException()
    {
        // Arrange: HostAdapterSchedulingService maps a failure envelope to an empty
        // list; the pipeline must degrade to message-only triage without throwing.
        var service = ServiceWithLookupMiss(Array.Empty<SchedulingEventDto>());
        var worker = Worker(service, Options());

        // Act
        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert: no exception and the deterministic pipeline continued past the
        // fallback (mailbox settings are consulted by the proposal stage).
        await act.Should().NotThrowAsync("an unavailable window read degrades to no-match");
        service.Verify(s => s.GetMailboxSettingsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
