using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Regression tests for the calendar overlap-filter bug (issue #19): the Restrict filter built by
/// <c>OutlookScanner.BuildCalendarFilter</c> must use interval-overlap semantics
/// (<c>[Start] &lt; windowEnd AND [End] &gt; windowStart</c>) so in-progress and window-spanning
/// events are included in the calendar scan, while preserving the
/// <c>MM/dd/yyyy hh:mm tt</c> local-time formatting established for issue #55.
/// </summary>
[TestClass]
public sealed class OutlookScannerCalendarOverlapFilterTests
{
    private const int CalendarPastDays = 7;
    private const int CalendarFutureDays = 30;

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

    /// <summary>
    /// Runs a calendar scan against the fake Outlook doubles and returns the Restrict filter
    /// string captured by <see cref="FakeOutlookItems.Restrict"/>.
    /// </summary>
    private static async Task<string> CaptureEmittedFilterAsync()
    {
        var settings = BridgeSettings.Default with
        {
            CalendarPastDays = CalendarPastDays,
            CalendarFutureDays = CalendarFutureDays,
        };
        var calendar = new FakeOutlookFolder();
        var com = new FakeComActiveObject { RunningObject = BuildOutlookWithCalendar(calendar) };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings, com);

        await scanner.ScanCalendarAsync(repo);

        calendar.Items.LastFilter.Should().NotBeNull("the calendar scan must call Restrict");
        return calendar.Items.LastFilter!;
    }

    /// <summary>
    /// Verifies the exact Restrict string: interval-overlap predicate with local-time boundaries
    /// formatted <c>MM/dd/yyyy hh:mm tt</c> (issue #55 formatting preserved).
    /// </summary>
    [TestMethod]
    public async Task ScanCalendarAsync_emits_interval_overlap_restrict_filter()
    {
        // Arrange
        var windowStartLocal = FixedNow.AddDays(-CalendarPastDays).LocalDateTime;
        var windowEndLocal = FixedNow.AddDays(CalendarFutureDays).LocalDateTime;
        var expected = string.Create(
            CultureInfo.InvariantCulture,
            $"[Start] < '{windowEndLocal:MM/dd/yyyy hh:mm tt}' AND [End] > '{windowStartLocal:MM/dd/yyyy hh:mm tt}'"
        );

        // Act
        var filter = await CaptureEmittedFilterAsync();

        // Assert
        filter
            .Should()
            .Be(
                expected,
                "the calendar Restrict filter must use interval-overlap semantics (issue #19)"
            );
    }

    /// <summary>
    /// Evaluates event membership against the emitted filter for the five boundary scenarios:
    /// fully-within, in-progress, window-spanning (all included), and the two strict-boundary
    /// exclusions (<c>End == windowStart</c>, <c>Start == windowEnd</c>).
    /// Offsets are whole/fractional days relative to the window start; the window spans
    /// <c>CalendarPastDays + CalendarFutureDays</c> = 37 days.
    /// </summary>
    [DataTestMethod]
    [DataRow(1.0, 1.5, true, "an event fully within the window must be included")]
    [DataRow(
        -1.0,
        0.5,
        true,
        "an in-progress event starting before the window and ending inside it must be included"
    )]
    [DataRow(-1.0, 38.0, true, "an event spanning the entire window must be included")]
    [DataRow(
        -1.0,
        0.0,
        false,
        "an event ending exactly at windowStart must be excluded (strict boundary)"
    )]
    [DataRow(
        37.0,
        38.0,
        false,
        "an event starting exactly at windowEnd must be excluded (strict boundary)"
    )]
    public async Task ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics(
        double startOffsetDays,
        double endOffsetDays,
        bool expectedIncluded,
        string scenario
    )
    {
        // Arrange
        var windowStartLocal = FixedNow.AddDays(-CalendarPastDays).LocalDateTime;
        var eventStart = windowStartLocal.AddDays(startOffsetDays);
        var eventEnd = windowStartLocal.AddDays(endOffsetDays);

        // Act
        var filter = await CaptureEmittedFilterAsync();
        var included = EvaluateFilter(filter, eventStart, eventEnd);

        // Assert
        included.Should().Be(expectedIncluded, scenario);
    }

    /// <summary>
    /// Parses the two-clause Restrict filter and returns whether an event with the given
    /// local start/end satisfies both clauses.
    /// </summary>
    private static bool EvaluateFilter(string filter, DateTime eventStart, DateTime eventEnd)
    {
        var clauses = filter.Split(" AND ", StringSplitOptions.None);
        clauses.Should().HaveCount(2, "the calendar filter must contain exactly two clauses");
        return clauses.All(clause => EvaluateClause(clause, eventStart, eventEnd));
    }

    /// <summary>
    /// Parses a single clause of the form <c>[Field] op 'MM/dd/yyyy hh:mm tt'</c> (field
    /// <c>[Start]</c> or <c>[End]</c>; operator <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, or
    /// <c>&gt;=</c>) and evaluates it against the event boundaries.
    /// </summary>
    private static bool EvaluateClause(string clause, DateTime eventStart, DateTime eventEnd)
    {
        var parts = clause.Split(' ', 3);
        parts
            .Should()
            .HaveCount(3, $"clause '{clause}' must have field, operator, and quoted boundary");
        var fieldValue = parts[0] switch
        {
            "[Start]" => eventStart,
            "[End]" => eventEnd,
            _ => throw new InvalidOperationException(
                $"Unsupported field in filter clause '{clause}'."
            ),
        };
        var boundary = DateTime.ParseExact(
            parts[2].Trim('\''),
            "MM/dd/yyyy hh:mm tt",
            CultureInfo.InvariantCulture
        );
        return parts[1] switch
        {
            "<" => fieldValue < boundary,
            ">" => fieldValue > boundary,
            "<=" => fieldValue <= boundary,
            ">=" => fieldValue >= boundary,
            _ => throw new InvalidOperationException(
                $"Unsupported operator in filter clause '{clause}'."
            ),
        };
    }
}
