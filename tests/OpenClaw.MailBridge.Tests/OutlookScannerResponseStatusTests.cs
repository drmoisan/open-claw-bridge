using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Targeted regression tests for issue #45, acceptance criterion AC-4: the
/// <see cref="OutlookScanner"/> must populate <see cref="EventDto.ResponseStatus"/>
/// from the underlying Outlook COM <c>AppointmentItem.ResponseStatus</c> value,
/// and must swallow per-event COM read errors so a malformed item does not fail
/// the scan. Uses the shared <c>FakeAppointmentItem</c> double (extended in this
/// plan to carry the nullable <c>ResponseStatus</c> field).
/// </summary>
[TestClass]
public sealed class OutlookScannerResponseStatusTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 4, 22, 12, 0, 0, TimeSpan.Zero);

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
    public async Task ScanCalendarAsync_should_populate_ResponseStatus_from_com_when_value_is_accepted()
    {
        // Arrange
        var settings = BridgeSettings.Default with
        {
            CalendarPastDays = 7,
            CalendarFutureDays = 30,
        };
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(
            new FakeAppointmentItem
            {
                EntryID = "entry-rs-accepted",
                GlobalAppointmentID = "gid-rs-accepted",
                Subject = "AC-4 Accepted",
                Start = FixedNow.AddDays(1),
                End = FixedNow.AddDays(1).AddHours(1),
                ResponseStatus = 3,
            }
        );
        var com = new FakeComActiveObject { RunningObject = BuildOutlookWithCalendar(calendar) };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings, com);

        // Act
        await scanner.ScanCalendarAsync(repo);

        // Assert
        repo.Events.Should().HaveCount(1);
        var evt = repo.Events.Values.Single();
        evt.ResponseStatus.Should()
            .Be(3, "scanner must carry the ResponseStatus COM value through to the EventDto");
    }

    [TestMethod]
    public async Task ScanCalendarAsync_should_set_ResponseStatus_to_null_when_com_property_is_absent_and_should_not_fail_the_scan()
    {
        // Arrange: an appointment where the ResponseStatus member is null on the fake
        // source. GetOptionalInt swallows the reflection/COM read to null and the scan
        // continues normally.
        var settings = BridgeSettings.Default with
        {
            CalendarPastDays = 7,
            CalendarFutureDays = 30,
        };
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(
            new FakeAppointmentItem
            {
                EntryID = "entry-rs-null",
                GlobalAppointmentID = "gid-rs-null",
                Subject = "AC-4 Null",
                Start = FixedNow.AddDays(2),
                End = FixedNow.AddDays(2).AddHours(1),
                ResponseStatus = null,
            }
        );
        var com = new FakeComActiveObject { RunningObject = BuildOutlookWithCalendar(calendar) };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings, com);

        // Act
        await scanner.ScanCalendarAsync(repo);

        // Assert: scan completed (event was persisted) and ResponseStatus is null.
        repo.Events.Should().HaveCount(1);
        var evt = repo.Events.Values.Single();
        evt.ResponseStatus.Should().BeNull("null COM value must pass through as null");
    }
}
