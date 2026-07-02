using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Edge-path scanner-level sensitivity-redaction tests (issue #18 remediation cycle 1, Fix 1):
/// sensitive meeting-message normalization (<c>IPM.Schedule.Meeting.Request</c>), the
/// null-<c>MessageClass</c> fallback in <c>IsMeetingItem</c>, the <c>Attachments</c> true
/// short-circuit, and hard never-ingest scans with <c>ThrowOnProtectedAccess</c> enabled.
/// Uses the shared COM-double scan pattern (no live COM, no temp files, deterministic clock).
/// </summary>
[TestClass]
public sealed class OutlookScannerSensitivityNormalizationEdgeTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FixedReceived = FixedNow.AddHours(-2);
    private static readonly DateTime FixedModified = new(2026, 6, 30, 8, 0, 0, DateTimeKind.Utc);

    private static OutlookScanner BuildScanner(
        BridgeSettings settings,
        FakeComActiveObject com,
        ILogger<OutlookScanner>? logger = null
    ) =>
        new(
            settings,
            new BridgeStateStore(settings),
            logger ?? NullLogger<OutlookScanner>.Instance,
            com,
            _ => 0,
            () => FixedNow
        );

    private static async Task<MessageDto> ScanSingleMessageAsync(
        AccessRecordingSensitiveMailItem item
    )
    {
        var settings = BridgeSettings.Default;
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(item);
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[6] = inbox;
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings, com);

        await scanner.ScanInboxAsync(repo);

        repo.Messages.Should().HaveCount(1);
        return repo.Messages.Values.Single();
    }

    private static async Task<EventDto> ScanSingleEventAsync(
        AccessRecordingSensitiveAppointmentItem item
    )
    {
        var settings = BridgeSettings.Default with
        {
            CalendarPastDays = 7,
            CalendarFutureDays = 30,
        };
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(item);
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[9] = calendar;
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings, com);

        await scanner.ScanCalendarAsync(repo);

        repo.Events.Should().HaveCount(1);
        return repo.Events.Values.Single();
    }

    private static AccessRecordingSensitiveMailItem CreateMailItem(
        int? sensitivity,
        string? messageClass = "IPM.Note",
        int? meetingType = null,
        bool attachments = false,
        bool hasAttachments = true,
        bool throwOnProtectedAccess = false
    ) =>
        new()
        {
            Sensitivity = sensitivity,
            MessageClass = messageClass,
            MeetingType = meetingType,
            Attachments = attachments,
            HasAttachments = hasAttachments,
            ThrowOnProtectedAccess = throwOnProtectedAccess,
            ReceivedTime = FixedReceived,
            SentOn = FixedReceived.AddMinutes(-5),
            Importance = 2,
            Unread = true,
        };

    private static void AssertFullMessageRedactionDisposition(MessageDto dto)
    {
        dto.Subject.Should().Be("Private message");
        dto.SenderName.Should().BeNull();
        dto.SenderEmail.Should().BeNull();
        dto.SenderEmailResolved.Should().BeNull();
        dto.FromEmailAddress.Should().BeNull();
        dto.ToJson.Should().BeNull();
        dto.CcJson.Should().BeNull();
        dto.BodyPreview.Should().BeNull();
        dto.IsRedacted.Should().BeTrue();
        dto.ProtectedFieldsAvailable.Should().BeFalse();
    }

    private static void AssertNoProtectedAccess(AccessRecordingSensitiveMailItem item) =>
        item
            .ProtectedMemberWasAccessed.Should()
            .BeFalse(
                "never-ingest: protected members accessed were {0}",
                string.Join(", ", item.ProtectedAccesses)
            );

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Sensitive_meeting_message_should_be_redacted_with_meeting_kind(
        int sensitivity
    )
    {
        var item = CreateMailItem(
            sensitivity,
            messageClass: "IPM.Schedule.Meeting.Request",
            meetingType: 1
        );

        var dto = await ScanSingleMessageAsync(item);

        AssertFullMessageRedactionDisposition(dto);
        dto.ItemKind.Should().Be("meeting");
        dto.BridgeId.Should().Be(BridgeIdCodec.MessageId("entry-sensitive-mail", true));
        dto.MeetingMessageType.Should().Be(1);
        AssertNoProtectedAccess(item);
    }

    [TestMethod]
    public async Task Sensitive_message_with_null_message_class_should_fall_back_to_mail_kind()
    {
        var item = CreateMailItem(2, messageClass: null);

        var dto = await ScanSingleMessageAsync(item);

        AssertFullMessageRedactionDisposition(dto);
        dto.ItemKind.Should().Be("mail");
        dto.MessageClass.Should().BeNull();
        AssertNoProtectedAccess(item);
    }

    [TestMethod]
    public async Task Sensitive_message_with_attachments_member_true_should_set_has_attachments()
    {
        var item = CreateMailItem(2, attachments: true, hasAttachments: false);

        var dto = await ScanSingleMessageAsync(item);

        AssertFullMessageRedactionDisposition(dto);
        dto.HasAttachments.Should().BeTrue();
    }

    [TestMethod]
    public async Task Sensitive_mail_scan_with_throwing_protected_members_should_stay_redacted()
    {
        var item = CreateMailItem(2, throwOnProtectedAccess: true);

        var dto = await ScanSingleMessageAsync(item);

        AssertFullMessageRedactionDisposition(dto);
        AssertNoProtectedAccess(item);
    }

    [TestMethod]
    public async Task Sensitive_event_scan_with_throwing_protected_members_should_stay_redacted()
    {
        var item = new AccessRecordingSensitiveAppointmentItem
        {
            Sensitivity = 3,
            ThrowOnProtectedAccess = true,
            Start = FixedNow.AddDays(1),
            End = FixedNow.AddDays(1).AddHours(1),
            IsRecurring = true,
            BusyStatus = 2,
            MeetingStatus = 1,
            ResponseStatus = 1,
            RecurrenceState = 2,
            IsOnlineMeeting = true,
            AllowNewTimeProposal = true,
            LastModificationTime = FixedModified,
        };

        var dto = await ScanSingleEventAsync(item);

        dto.Subject.Should().Be("Private appointment");
        dto.Location.Should().BeNull();
        dto.Organizer.Should().BeNull();
        dto.RequiredAttendeesJson.Should().BeNull();
        dto.OptionalAttendeesJson.Should().BeNull();
        dto.ResourcesJson.Should().BeNull();
        dto.BodyPreview.Should().BeNull();
        dto.BodyFull.Should().BeNull();
        dto.Categories.Should().NotBeNull();
        dto.Categories.Should().BeEmpty();
        dto.IsRedacted.Should().BeTrue();
        dto.ProtectedFieldsAvailable.Should().BeFalse();
        item.ProtectedMemberWasAccessed.Should()
            .BeFalse(
                "hard never-ingest: protected members accessed were {0}",
                string.Join(", ", item.ProtectedAccesses)
            );
    }
}
