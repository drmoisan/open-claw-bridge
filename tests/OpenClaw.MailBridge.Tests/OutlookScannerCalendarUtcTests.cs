using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Integration-level regression test for the UTC double-shift bug (issue #45):
/// verifies that <see cref="OutlookScanner"/> stores the correct UTC timestamp when
/// Outlook COM's <c>StartUTC</c>/<c>EndUTC</c> properties return
/// <see cref="DateTime"/> values with <see cref="DateTimeKind.Unspecified"/>.
/// </summary>
[TestClass]
public sealed class OutlookScannerCalendarUtcTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    private static OutlookScanner BuildScanner(BridgeSettings settings, FakeComActiveObject com) =>
        new(
            settings,
            new BridgeStateStore(settings),
            NullLogger<OutlookScanner>.Instance,
            com,
            _ => 0,
            () => FixedNow
        );

    private static FakeOutlookApplication BuildOutlookWithCalendar(FakeOutlookFolder calendar)
    {
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[9] = calendar;
        outlook.Namespace.DefaultFolders[6] = new FakeOutlookFolder();
        return outlook;
    }

    [TestMethod]
    public async Task ScanCalendarAsync_StartUTC_UnspecifiedKind_stores_correct_utc()
    {
        // Arrange — Outlook COM returns Unspecified-kind DateTime carrying a UTC value.
        // On EDT (UTC-4) a naive ToUniversalTime() call would shift this to 21:00 UTC (wrong).
        // GetOptionalUtcDateTimeOffset must treat Unspecified as already UTC and return 17:00 UTC.
        var settings = BridgeSettings.Default with
        {
            CalendarPastDays = 7,
            CalendarFutureDays = 30,
        };
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(
            new FakeAppointmentItem
            {
                EntryID = "entry-utc-unspecified",
                GlobalAppointmentID = "gid-utc-unspecified",
                Subject = "UTC Double-Shift Regression",
                Start = FixedNow.AddDays(1),
                End = FixedNow.AddDays(1).AddHours(1),
                StartUTC = new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Unspecified),
                EndUTC = new DateTime(2026, 4, 27, 18, 0, 0, DateTimeKind.Unspecified),
            }
        );
        var com = new FakeComActiveObject { RunningObject = BuildOutlookWithCalendar(calendar) };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings, com);

        // Act
        await scanner.ScanCalendarAsync(repo);

        // Assert — StartUtc must be 17:00 UTC with zero offset, not shifted by local timezone.
        repo.Events.Should().HaveCount(1);
        var evt = repo.Events.Values.Single();
        evt.StartUtc.UtcDateTime.Should()
            .Be(
                new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc),
                "Unspecified-kind UTC value from Outlook COM must not be double-shifted"
            );
        evt.StartUtc.Offset.Should().Be(TimeSpan.Zero);
    }
}
