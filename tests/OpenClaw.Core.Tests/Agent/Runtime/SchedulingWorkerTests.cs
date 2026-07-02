using System;
using System.Collections.Generic;
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
using OpenClaw.HostAdapter.Contracts;
using SendMailRequest = OpenClaw.Core.Agent.SendMailRequest;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Unit tests for the <see cref="SchedulingWorker"/> kill switches and failure
/// isolation (AC-7, AC-9).
/// </summary>
[TestClass]
public sealed class SchedulingWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);

    private static AgentPolicyOptions Options(
        bool sendEnabled = false,
        bool calendarWriteEnabled = false
    ) =>
        new()
        {
            InternalDomains = new[] { "contoso.com" },
            InternalDomain = "contoso.com",
            SendEnabled = sendEnabled,
            CalendarWriteEnabled = calendarWriteEnabled,
            NoMeetingBlocks = Array.Empty<string>(),
            MinNoticeMinutes = 0,
            PreferredDays = Array.Empty<string>(),
        };

    private static SchedulingMessageDto Message(string id = "msg-1") =>
        new(
            Id: id,
            Subject: "Project sync",
            BodyPreview: "Let us meet",
            BodyContent: null,
            BodyContentType: null,
            From: new AttendeeDto("Colleague", "colleague@contoso.com"),
            Sender: new AttendeeDto("Colleague", "colleague@contoso.com"),
            ToRecipients: Array.Empty<AttendeeDto>(),
            CcRecipients: Array.Empty<AttendeeDto>(),
            ConversationId: "conv-1",
            ReceivedDateTime: Now,
            MeetingMessageType: "meetingRequest",
            Importance: "normal"
        );

    private static MailboxSettingsDto MailboxSettings() =>
        new(
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
        );

    private static Mock<ISchedulingService> ServiceReturningContext()
    {
        var service = new Mock<ISchedulingService>();
        service
            .Setup(s => s.GetSchedulingMessageAsync("msg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Message());
        service
            .Setup(s => s.GetEventForMessageAsync("msg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SchedulingEventDto?)null);
        service
            .Setup(s => s.GetMailboxSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailboxSettings());
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

    /// <summary>
    /// Adds hydration setups for an additional candidate id so multi-candidate cycles can
    /// be exercised alongside the default "msg-1" context.
    /// </summary>
    private static void SetupAdditionalCandidate(Mock<ISchedulingService> service, string id)
    {
        service
            .Setup(s => s.GetSchedulingMessageAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Message(id));
        service
            .Setup(s => s.GetEventForMessageAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SchedulingEventDto?)null);
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
        Mock<ISchedulingCandidateSource> source,
        AgentPolicyOptions options
    )
    {
        // Default store: nothing is recorded, so existing worker tests keep their
        // pre-dedupe behavior.
        var sentActionStore = new Mock<ISentActionStore>();
        sentActionStore
            .Setup(s => s.IsRecordedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return new(
            service.Object,
            sentActionStore.Object,
            source.Object,
            Microsoft.Extensions.Options.Options.Create(options),
            new FakeTimeProvider(Now),
            NullLogger<SchedulingWorker>.Instance
        );
    }

    [TestMethod]
    public async Task RunCycle_SendDisabled_NeverInvokesSendMail()
    {
        var service = ServiceReturningContext();
        var source = CandidateSource("msg-1");
        var worker = Worker(service, source, Options(sendEnabled: false));

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        service.Verify(
            s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task RunCycle_SendEnabled_InvokesSendMail()
    {
        var service = ServiceReturningContext();
        var captured = new List<SendMailRequest>();
        service
            .Setup(s => s.SendMailAsync(Capture.In(captured), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var source = CandidateSource("msg-1");
        var worker = Worker(service, source, Options(sendEnabled: true));

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        service.Verify(
            s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        // The composed agent request (spec "Seeded Test Conditions"): reply subject, one
        // To recipient equal to the normalized MessageFrom, and a non-empty plain-text
        // body produced by the slot-proposal formatter.
        var request = captured.Should().ContainSingle().Subject;
        request.Subject.Should().Be("Re: Project sync");
        request
            .ToRecipients.Should()
            .ContainSingle()
            .Which.Should()
            .Be(new AttendeeDto(string.Empty, "colleague@contoso.com"));
        request.BodyContentType.Should().Be("text");
        request.BodyContent.Should().NotBeNullOrWhiteSpace().And.StartWith("Proposed times:");
    }

    [TestMethod]
    public async Task RunCycle_SendFailure_LogsAndContinues()
    {
        var service = ServiceReturningContext();
        SetupAdditionalCandidate(service, "msg-2");
        service
            .Setup(s =>
                s.SendMailAsync(
                    It.Is<SendMailRequest>(r => r.InReplyToMessageId == "msg-1"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("send failed"));
        var source = CandidateSource("msg-1", "msg-2");
        var worker = Worker(service, source, Options(sendEnabled: true));

        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Per-message isolation (ProcessMessageSafelyAsync): the first send failure is
        // logged, the cycle does not throw, and the second candidate is still processed.
        await act.Should().NotThrowAsync();
        service.Verify(
            s => s.GetSchedulingMessageAsync("msg-2", It.IsAny<CancellationToken>()),
            Times.Once
        );
        service.Verify(
            s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [TestMethod]
    public async Task RunCycle_SendCancellation_StopsCycle()
    {
        var service = ServiceReturningContext();
        SetupAdditionalCandidate(service, "msg-2");
        service
            .Setup(s =>
                s.SendMailAsync(
                    It.Is<SendMailRequest>(r => r.InReplyToMessageId == "msg-1"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException());
        var source = CandidateSource("msg-1", "msg-2");
        var worker = Worker(service, source, Options(sendEnabled: true));

        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // AC-3: cancellation stops the cycle — OperationCanceledException propagates and
        // the second candidate is never hydrated.
        await act.Should().ThrowAsync<OperationCanceledException>();
        service.Verify(
            s => s.GetSchedulingMessageAsync("msg-2", It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task RunCycle_CalendarWriteDisabled_DeterministicPipelineStillRuns()
    {
        var service = ServiceReturningContext();
        var source = CandidateSource("msg-1");
        var worker = Worker(service, source, Options(calendarWriteEnabled: false));

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // The deterministic pipeline still hydrates and proposes even with calendar
        // writes disabled.
        service.Verify(
            s => s.GetSchedulingMessageAsync("msg-1", It.IsAny<CancellationToken>()),
            Times.Once
        );
        service.Verify(s => s.GetMailboxSettingsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RunCycle_SingleHydrateFailure_DoesNotStopLoop()
    {
        var service = ServiceReturningContext();
        service
            .Setup(s => s.GetSchedulingMessageAsync("bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var source = CandidateSource("bad", "msg-1");
        var worker = Worker(service, source, Options());

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // The good message after the failing one is still processed.
        service.Verify(
            s => s.GetSchedulingMessageAsync("msg-1", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task RunCycle_NoCandidates_DoesNothing()
    {
        var service = ServiceReturningContext();
        var source = CandidateSource();
        var worker = Worker(service, source, Options());

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        service.Verify(
            s => s.GetSchedulingMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task RunCycle_CandidateSourceFailure_SkipsCycleWithoutThrowing()
    {
        var service = ServiceReturningContext();
        var source = new Mock<ISchedulingCandidateSource>();
        source
            .Setup(s => s.GetCandidateMessageIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("candidate source down"));
        var worker = Worker(service, source, Options());

        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        service.Verify(
            s => s.GetSchedulingMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
