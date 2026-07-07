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

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Worker unit tests for the organizer-reschedule path (issue #128): the four-row gate
/// truth table (a), dry-run detail (b), and guard-before-gate ordering (c). All time comes
/// from <see cref="FakeTimeProvider"/>. The shared harness helpers are reused by
/// <see cref="SchedulingWorkerRescheduleEdgeTests"/> (rows d-h) to keep each file under the
/// 500-line cap. The mocked Graph write is exercised through the
/// <see cref="ISchedulingService"/> seam.
/// </summary>
[TestClass]
public sealed class SchedulingWorkerRescheduleTests
{
    // Monday 2026-06-08; the recurring event is on Wednesday 2026-06-10 (pre-move start).
    internal static readonly DateTimeOffset Now = new(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset EventStart = new(2026, 6, 10, 15, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset EventEnd = new(2026, 6, 10, 15, 30, 0, TimeSpan.Zero);

    internal static AgentPolicyOptions Options(
        bool sendEnabled = false,
        bool calendarWriteEnabled = false,
        bool enableOrganizerReschedule = false
    ) =>
        new()
        {
            InternalDomains = new[] { "contoso.com" },
            InternalDomain = "contoso.com",
            SendEnabled = sendEnabled,
            CalendarWriteEnabled = calendarWriteEnabled,
            EnableOrganizerReschedule = enableOrganizerReschedule,
            CalendarViewFallbackDays = 0,
            NoMeetingBlocks = Array.Empty<string>(),
            MinNoticeMinutes = 0,
            PreferredDays = Array.Empty<string>(),
        };

    internal static SchedulingMessageDto Message(string id = "msg-1") =>
        new(
            Id: id,
            Subject: "Weekly 1:1",
            BodyPreview: "Can we move this?",
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

    /// <summary>
    /// A recurring organizer-owned 1:1: the Graph <c>isOrganizer</c> flag is authoritative
    /// for reschedule eligibility, while the classifier reads the attendee structure — the
    /// owner is the only non-organizer attendee, so it classifies as ONE_ON_ONE and the
    /// move guard governs the decision.
    /// </summary>
    internal static SchedulingEventDto OneOnOneEvent(
        bool isOrganizer = true,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string? id = "evt-1",
        string? seriesMasterId = "master-1"
    ) =>
        new(
            Id: id,
            ICalUId: "ical-1",
            SeriesMasterId: seriesMasterId,
            Subject: "Weekly 1:1",
            BodyPreview: null,
            BodyContent: null,
            BodyContentType: null,
            Organizer: new AttendeeDto("Colleague", "colleague@contoso.com"),
            RequiredAttendees: new[] { new AttendeeDto("Owner", "owner@contoso.com") },
            OptionalAttendees: Array.Empty<AttendeeDto>(),
            ResourceAttendees: Array.Empty<AttendeeDto>(),
            Categories: Array.Empty<string>(),
            IsOrganizer: isOrganizer,
            IsOnlineMeeting: false,
            AllowNewTimeProposals: true,
            Sensitivity: "normal",
            Start: start ?? EventStart,
            StartTimeZone: null,
            End: end ?? EventEnd,
            EndTimeZone: null,
            LastModifiedDateTime: null,
            Type: null
        );

    internal static MailboxSettingsDto MailboxSettings() =>
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

    /// <summary>Builds the scheduling-service mock; the event and free/busy are configurable.</summary>
    internal static Mock<ISchedulingService> Service(
        SchedulingEventDto? meetingEvent,
        FreeBusyScheduleDto? freeBusy = null
    )
    {
        var service = new Mock<ISchedulingService>();
        service
            .Setup(s => s.GetSchedulingMessageAsync("msg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Message());
        service
            .Setup(s => s.GetEventForMessageAsync("msg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(meetingEvent);
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
                freeBusy
                    ?? new FreeBusyScheduleDto("owner@contoso.com", Array.Empty<BusyIntervalDto>())
            );
        return service;
    }

    internal static Mock<ISchedulingCandidateSource> CandidateSource(params string[] ids)
    {
        var source = new Mock<ISchedulingCandidateSource>();
        source
            .Setup(s => s.GetCandidateMessageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);
        return source;
    }

    internal static Mock<ISeriesMoveHistory> MoveHistory(
        IReadOnlyList<DateTimeOffset>? movedStarts = null
    )
    {
        var history = new Mock<ISeriesMoveHistory>();
        history
            .Setup(h =>
                h.GetMovedOccurrenceStartsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(movedStarts ?? Array.Empty<DateTimeOffset>());
        history
            .Setup(h =>
                h.RecordMoveAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        return history;
    }

    internal static Mock<ISentActionStore> Store(bool isRecorded = false)
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

    internal static Mock<IActionAuditLog> Audit(List<ActionAuditRecord> captured)
    {
        var audit = new Mock<IActionAuditLog>();
        audit
            .Setup(a => a.RecordAsync(Capture.In(captured), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return audit;
    }

    internal static SchedulingWorker Worker(
        Mock<ISchedulingService> service,
        Mock<ISentActionStore> store,
        Mock<IActionAuditLog> audit,
        Mock<ISeriesMoveHistory> history,
        AgentPolicyOptions options
    ) =>
        new(
            service.Object,
            store.Object,
            audit.Object,
            CandidateSource("msg-1").Object,
            history.Object,
            Microsoft.Extensions.Options.Options.Create(options),
            new FakeTimeProvider(Now),
            NullLogger<SchedulingWorker>.Instance
        );

    internal static ActionAuditRecord RescheduleRecord(List<ActionAuditRecord> captured) =>
        captured
            .Should()
            .ContainSingle(r => r.ActionType == SentActionKey.OrganizerReschedule)
            .Subject;

    // ---- (a) gate truth table ----

    [DataTestMethod]
    [DataRow(false, false, DisplayName = "both off")]
    [DataRow(false, true, DisplayName = "kill switch off")]
    [DataRow(true, false, DisplayName = "path flag off")]
    public async Task GateTruthTable_DisabledRow_DryRunDisabledWithNoWrite(
        bool calendarWriteEnabled,
        bool enableOrganizerReschedule
    )
    {
        var captured = new List<ActionAuditRecord>();
        var service = Service(OneOnOneEvent());
        var history = MoveHistory();
        var store = Store();
        var worker = Worker(
            service,
            store,
            Audit(captured),
            history,
            Options(
                calendarWriteEnabled: calendarWriteEnabled,
                enableOrganizerReschedule: enableOrganizerReschedule
            )
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        RescheduleRecord(captured).ResultCode.Should().Be(ActionAuditResultCode.RescheduleDisabled);
        service.Verify(
            s =>
                s.RescheduleEventAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        history.Verify(
            h =>
                h.RecordMoveAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
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
    }

    [TestMethod]
    public async Task GateTruthTable_BothFlagsOn_WritesExactlyOnce()
    {
        var captured = new List<ActionAuditRecord>();
        var service = Service(OneOnOneEvent());
        service
            .Setup(s =>
                s.RescheduleEventAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        var worker = Worker(
            service,
            Store(),
            Audit(captured),
            MoveHistory(),
            Options(calendarWriteEnabled: true, enableOrganizerReschedule: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        service.Verify(
            s =>
                s.RescheduleEventAsync(
                    "evt-1",
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ---- (b) dry-run detail ----

    [TestMethod]
    public async Task DryRun_Disabled_CarriesFourTimeColumnsAndRescheduleActingFlags()
    {
        var captured = new List<ActionAuditRecord>();
        var worker = Worker(
            Service(OneOnOneEvent()),
            Store(),
            Audit(captured),
            MoveHistory(),
            Options(calendarWriteEnabled: true, enableOrganizerReschedule: false)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        var record = RescheduleRecord(captured);
        record.ResultCode.Should().Be(ActionAuditResultCode.RescheduleDisabled);
        record.EventId.Should().Be("evt-1");
        record.OriginalStartUtc.Should().Be(EventStart);
        record.OriginalEndUtc.Should().Be(EventEnd);
        record.NewStartUtc.Should().NotBeNull();
        record.NewEndUtc.Should().NotBeNull();
        // Duration preserved from the original event.
        (record.NewEndUtc - record.NewStartUtc)
            .Should()
            .Be(EventEnd - EventStart);
        record
            .ActingFlags.Should()
            .Be(
                "CalendarWriteEnabled=True;EnableOrganizerReschedule=False",
                "the reschedule path uses its own ActingFlags snapshot"
            );
    }

    // ---- (c) guard block before gate ----

    [TestMethod]
    public async Task GuardBlock_BothFlagsOn_AuditsBlockedWithNoWrite()
    {
        // Two prior moves anchored to the pre-move occurrence date exhaust the 1:1 rolling
        // budget (< 2), so the guard blocks even with both flags on.
        var captured = new List<ActionAuditRecord>();
        var service = Service(OneOnOneEvent());
        var history = MoveHistory(new[] { EventStart, EventStart.AddHours(1) });
        var worker = Worker(
            service,
            Store(),
            Audit(captured),
            history,
            Options(calendarWriteEnabled: true, enableOrganizerReschedule: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        RescheduleRecord(captured).ResultCode.Should().Be(ActionAuditResultCode.RescheduleBlocked);
        service.Verify(
            s =>
                s.RescheduleEventAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "the guard blocks before the flag gate"
        );
    }
}
