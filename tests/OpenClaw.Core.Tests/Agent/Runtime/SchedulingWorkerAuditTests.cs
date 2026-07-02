using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
/// Unit tests for the <see cref="SchedulingWorker"/> audit emissions (issue #107, AC2-AC4):
/// one record per Stage 0 decision point (<c>send_disabled</c>, <c>dedupe_skipped</c>,
/// <c>sent</c> before the dedupe record, <c>send_failed</c> with error detail before
/// propagation), correlation-id generation and forwarding, audit-failure resilience (D4),
/// and the <see cref="FakeTimeProvider"/>-sourced <c>RecordedAtUtc</c>.
/// </summary>
[TestClass]
public sealed class SchedulingWorkerAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);

    private static readonly DayOfWeek[] Weekdays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    ];

    private static AgentPolicyOptions Options(bool sendEnabled = true) =>
        new()
        {
            InternalDomains = new[] { "contoso.com" },
            InternalDomain = "contoso.com",
            SendEnabled = sendEnabled,
            CalendarWriteEnabled = false,
            NoMeetingBlocks = Array.Empty<string>(),
            MinNoticeMinutes = 0,
            PreferredDays = Array.Empty<string>(),
        };

    private static SchedulingMessageDto Message(string id) =>
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

    private static Mock<ISchedulingService> ServiceReturningContext(params string[] ids)
    {
        var service = new Mock<ISchedulingService>();
        foreach (var id in ids)
        {
            service
                .Setup(s => s.GetSchedulingMessageAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Message(id));
            service
                .Setup(s => s.GetEventForMessageAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SchedulingEventDto?)null);
        }

        service
            .Setup(s =>
                s.GetCalendarViewAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<SchedulingEventDto>());
        service
            .Setup(s => s.GetMailboxSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxSettingsDto("UTC", Weekdays, new(9, 0), new(17, 0)));
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

    private static Mock<ISentActionStore> Store(bool isRecorded = false)
    {
        var store = new Mock<ISentActionStore>();
        store
            .Setup(s => s.IsRecordedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(isRecorded);
        store
            .Setup(s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        return store;
    }

    private static Mock<IActionAuditLog> AuditLog(List<ActionAuditRecord> captured)
    {
        var audit = new Mock<IActionAuditLog>();
        audit
            .Setup(a => a.RecordAsync(Capture.In(captured), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return audit;
    }

    /// <summary>Builds a worker over the single default candidate "msg-1".</summary>
    private static SchedulingWorker Worker(
        Mock<ISchedulingService> service,
        Mock<ISentActionStore> store,
        Mock<IActionAuditLog> audit,
        AgentPolicyOptions options,
        ILogger<SchedulingWorker>? logger = null
    ) =>
        new(
            service.Object,
            store.Object,
            audit.Object,
            CandidateSource("msg-1").Object,
            Microsoft.Extensions.Options.Options.Create(options),
            new FakeTimeProvider(Now),
            logger ?? NullLogger<SchedulingWorker>.Instance
        );

    private static void VerifyErrorLogged(
        Mock<ILogger<SchedulingWorker>> logger,
        string messageFragment,
        Func<Exception?, bool> exceptionPredicate
    ) =>
        logger.Verify(
            l =>
                l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(messageFragment)),
                    It.Is<Exception?>(e => exceptionPredicate(e)),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );

    [TestMethod]
    public async Task RunCycle_SendDisabled_WritesSendDisabledRecord()
    {
        // Arrange (AC2 kill-switch suppression)
        var captured = new List<ActionAuditRecord>();
        var service = ServiceReturningContext("msg-1");
        var worker = Worker(service, Store(), AuditLog(captured), Options(sendEnabled: false));

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        var record = captured.Should().ContainSingle().Subject;
        record.ResultCode.Should().Be(ActionAuditResultCode.SendDisabled);
        record.Mailbox.Should().Be("owner@contoso.com");
        record.MessageId.Should().Be("msg-1");
        record.EventId.Should().BeNull("the pipeline ran message-only");
        record.ActionType.Should().Be(SentActionKey.ProposalReply);
        record.ActingFlags.Should().Be("SendEnabled=False;CalendarWriteEnabled=False");
        record.ErrorDetail.Should().BeNull();
        record.OriginalStartUtc.Should().BeNull("Stage 0 send actions carry no time columns");
    }

    [TestMethod]
    public async Task RunCycle_DedupeHit_WritesDedupeSkippedRecord()
    {
        // Arrange (AC2 dedupe hit)
        var captured = new List<ActionAuditRecord>();
        var service = ServiceReturningContext("msg-1");
        var worker = Worker(service, Store(isRecorded: true), AuditLog(captured), Options());

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        var record = captured.Should().ContainSingle().Subject;
        record.ResultCode.Should().Be(ActionAuditResultCode.DedupeSkipped);
        record.ActingFlags.Should().Be("SendEnabled=True;CalendarWriteEnabled=False");
        service.Verify(
            s =>
                s.SendMailAsync(
                    It.IsAny<SendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [TestMethod]
    public async Task RunCycle_SendSuccess_WritesSentRecordAfterSendAndBeforeDedupeRecord()
    {
        // Arrange (AC2 success ordering): callbacks capture the call order so the
        // send → audit(sent) → dedupe-record sequence is provable.
        var callOrder = new List<string>();
        var captured = new List<ActionAuditRecord>();
        var service = ServiceReturningContext("msg-1");
        service
            .Setup(s =>
                s.SendMailAsync(
                    It.IsAny<SendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => callOrder.Add("send"))
            .Returns(Task.CompletedTask);
        var store = Store();
        store
            .Setup(s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => callOrder.Add("dedupe-record"))
            .Returns(Task.CompletedTask);
        var audit = new Mock<IActionAuditLog>();
        audit
            .Setup(a => a.RecordAsync(Capture.In(captured), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("audit"))
            .Returns(Task.CompletedTask);
        var worker = Worker(service, store, audit, Options());

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        var record = captured.Should().ContainSingle().Subject;
        record.ResultCode.Should().Be(ActionAuditResultCode.Sent);
        record.ErrorDetail.Should().BeNull();
        callOrder
            .Should()
            .Equal(
                ["send", "audit", "dedupe-record"],
                "the sent record is written after the send completes and before the dedupe record"
            );
    }

    [TestMethod]
    public async Task RunCycle_SendFailure_WritesSendFailedWithErrorDetailBeforePropagation()
    {
        // Arrange (AC2 failure): the send throws; the send_failed record must carry the
        // exception type and message, and the original exception must still reach
        // ProcessMessageSafelyAsync (observable via its Error log).
        var captured = new List<ActionAuditRecord>();
        var service = ServiceReturningContext("msg-1");
        service
            .Setup(s =>
                s.SendMailAsync(
                    It.IsAny<SendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("send failed"));
        var store = Store();
        var logger = new Mock<ILogger<SchedulingWorker>>();
        var worker = Worker(service, store, AuditLog(captured), Options(), logger.Object);

        // Act
        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("per-message isolation contains the send failure");
        var record = captured.Should().ContainSingle().Subject;
        record.ResultCode.Should().Be(ActionAuditResultCode.SendFailed);
        record
            .ErrorDetail.Should()
            .Contain(nameof(InvalidOperationException))
            .And.Contain("send failed");
        store.Verify(
            s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        VerifyErrorLogged(
            logger,
            "Scheduling pipeline failed",
            e => e is InvalidOperationException && e.Message == "send failed"
        );
    }

    [TestMethod]
    public async Task RunCycle_SendSuccess_CorrelationIdIsGuidAndMatchesForwardedValue()
    {
        // Arrange (AC4): capture the correlationId argument the send seam receives.
        var captured = new List<ActionAuditRecord>();
        var forwarded = new List<string?>();
        var service = ServiceReturningContext("msg-1");
        service
            .Setup(s =>
                s.SendMailAsync(
                    It.IsAny<SendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (SendMailRequest _, string? correlationId, CancellationToken _) =>
                    forwarded.Add(correlationId)
            )
            .Returns(Task.CompletedTask);
        var worker = Worker(service, Store(), AuditLog(captured), Options());

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        var record = captured.Should().ContainSingle().Subject;
        Guid.TryParse(record.CorrelationId, out _)
            .Should()
            .BeTrue("the correlation id is a worker-generated GUID");
        forwarded
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(record.CorrelationId, "the audit row's id equals the forwarded request id");
    }

    [TestMethod]
    public async Task RunCycle_AuditWriteFailure_OnSuccessPath_ContinuesAndLogsError()
    {
        // Arrange (AC3, D4): the audit sink throws; processing must continue (the dedupe
        // record is still written) and the failure must be logged at Error.
        var service = ServiceReturningContext("msg-1");
        var store = Store();
        var audit = new Mock<IActionAuditLog>();
        audit
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit down"));
        var logger = new Mock<ILogger<SchedulingWorker>>();
        var worker = Worker(service, store, audit, Options(), logger.Object);

        // Act
        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("an audit-sink fault must never break processing");
        store.Verify(
            s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        VerifyErrorLogged(
            logger,
            "Audit write failed",
            e => e is InvalidOperationException && e.Message == "audit down"
        );
    }

    [TestMethod]
    public async Task RunCycle_AuditWriteFailure_OnFailurePath_DoesNotMaskOriginalException()
    {
        // Arrange (AC3, D4): both the send and the audit write throw; the original send
        // exception must reach ProcessMessageSafelyAsync unreplaced.
        var service = ServiceReturningContext("msg-1");
        service
            .Setup(s =>
                s.SendMailAsync(
                    It.IsAny<SendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("send failed"));
        var audit = new Mock<IActionAuditLog>();
        audit
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit down"));
        var logger = new Mock<ILogger<SchedulingWorker>>();
        var worker = Worker(service, Store(), audit, Options(), logger.Object);

        // Act
        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        VerifyErrorLogged(
            logger,
            "Scheduling pipeline failed",
            e => e is InvalidOperationException && e.Message == "send failed"
        );
        VerifyErrorLogged(
            logger,
            "Audit write failed",
            e => e is InvalidOperationException && e.Message == "audit down"
        );
    }

    [TestMethod]
    public async Task RunCycle_SendSuccess_RecordedAtUtcEqualsFakeTimeProviderValue()
    {
        // Arrange (AC4): RecordedAtUtc must come from the injected TimeProvider.
        var captured = new List<ActionAuditRecord>();
        var service = ServiceReturningContext("msg-1");
        var worker = Worker(service, Store(), AuditLog(captured), Options());

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        captured
            .Should()
            .ContainSingle()
            .Which.RecordedAtUtc.Should()
            .Be(Now, "the injected FakeTimeProvider supplies the recording timestamp");
    }
}
