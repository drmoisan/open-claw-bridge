using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Cache-write redaction round-trip tests for issue #18 (spec AC A6): a sensitive item scanned
/// into a real <see cref="CacheRepository"/> via <c>UpsertMessageAsync</c>/<c>UpsertEventAsync</c>
/// and read back via <c>GetMessageAsync</c>/<c>GetEventAsync</c> returns fully redacted values
/// with no <c>ResponseShaper</c> involvement in the act/assert path. Uses in-memory shared-cache
/// SQLite (no temp files) following the <c>CacheRepositoryGraphFieldsTests</c> pattern.
/// </summary>
[TestClass]
public sealed class CacheRepositorySensitivityRedactionTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private static OutlookScanner BuildScanner(BridgeSettings settings, FakeComActiveObject com) =>
        new(
            settings,
            new BridgeStateStore(settings),
            NullLogger<OutlookScanner>.Instance,
            com,
            _ => 0,
            () => FixedNow
        );

    [TestMethod]
    public async Task Scanned_sensitivity2_message_should_round_trip_redacted_through_cache()
    {
        // Arrange: a Sensitivity=2 mail item behind the fake COM scan pattern, persisting into a
        // real in-memory SQLite CacheRepository.
        using var repo = new CacheRepository(
            $"Data Source=redact-msg-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var item = new AccessRecordingSensitiveMailItem
        {
            Sensitivity = 2,
            ReceivedTime = FixedNow.AddHours(-1),
            SentOn = FixedNow.AddHours(-1).AddMinutes(-5),
            Importance = 2,
            Unread = true,
            HasAttachments = true,
        };
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(item);
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[6] = inbox;
        var com = new FakeComActiveObject { RunningObject = outlook };
        var scanner = BuildScanner(BridgeSettings.Default, com);

        // Act: scan writes through UpsertMessageAsync; read back directly via GetMessageAsync.
        await scanner.ScanInboxAsync(repo);
        var loaded = await repo.GetMessageAsync(
            BridgeIdCodec.MessageId("entry-sensitive-mail", false)
        );

        // Assert: the stored row is fully redacted with no shaping involved.
        loaded.Should().NotBeNull();
        loaded!.Subject.Should().Be("Private message");
        loaded.SenderName.Should().BeNull();
        loaded.SenderEmail.Should().BeNull();
        loaded.SenderEmailResolved.Should().BeNull();
        loaded.FromEmailAddress.Should().BeNull();
        loaded.ToJson.Should().BeNull();
        loaded.CcJson.Should().BeNull();
        loaded.BodyPreview.Should().BeNull();
        loaded.IsRedacted.Should().BeTrue();
        loaded.ProtectedFieldsAvailable.Should().BeFalse();
        loaded.Sensitivity.Should().Be(2);
        loaded.ConversationId.Should().Be("conv-sensitive");
    }

    [TestMethod]
    public async Task Scanned_sensitivity3_event_should_round_trip_redacted_through_cache()
    {
        // Arrange: a Sensitivity=3 appointment behind the fake COM scan pattern, persisting into
        // a real in-memory SQLite CacheRepository.
        using var repo = new CacheRepository(
            $"Data Source=redact-evt-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var start = FixedNow.AddDays(1);
        var item = new AccessRecordingSensitiveAppointmentItem
        {
            Sensitivity = 3,
            Start = start,
            End = start.AddHours(1),
            IsRecurring = true,
            BusyStatus = 2,
            MeetingStatus = 1,
            ResponseStatus = 1,
        };
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(item);
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[9] = calendar;
        var com = new FakeComActiveObject { RunningObject = outlook };
        var settings = BridgeSettings.Default with
        {
            CalendarPastDays = 7,
            CalendarFutureDays = 30,
        };
        var scanner = BuildScanner(settings, com);

        // Act: scan writes through UpsertEventAsync; read back directly via GetEventAsync.
        await scanner.ScanCalendarAsync(repo);
        var loaded = await repo.GetEventAsync(
            BridgeIdCodec.EventId("gid-sensitive-appt", "entry-sensitive-appt", start)
        );

        // Assert: the stored row is fully redacted; Categories round-trips as an empty array.
        loaded.Should().NotBeNull();
        loaded!.Subject.Should().Be("Private appointment");
        loaded.Location.Should().BeNull();
        loaded.Organizer.Should().BeNull();
        loaded.RequiredAttendeesJson.Should().BeNull();
        loaded.OptionalAttendeesJson.Should().BeNull();
        loaded.ResourcesJson.Should().BeNull();
        loaded.BodyPreview.Should().BeNull();
        loaded.BodyFull.Should().BeNull();
        loaded.Categories.Should().NotBeNull();
        loaded.Categories.Should().BeEmpty("redacted categories round-trip as an empty array");
        loaded.IsRedacted.Should().BeTrue();
        loaded.ProtectedFieldsAvailable.Should().BeFalse();
        loaded.Sensitivity.Should().Be(3);
        loaded.SensitivityLabel.Should().Be("confidential");
        loaded.StartUtc.Should().Be(start);
        loaded.EndUtc.Should().Be(start.AddHours(1));
        loaded.BusyStatus.Should().Be(2);
    }
}
