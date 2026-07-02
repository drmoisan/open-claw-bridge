using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Scanner-level sensitivity-redaction tests for issue #18 (spec Group A): normalization of
/// <c>Sensitivity</c> 2/3 items produces fully redacted DTOs, never accesses protected COM
/// members (asserted via the access-recording doubles in
/// <c>SensitivityRedactionTestDoubles.cs</c>), leaves boundary values untouched, and logs each
/// redaction by bridge id only. Uses the shared COM-double scan pattern (no live COM, no temp
/// files, deterministic clock).
/// </summary>
[TestClass]
public sealed class OutlookScannerSensitivityNormalizationTests
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
        AccessRecordingSensitiveMailItem item,
        ILogger<OutlookScanner>? logger = null
    )
    {
        var settings = BridgeSettings.Default;
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(item);
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[6] = inbox;
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings, com, logger);

        await scanner.ScanInboxAsync(repo);

        repo.Messages.Should().HaveCount(1);
        return repo.Messages.Values.Single();
    }

    private static async Task<EventDto> ScanSingleEventAsync(
        AccessRecordingSensitiveAppointmentItem item,
        ILogger<OutlookScanner>? logger = null
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
        var scanner = BuildScanner(settings, com, logger);

        await scanner.ScanCalendarAsync(repo);

        repo.Events.Should().HaveCount(1);
        return repo.Events.Values.Single();
    }

    private static AccessRecordingSensitiveMailItem CreateMailItem(int? sensitivity) =>
        new()
        {
            Sensitivity = sensitivity,
            ReceivedTime = FixedReceived,
            SentOn = FixedReceived.AddMinutes(-5),
            Importance = 2,
            Unread = true,
            HasAttachments = true,
        };

    private static AccessRecordingSensitiveAppointmentItem CreateAppointmentItem(
        int? sensitivity
    ) =>
        new()
        {
            Sensitivity = sensitivity,
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

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Sensitive_message_should_be_fully_redacted(int sensitivity)
    {
        var dto = await ScanSingleMessageAsync(CreateMailItem(sensitivity));

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

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Sensitive_message_should_retain_mechanical_fields(int sensitivity)
    {
        var dto = await ScanSingleMessageAsync(CreateMailItem(sensitivity));

        dto.BridgeId.Should().Be(BridgeIdCodec.MessageId("entry-sensitive-mail", false));
        dto.ItemKind.Should().Be("mail");
        dto.MessageClass.Should().Be("IPM.Note");
        dto.ReceivedUtc.Should().Be(FixedReceived);
        dto.SentUtc.Should().Be(FixedReceived.AddMinutes(-5));
        dto.Importance.Should().Be(2);
        dto.Sensitivity.Should().Be(sensitivity);
        dto.Unread.Should().BeTrue();
        dto.HasAttachments.Should().BeTrue();
        dto.ConversationId.Should().Be("conv-sensitive");
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Sensitive_event_should_be_fully_redacted(int sensitivity)
    {
        var dto = await ScanSingleEventAsync(CreateAppointmentItem(sensitivity));

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
    }

    [TestMethod]
    [DataRow(2, "private")]
    [DataRow(3, "confidential")]
    public async Task Sensitive_event_should_retain_mechanical_fields(
        int sensitivity,
        string expectedLabel
    )
    {
        var dto = await ScanSingleEventAsync(CreateAppointmentItem(sensitivity));

        dto.BridgeId.Should()
            .Be(
                BridgeIdCodec.EventId(
                    "gid-sensitive-appt",
                    "entry-sensitive-appt",
                    FixedNow.AddDays(1)
                )
            );
        dto.GlobalAppointmentId.Should().Be("gid-sensitive-appt");
        dto.StartUtc.Should().Be(FixedNow.AddDays(1));
        dto.EndUtc.Should().Be(FixedNow.AddDays(1).AddHours(1));
        dto.BusyStatus.Should().Be(2);
        dto.MeetingStatus.Should().Be(1);
        dto.IsRecurring.Should().BeTrue();
        dto.Sensitivity.Should().Be(sensitivity);
        dto.SensitivityLabel.Should().Be(expectedLabel);
        dto.ResponseStatus.Should().Be(1);
        dto.IsOrganizer.Should().BeTrue();
        dto.IsOnlineMeeting.Should().BeTrue();
        dto.AllowNewTimeProposals.Should().BeTrue();
        dto.ICalUId.Should().Be("gid-sensitive-appt");
        dto.SeriesMasterId.Should().Be("gid-sensitive-appt");
        dto.LastModifiedDateTime.Should().Be(new DateTimeOffset(FixedModified));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Sensitive_message_normalization_should_never_access_protected_members(
        int sensitivity
    )
    {
        var item = CreateMailItem(sensitivity);

        await ScanSingleMessageAsync(item);

        item.ProtectedMemberWasAccessed.Should()
            .BeFalse(
                "never-ingest: protected members accessed were {0}",
                string.Join(", ", item.ProtectedAccesses)
            );
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Sensitive_event_normalization_should_never_access_protected_members(
        int sensitivity
    )
    {
        var item = CreateAppointmentItem(sensitivity);

        await ScanSingleEventAsync(item);

        item.ProtectedMemberWasAccessed.Should()
            .BeFalse(
                "never-ingest: protected members accessed were {0}",
                string.Join(", ", item.ProtectedAccesses)
            );
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(null)]
    [DataRow(-1)]
    [DataRow(4)]
    [DataRow(99)]
    public async Task Boundary_sensitivity_message_should_stay_unredacted(int? sensitivity)
    {
        var dto = await ScanSingleMessageAsync(CreateMailItem(sensitivity));

        dto.IsRedacted.Should().BeFalse();
        dto.Subject.Should().Be("Secret subject");
        dto.SenderName.Should().Be("Secret Sender");
        dto.SenderEmail.Should().Be("secret.sender@example.com");
        dto.SenderEmailResolved.Should().Be("secret.sender@example.com");
        dto.BodyPreview.Should().Be("Secret body content");
        dto.ProtectedFieldsAvailable.Should().BeTrue();
        dto.Sensitivity.Should().Be(sensitivity);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(null)]
    [DataRow(-1)]
    [DataRow(4)]
    [DataRow(99)]
    public async Task Boundary_sensitivity_event_should_stay_unredacted(int? sensitivity)
    {
        var dto = await ScanSingleEventAsync(CreateAppointmentItem(sensitivity));

        dto.IsRedacted.Should().BeFalse();
        dto.Subject.Should().Be("Secret meeting subject");
        dto.Location.Should().Be("Secret Room");
        dto.Organizer.Should().Be("Secret Organizer");
        dto.Categories.Should().Equal("Secret Category");
        dto.BodyPreview.Should().Be("Secret agenda");
        dto.BodyFull.Should().Be("Secret agenda");
        dto.ProtectedFieldsAvailable.Should().BeTrue();
        dto.Sensitivity.Should().Be(sensitivity);
    }

    [TestMethod]
    public async Task Message_redaction_should_log_bridge_id_only_at_information_level()
    {
        var logger = new CapturingScannerLogger();
        var dto = await ScanSingleMessageAsync(CreateMailItem(2), logger);

        var redactionEntries = logger
            .Entries.Where(entry => entry.Message.Contains(dto.BridgeId, StringComparison.Ordinal))
            .ToList();
        redactionEntries.Should().HaveCount(1);
        redactionEntries[0].Level.Should().Be(LogLevel.Information);
        AssertNoProtectedContent(
            logger,
            "Secret subject",
            "Secret Sender",
            "secret.sender@example.com",
            "Secret body content"
        );
    }

    [TestMethod]
    public async Task Event_redaction_should_log_bridge_id_only_at_information_level()
    {
        var logger = new CapturingScannerLogger();
        var dto = await ScanSingleEventAsync(CreateAppointmentItem(3), logger);

        var redactionEntries = logger
            .Entries.Where(entry => entry.Message.Contains(dto.BridgeId, StringComparison.Ordinal))
            .ToList();
        redactionEntries.Should().HaveCount(1);
        redactionEntries[0].Level.Should().Be(LogLevel.Information);
        AssertNoProtectedContent(
            logger,
            "Secret meeting subject",
            "Secret Organizer",
            "Secret Room",
            "Secret Category",
            "Secret agenda"
        );
    }

    private static void AssertNoProtectedContent(
        CapturingScannerLogger logger,
        params string[] protectedValues
    )
    {
        foreach (var entry in logger.Entries)
        {
            foreach (var value in protectedValues)
            {
                entry
                    .Message.Contains(value, StringComparison.OrdinalIgnoreCase)
                    .Should()
                    .BeFalse(
                        "log line '{0}' must not contain protected value '{1}'",
                        entry.Message,
                        value
                    );
            }
        }
    }

    /// <summary>Capturing <see cref="ILogger{TCategoryName}"/> double for log assertions.</summary>
    private sealed class CapturingScannerLogger : ILogger<OutlookScanner>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
