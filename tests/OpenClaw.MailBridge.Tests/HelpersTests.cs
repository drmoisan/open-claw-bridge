using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Unit tests for the helper types in <c>Helpers.cs</c>:
/// <see cref="BridgeIdCodec"/>, <see cref="BodySanitizer"/>, and <see cref="BridgeSettingsValidator"/>.
/// </summary>
/// <remarks>
/// <see cref="BridgeSettingsValidator.Validate"/> with valid settings (<see cref="BridgeSettings.Default"/>)
/// is already covered in <see cref="BridgeContractsCoverageTests"/> and is not duplicated here.
/// </remarks>
[TestClass]
public class HelpersTests
{
    // ─── BridgeIdCodec — MessageId encoding ───────────────────────────────────────

    /// <summary>
    /// Verifies that a non-meeting entry ID is prefixed with <c>msg:</c>.
    /// </summary>
    [TestMethod]
    public void MessageId_WhenNotMeeting_ReturnsMsgPrefix()
    {
        // Act
        var id = BridgeIdCodec.MessageId("ENTRY123", isMeeting: false);

        // Assert
        id.Should().StartWith("msg:");
    }

    /// <summary>
    /// Verifies that a meeting entry ID is prefixed with <c>mtg:</c>.
    /// </summary>
    [TestMethod]
    public void MessageId_WhenMeeting_ReturnsMtgPrefix()
    {
        // Act
        var id = BridgeIdCodec.MessageId("ENTRY456", isMeeting: true);

        // Assert
        id.Should().StartWith("mtg:");
    }

    // ─── BridgeIdCodec — EventId encoding ────────────────────────────────────────

    /// <summary>
    /// Verifies that when a non-empty global appointment ID is provided it is used for
    /// the encoded identity segment rather than the entry ID.
    /// </summary>
    [TestMethod]
    public void EventId_WhenGlobalAppointmentIdIsProvided_UsesGlobalIdForEncoding()
    {
        // Arrange
        var startUtc = new DateTimeOffset(2026, 4, 18, 9, 0, 0, TimeSpan.Zero);

        // Act
        var withGlobalId = BridgeIdCodec.EventId("GLOBAL-ID", "ENTRY-ID", startUtc);
        var withoutGlobalId = BridgeIdCodec.EventId(null, "ENTRY-ID", startUtc);

        // Assert: the two encoded ids should differ because different identity values are encoded
        withGlobalId.Should().NotBe(withoutGlobalId);
    }

    /// <summary>
    /// Verifies that when the global appointment ID is null or whitespace the entry ID is
    /// used instead, so the event ID remains stable regardless of whether the meeting has
    /// a global ID.
    /// </summary>
    [TestMethod]
    public void EventId_WhenGlobalAppointmentIdIsNullOrWhitespace_UsesEntryId()
    {
        // Arrange
        var startUtc = new DateTimeOffset(2026, 4, 18, 9, 0, 0, TimeSpan.Zero);

        // Act — null and whitespace should produce identical results because both fall back
        var fromNull = BridgeIdCodec.EventId(null, "ENTRY-ID", startUtc);
        var fromWhitespace = BridgeIdCodec.EventId("   ", "ENTRY-ID", startUtc);

        // Assert
        fromNull.Should().Be(fromWhitespace);
        fromNull.Should().StartWith("evt:");
    }

    // ─── BridgeIdCodec — round-trip (MessageId + TryDecodeMessageId) ─────────────

