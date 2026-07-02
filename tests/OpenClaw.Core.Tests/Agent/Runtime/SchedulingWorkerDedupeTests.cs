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
/// Unit tests for the <see cref="SchedulingWorker"/> send-idempotency dedupe seam
/// (issue #101, AC-3/AC-4/AC-5): consult-before-send skip, record-after-success with the
/// injected clock timestamp, no-record-on-failure with per-message isolation, kill-switch
/// composition, and restart persistence over one shared in-memory SQLite database.
/// </summary>
[TestClass]
public sealed class SchedulingWorkerDedupeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);

    private static readonly string Msg1Key = SentActionKey.Build(
        "owner@contoso.com",
        "msg-1",
        SentActionKey.ProposalReply
    );

    private static AgentPolicyOptions Options(bool sendEnabled) =>
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

    private static Mock<ISentActionStore> Store(bool isRecorded)
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

    private static SchedulingWorker Worker(
        Mock<ISchedulingService> service,
        ISentActionStore sentActionStore,
        Mock<ISchedulingCandidateSource> source,
        AgentPolicyOptions options
    ) =>
        new(
            service.Object,
            sentActionStore,
            source.Object,
            Microsoft.Extensions.Options.Options.Create(options),
            new FakeTimeProvider(Now),
            NullLogger<SchedulingWorker>.Instance
        );

    [TestMethod]
    public async Task RunCycle_StoreHit_SkipsSendAndCompletesWithoutThrowing()
    {
        // Arrange: the store already has the msg-1 proposal-reply key recorded.
        var service = ServiceReturningContext("msg-1");
        var store = Store(isRecorded: true);
        var worker = Worker(service, store.Object, CandidateSource("msg-1"), Options(true));

        // Act
        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("a dedupe hit is a normal skip, not a failure");
        service.Verify(
            s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task RunCycle_StoreMiss_SendsThenRecordsKeyWithInjectedClockTimestamp()
    {
        // Arrange: capture call order via Moq callbacks so send-before-record is provable.
        var callOrder = new List<string>();
        var service = ServiceReturningContext("msg-1");
        service
            .Setup(s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("send"))
            .Returns(Task.CompletedTask);
        var store = Store(isRecorded: false);
        store
            .Setup(s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => callOrder.Add("record"))
            .Returns(Task.CompletedTask);
        var worker = Worker(service, store.Object, CandidateSource("msg-1"), Options(true));

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        store.Verify(s => s.RecordAsync(Msg1Key, Now, It.IsAny<CancellationToken>()), Times.Once);
        callOrder
            .Should()
            .Equal(["send", "record"], "the record must happen only after a successful send");
    }

    [TestMethod]
    public async Task RunCycle_SendFailure_DoesNotRecordAndProcessesNextCandidate()
    {
        // Arrange: every send throws; two candidates exercise the isolation path.
        var service = ServiceReturningContext("msg-1", "msg-2");
        service
            .Setup(s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("send failed"));
        var store = Store(isRecorded: false);
        var worker = Worker(
            service,
            store.Object,
            CandidateSource("msg-1", "msg-2"),
            Options(true)
        );

        // Act
        var act = async () => await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("per-message isolation must survive a send failure");
        store.Verify(s => s.IsRecordedAsync(Msg1Key, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(
            s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        service.Verify(
            s => s.GetSchedulingMessageAsync("msg-2", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task RunCycle_SendDisabled_NeverTouchesStoreAndNeverSends()
    {
        // Arrange: SendEnabled=false composes with dedupe — the store stays untouched.
        var service = ServiceReturningContext("msg-1");
        var store = Store(isRecorded: false);
        var worker = Worker(service, store.Object, CandidateSource("msg-1"), Options(false));

        // Act
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        store.Verify(
            s => s.IsRecordedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        store.Verify(
            s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        service.Verify(
            s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task RunCycle_TwoWorkerStorePairsOverOneDatabase_SendExactlyOnceInTotal()
    {
        // Arrange: a real CoreCacheRepository store over one shared in-memory database
        // simulates a process restart; the second worker must observe the first record.
        var connectionString =
            $"Data Source=worker-dedupe-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var service = ServiceReturningContext("msg-1");
        service
            .Setup(s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var firstStore = new OpenClaw.Core.CoreCacheRepository(connectionString);
        using var secondStore = new OpenClaw.Core.CoreCacheRepository(connectionString);
        var firstWorker = Worker(service, firstStore, CandidateSource("msg-1"), Options(true));
        var secondWorker = Worker(service, secondStore, CandidateSource("msg-1"), Options(true));

        // Act: two successive cycles process the same candidate across a "restart".
        await firstWorker.RunSchedulingCycleAsync(CancellationToken.None);
        await secondWorker.RunSchedulingCycleAsync(CancellationToken.None);

        // Assert
        service.Verify(
            s => s.SendMailAsync(It.IsAny<SendMailRequest>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
