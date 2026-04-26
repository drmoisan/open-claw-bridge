using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Unit tests for <see cref="OutlookComHelpers.GetOptionalUtcDateTimeOffset"/> covering
/// all <see cref="DateTimeKind"/> branches, the <see cref="DateTimeOffset"/> arm,
/// a missing COM member, and a string parse fallback.
/// </summary>
[TestClass]
public sealed class OutlookComHelpersDateTimeKindTests
{
    [TestMethod]
    public void GetOptionalUtcDateTimeOffset_UnspecifiedKind_returns_offset_with_zero_offset()
    {
        // Arrange — Outlook COM's StartUTC / EndUTC returns Unspecified kind carrying a UTC value.
        var dt = new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Unspecified);
        var fake = new FakeDateTimeHolder { Value = dt };

        // Act
        var result = OutlookComHelpers.GetOptionalUtcDateTimeOffset(fake, nameof(fake.Value));

        // Assert — value is preserved as-is with zero offset (no double-shift).
        result.Should().NotBeNull();
        result!
            .Value.UtcDateTime.Should()
            .Be(new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc));
        result.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [TestMethod]
    public void GetOptionalUtcDateTimeOffset_UtcKind_returns_same_utc_value()
    {
        // Arrange
        var dt = new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc);
        var fake = new FakeDateTimeHolder { Value = dt };

        // Act
        var result = OutlookComHelpers.GetOptionalUtcDateTimeOffset(fake, nameof(fake.Value));

        // Assert — UTC kind is preserved without modification.
        result.Should().NotBeNull();
        result!
            .Value.UtcDateTime.Should()
            .Be(new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc));
        result.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [TestMethod]
    public void GetOptionalUtcDateTimeOffset_LocalKind_converts_to_utc()
    {
        // Arrange
        var dt = new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Local);
        var fake = new FakeDateTimeHolder { Value = dt };

        // Act
        var result = OutlookComHelpers.GetOptionalUtcDateTimeOffset(fake, nameof(fake.Value));

        // Assert — Local kind is converted to UTC and the resulting offset is zero.
        result.Should().NotBeNull();
        result!.Value.UtcDateTime.Should().Be(dt.ToUniversalTime());
        result.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [TestMethod]
    public void GetOptionalUtcDateTimeOffset_DateTimeOffset_returns_utc()
    {
        // Arrange — a DateTimeOffset at UTC-4 representing 13:00 local / 17:00 UTC.
        var dto = new DateTimeOffset(2026, 4, 27, 13, 0, 0, TimeSpan.FromHours(-4));
        var fake = new FakeDateTimeOffsetHolder { Value = dto };

        // Act
        var result = OutlookComHelpers.GetOptionalUtcDateTimeOffset(fake, nameof(fake.Value));

        // Assert — converted to UTC 17:00.
        result.Should().NotBeNull();
        result!
            .Value.UtcDateTime.Should()
            .Be(new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc));
    }

    [TestMethod]
    public void GetOptionalUtcDateTimeOffset_MissingMember_returns_null()
    {
        // Arrange — object does not expose the requested property; GetMemberValue will throw.
        var fake = new FakeDateTimeHolder { Value = default };

        // Act
        var result = OutlookComHelpers.GetOptionalUtcDateTimeOffset(fake, "NonExistentMember");

        // Assert — COM read failure yields null without propagating.
        result.Should().BeNull();
    }

    [TestMethod]
    public void GetOptionalUtcDateTimeOffset_StringValue_parses_to_utc()
    {
        // Arrange — some COM implementations return a string representation of the date.
        var fake = new FakeStringHolder { Value = "2026-04-27T17:00:00Z" };

        // Act
        var result = OutlookComHelpers.GetOptionalUtcDateTimeOffset(fake, nameof(fake.Value));

        // Assert — TryParse fallback succeeds and returns a non-null result.
        result.Should().NotBeNull();
    }

    // ---------------------------------------------------------------------------
    // Private helpers — simple property bags backed by reflection for COM stub testing
    // ---------------------------------------------------------------------------

    private sealed class FakeDateTimeHolder
    {
        public DateTime Value { get; init; }
    }

    private sealed class FakeDateTimeOffsetHolder
    {
        public DateTimeOffset Value { get; init; }
    }

    private sealed class FakeStringHolder
    {
        public string? Value { get; init; }
    }
}