    /// <summary>
    /// Verifies that encoding a non-meeting entry ID and decoding it back yields the
    /// original entry ID with isMeeting = false.
    /// </summary>
    [TestMethod]
    public void MessageId_ThenTryDecodeMessageId_RoundTripsNonMeetingId()
    {
        // Arrange
        const string original = "ABCDEF1234567890";
        var encoded = BridgeIdCodec.MessageId(original, isMeeting: false);

        // Act
        var decoded = BridgeIdCodec.TryDecodeMessageId(encoded, out var entryId, out var isMeeting);

        // Assert
        decoded.Should().BeTrue();
        entryId.Should().Be(original);
        isMeeting.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that encoding a meeting entry ID and decoding it back yields the original
    /// entry ID with isMeeting = true.
    /// </summary>
    [TestMethod]
    public void MessageId_ThenTryDecodeMessageId_RoundTripsMeetingId()
    {
        // Arrange
        const string original = "MTG0000111122223333";
        var encoded = BridgeIdCodec.MessageId(original, isMeeting: true);

        // Act
        var decoded = BridgeIdCodec.TryDecodeMessageId(encoded, out var entryId, out var isMeeting);

        // Assert
        decoded.Should().BeTrue();
        entryId.Should().Be(original);
        isMeeting.Should().BeTrue();
    }

    // ─── BridgeIdCodec — TryDecodeMessageId edge cases ───────────────────────────

    /// <summary>
    /// Verifies that a null bridge ID causes <c>TryDecodeMessageId</c> to return false
    /// with empty out-parameter values.
    /// </summary>
    [TestMethod]
    public void TryDecodeMessageId_WhenBridgeIdIsNull_ReturnsFalse()
    {
        // Act
        var result = BridgeIdCodec.TryDecodeMessageId(null, out var entryId, out var isMeeting);

        // Assert
        result.Should().BeFalse();
        entryId.Should().BeEmpty();
        isMeeting.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a bridge ID with an unrecognized prefix causes <c>TryDecodeMessageId</c>
    /// to return false so callers can distinguish message IDs from event IDs.
    /// </summary>
    [TestMethod]
    public void TryDecodeMessageId_WhenPrefixIsInvalid_ReturnsFalse()
    {
        // Act: "evt:" is an event prefix, not a message prefix
        var result = BridgeIdCodec.TryDecodeMessageId("evt:somebase64", out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a string that is not a valid encoded message ID (corrupted base-64
    /// segment) causes <c>TryDecodeMessageId</c> to return false gracefully.
    /// </summary>
    [TestMethod]
    public void TryDecodeMessageId_WhenBase64SegmentIsCorrupted_ReturnsFalse()
    {
        // Arrange: "!!!" is not valid base-64
        var result = BridgeIdCodec.TryDecodeMessageId("msg:!!!", out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    // ─── BridgeIdCodec — round-trip (EventId + TryDecodeEventId) ─────────────────

    /// <summary>
    /// Verifies that encoding an event and decoding it back yields the original identity
    /// and start timestamp (UTC).
    /// </summary>
    [TestMethod]
    public void EventId_ThenTryDecodeEventId_RoundTripsIdentityAndStartUtc()
    {
        // Arrange
        var startUtc = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var encoded = BridgeIdCodec.EventId("GLOBAL-APPT-ID", "ENTRY-ID", startUtc);

        // Act
        var decoded = BridgeIdCodec.TryDecodeEventId(
            encoded,
            out var appointmentIdentity,
            out var decodedStart
        );

        // Assert
        decoded.Should().BeTrue();
        appointmentIdentity.Should().Be("GLOBAL-APPT-ID");
        decodedStart.Should().Be(startUtc);
    }

    // ─── BridgeIdCodec — TryDecodeEventId edge cases ─────────────────────────────

    /// <summary>
    /// Verifies that a null bridge ID causes <c>TryDecodeEventId</c> to return false.
    /// </summary>
    [TestMethod]
    public void TryDecodeEventId_WhenBridgeIdIsNull_ReturnsFalse()
    {
        // Act
        var result = BridgeIdCodec.TryDecodeEventId(null, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a bridge ID with the wrong prefix (e.g. <c>msg:</c>) causes
    /// <c>TryDecodeEventId</c> to return false.
    /// </summary>
    [TestMethod]
    public void TryDecodeEventId_WhenPrefixIsNotEvt_ReturnsFalse()
    {
        // Act
        var result = BridgeIdCodec.TryDecodeEventId("msg:somebase64:2026-01-01", out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that an event ID with a non-parsable date segment causes
    /// <c>TryDecodeEventId</c> to return false rather than throwing.
    /// </summary>
    [TestMethod]
    public void TryDecodeEventId_WhenDateSegmentIsNotParsable_ReturnsFalse()
    {
        // Arrange: build a valid prefix + encoded identity, but corrupt the date segment
        var encodedIdentity = BridgeIdCodec.EventId("ID", "ID", DateTimeOffset.UtcNow);
        var parts = encodedIdentity.Split(':', 3);
        var corrupted = $"{parts[0]}:{parts[1]}:NOT_A_DATE";

        // Act
        var result = BridgeIdCodec.TryDecodeEventId(corrupted, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    // ─── BodySanitizer.NormalizePreview ───────────────────────────────────────────

    /// <summary>
    /// Verifies that null input returns an empty string without throwing.
    /// </summary>
    [TestMethod]
    public void NormalizePreview_WhenInputIsNull_ReturnsEmpty()
    {
        // Act & Assert
        BodySanitizer.NormalizePreview(null, 200).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that a whitespace-only input returns an empty string.
    /// </summary>
    [TestMethod]
    public void NormalizePreview_WhenInputIsWhitespace_ReturnsEmpty()
    {
        // Act & Assert
        BodySanitizer.NormalizePreview("   ", 200).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that HTML tags are stripped and resulting whitespace runs are condensed
    /// to a single space.
    /// </summary>
    [TestMethod]
    public void NormalizePreview_WhenInputContainsHtmlTags_StripsTagsAndCondensesSpaces()
    {
        // Arrange
        const string input = "<p>Hello <b>world</b>.</p>";

        // Act
        var result = BodySanitizer.NormalizePreview(input, 200);

        // Assert: tags are gone and excess whitespace is collapsed
        result.Should().NotContain("<");
        result.Should().NotContain(">");
        result.Should().Be("Hello world .");
    }

    /// <summary>
    /// Verifies that Windows-style file paths are replaced with the <c>[path]</c> placeholder
    /// to prevent leaking local filesystem information into body previews.
    /// </summary>
    [TestMethod]
    public void NormalizePreview_WhenInputContainsWindowsFilePath_ReplacesWithPlaceholder()
    {
        // Arrange
        const string input = "See C:\\Users\\alice\\report.pdf for details.";

        // Act
        var result = BodySanitizer.NormalizePreview(input, 200);

        // Assert
        result.Should().Contain("[path]");
        result.Should().NotContain("C:\\");
    }

    /// <summary>
    /// Verifies that text longer than <paramref name="maxChars"/> is truncated at the
    /// exact character boundary.
    /// </summary>
    [TestMethod]
    public void NormalizePreview_WhenInputExceedsMaxChars_TruncatesAtBoundary()
    {
        // Arrange: 20 non-whitespace characters, limit to 10
        const string input = "ABCDEFGHIJKLMNOPQRST";

        // Act
        var result = BodySanitizer.NormalizePreview(input, 10);

        // Assert
        result.Should().Be("ABCDEFGHIJ");
        result.Length.Should().Be(10);
    }

    /// <summary>
    /// Verifies that text at or below the limit is returned untruncated.
    /// </summary>
    [TestMethod]
    public void NormalizePreview_WhenInputIsWithinMaxChars_ReturnsFullText()
    {
        // Arrange
        const string input = "Short text.";

        // Act
        var result = BodySanitizer.NormalizePreview(input, 200);

        // Assert
        result.Should().Be("Short text.");
    }

    // ─── BridgeSettingsValidator — individual constraint violations ───────────────

    private static BridgeSettings Valid() => BridgeSettings.Default;

    /// <summary>
    /// Verifies that an unrecognized mode value produces a validation error.
    /// </summary>
    [TestMethod]
    public void Validate_WhenModeIsInvalid_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            Mode = "extreme",
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("mode"));
    }

    /// <summary>
    /// Verifies that <c>enhanced</c> is accepted as a valid mode alongside <c>safe</c>.
    /// </summary>
    [TestMethod]
    public void Validate_WhenModeIsEnhanced_ReturnsNoModeError()
    {
        // Arrange
        var s = Valid() with
        {
            Mode = "enhanced",
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().NotContain(e => e.Contains("mode"));
    }

    /// <summary>
    /// Verifies that <c>InboxPollSeconds</c> below 5 is rejected.
    /// </summary>
    [TestMethod]
    public void Validate_WhenInboxPollSecondsLessThan5_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            InboxPollSeconds = 4,
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("inboxPollSeconds"));
    }

    /// <summary>
    /// Verifies that <c>CalendarPollSeconds</c> below 30 is rejected.
    /// </summary>
    [TestMethod]
    public void Validate_WhenCalendarPollSecondsLessThan30_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            CalendarPollSeconds = 29,
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("calendarPollSeconds"));
    }

    /// <summary>
    /// Verifies that <c>MaxItemsPerScan</c> of 0 is rejected.
    /// </summary>
    [TestMethod]
    public void Validate_WhenMaxItemsPerScanIsZero_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            MaxItemsPerScan = 0,
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("maxItemsPerScan"));
    }

    /// <summary>
    /// Verifies that <c>MaxItemsPerScan</c> above 2000 is rejected.
    /// </summary>
    [TestMethod]
    public void Validate_WhenMaxItemsPerScanExceeds2000_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            MaxItemsPerScan = 2001,
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("maxItemsPerScan"));
    }

    /// <summary>
    /// Verifies that <c>BodyPreviewMaxChars</c> of 0 is rejected.
    /// </summary>
    [TestMethod]
    public void Validate_WhenBodyPreviewMaxCharsIsZero_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            BodyPreviewMaxChars = 0,
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("bodyPreviewMaxChars"));
    }

    /// <summary>
    /// Verifies that <c>ComYieldBatchSize</c> of 0 is rejected.
    /// </summary>
    [TestMethod]
    public void Validate_WhenComYieldBatchSizeIsZero_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            ComYieldBatchSize = 0,
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("comYieldBatchSize"));
    }

    /// <summary>
    /// Verifies that a negative <c>ComYieldMilliseconds</c> is rejected.
    /// </summary>
    [TestMethod]
    public void Validate_WhenComYieldMillisecondsIsNegative_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            ComYieldMilliseconds = -1,
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("comYieldMilliseconds"));
    }

    /// <summary>
    /// Verifies that an empty pipe name is rejected, since the bridge cannot operate
    /// without a named pipe to listen on.
    /// </summary>
    [TestMethod]
    public void Validate_WhenPipeNameIsEmpty_ReturnsError()
    {
        // Arrange
        var s = Valid() with
        {
            PipeName = string.Empty,
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("pipeName"));
    }

    /// <summary>
    /// Verifies that multiple simultaneous violations are all reported in a single call,
    /// so the caller receives the full list of configuration problems at once.
    /// </summary>
    [TestMethod]
    public void Validate_WhenMultipleConstraintsViolated_ReturnsAllErrors()
    {
        // Arrange: mode is invalid, inbox poll is too low, pipe name is missing
        var s = Valid() with
        {
            Mode = "turbo",
            InboxPollSeconds = 1,
            PipeName = "",
        };

        // Act
        var errors = BridgeSettingsValidator.Validate(s);

        // Assert: all three violations must be present regardless of order
        errors.Should().HaveCountGreaterThanOrEqualTo(3);
    }
}
