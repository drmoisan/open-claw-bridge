using FluentAssertions;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

/// <summary>
/// Direct unit tests for <see cref="HostAdapterRequestValidation"/>, exercising each
/// static validation method against its full range of valid, invalid, and boundary inputs.
/// These tests complement the HTTP-level integration tests in
/// <see cref="HostAdapterValidationTests"/> by pinning the validation logic independently
/// of the web stack.
/// </summary>
[TestClass]
public class HostAdapterRequestValidationTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static HostAdapterOptions TestOptions() =>
        new()
        {
            DefaultLimit = 100,
            MaxLimit = 250,
            AdapterVersion = "1.0-test",
        };

    // ─── TryGetUtcTimestamp ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an empty query parameter value causes the method to return false and
    /// populate the failure output with an <c>InvalidRequest</c> response.
    /// </summary>
    [TestMethod]
    public void TryGetUtcTimestamp_WhenValueIsEmpty_ReturnsFalseWithFailure()
    {
        // Arrange
        StringValues empty = string.Empty;

        // Act
        var result = HostAdapterRequestValidation.TryGetUtcTimestamp<object>(
            empty,
            "since",
            "req-1",
            TestOptions(),
            null,
            out _,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
        failure!.Envelope.Ok.Should().BeFalse();
        failure.Envelope.Error!.Message.Should().Contain("since");
    }

    /// <summary>
    /// Verifies that a timestamp with a non-UTC offset (e.g. Eastern time) causes the
    /// method to return false and report that a UTC timestamp is required.
    /// </summary>
    [TestMethod]
    public void TryGetUtcTimestamp_WhenTimestampHasNonZeroOffset_ReturnsFalseWithFailure()
    {
        // Arrange — valid ISO-8601 but offset is -04:00, not UTC
        StringValues nonUtc = "2026-04-12T09:15:00-04:00";

        // Act
        var result = HostAdapterRequestValidation.TryGetUtcTimestamp<object>(
            nonUtc,
            "since",
            "req-2",
            TestOptions(),
            null,
            out _,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
        failure!.Envelope.Error!.Message.Should().Contain("UTC");
    }

    /// <summary>
    /// Verifies that a well-formed UTC timestamp is parsed correctly, with the offset
    /// confirmed to be zero.
    /// </summary>
    [TestMethod]
    public void TryGetUtcTimestamp_WhenValueIsValidUtcTimestamp_ReturnsTrueWithParsedTimestamp()
    {
        // Arrange
        StringValues validUtc = "2026-04-15T10:00:00Z";

        // Act
        var result = HostAdapterRequestValidation.TryGetUtcTimestamp<object>(
            validUtc,
            "since",
            "req-3",
            TestOptions(),
            null,
            out var timestamp,
            out var failure
        );

        // Assert
        result.Should().BeTrue();
        failure.Should().BeNull();
        timestamp.Offset.Should().Be(TimeSpan.Zero);
        timestamp.Year.Should().Be(2026);
        timestamp.Month.Should().Be(4);
        timestamp.Day.Should().Be(15);
    }

    // ─── TryGetLimit ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an absent (empty) limit query parameter causes the method to succeed
    /// and return the configured default limit.
    /// </summary>
    [TestMethod]
    public void TryGetLimit_WhenValueIsEmpty_ReturnsTrueWithDefaultLimit()
    {
        // Arrange
        var options = TestOptions();
        StringValues empty = string.Empty;

        // Act
        var result = HostAdapterRequestValidation.TryGetLimit<object>(
            empty,
            "req-4",
            options,
            null,
            out var limit,
            out var failure
        );

        // Assert
        result.Should().BeTrue();
        failure.Should().BeNull();
        limit.Should().Be(options.DefaultLimit);
    }

    /// <summary>
    /// Verifies that a non-integer string is rejected with an <c>InvalidRequest</c> failure.
    /// </summary>
    [TestMethod]
    public void TryGetLimit_WhenValueIsNotAnInteger_ReturnsFalseWithFailure()
    {
        // Arrange
        StringValues notANumber = "abc";

        // Act
        var result = HostAdapterRequestValidation.TryGetLimit<object>(
            notANumber,
            "req-5",
            TestOptions(),
            null,
            out _,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
        failure!.Envelope.Error!.Message.Should().Contain("integer");
    }

    /// <summary>
    /// Verifies that a limit of zero is rejected because it must be greater than zero.
    /// </summary>
    [TestMethod]
    public void TryGetLimit_WhenValueIsZero_ReturnsFalseWithFailure()
    {
        // Arrange
        StringValues zero = "0";

        // Act
        var result = HostAdapterRequestValidation.TryGetLimit<object>(
            zero,
            "req-6",
            TestOptions(),
            null,
            out _,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
        failure!.Envelope.Error!.Message.Should().Contain("greater than zero");
    }

    /// <summary>
    /// Verifies that a negative limit value is rejected.
    /// </summary>
    [TestMethod]
    public void TryGetLimit_WhenValueIsNegative_ReturnsFalseWithFailure()
    {
        // Arrange
        StringValues negative = "-5";

        // Act
        var result = HostAdapterRequestValidation.TryGetLimit<object>(
            negative,
            "req-7",
            TestOptions(),
            null,
            out _,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that a limit above <see cref="HostAdapterOptions.MaxLimit"/> is rejected and
    /// the failure message mentions the maximum permitted value.
    /// </summary>
    [TestMethod]
    public void TryGetLimit_WhenValueExceedsMaxLimit_ReturnsFalseWithFailureContainingMaxValue()
    {
        // Arrange
        var options = TestOptions(); // MaxLimit = 250
        StringValues overMax = "251";

        // Act
        var result = HostAdapterRequestValidation.TryGetLimit<object>(
            overMax,
            "req-8",
            options,
            null,
            out _,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
        failure!.Envelope.Error!.Message.Should().Contain("250");
    }

    /// <summary>
    /// Verifies that a valid in-range limit is accepted and returned as-is.
    /// </summary>
    [TestMethod]
    public void TryGetLimit_WhenValueIsValid_ReturnsTrueWithParsedLimit()
    {
        // Arrange
        StringValues valid = "50";

        // Act
        var result = HostAdapterRequestValidation.TryGetLimit<object>(
            valid,
            "req-9",
            TestOptions(),
            null,
            out var limit,
            out var failure
        );

        // Assert
        result.Should().BeTrue();
        failure.Should().BeNull();
        limit.Should().Be(50);
    }

    // ─── TryValidateWindow ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when <c>end</c> equals <c>start</c> the window is considered invalid
    /// because an empty window cannot contain any events.
    /// </summary>
    [TestMethod]
    public void TryValidateWindow_WhenEndEqualsStart_ReturnsFalseWithFailure()
    {
        // Arrange
        var pivot = DateTimeOffset.Parse("2026-04-15T09:00:00Z");

        // Act
        var result = HostAdapterRequestValidation.TryValidateWindow<object>(
            pivot,
            pivot,
            "req-10",
            TestOptions(),
            null,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
        failure!.Envelope.Error!.Message.Should().Contain("end");
    }

    /// <summary>
    /// Verifies that when <c>end</c> is earlier than <c>start</c> the window is rejected.
    /// </summary>
    [TestMethod]
    public void TryValidateWindow_WhenEndIsBeforeStart_ReturnsFalseWithFailure()
    {
        // Arrange
        var start = DateTimeOffset.Parse("2026-04-15T12:00:00Z");
        var end = DateTimeOffset.Parse("2026-04-15T11:00:00Z");

        // Act
        var result = HostAdapterRequestValidation.TryValidateWindow<object>(
            start,
            end,
            "req-11",
            TestOptions(),
            null,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that when <c>end</c> is strictly after <c>start</c> the window is accepted.
    /// </summary>
    [TestMethod]
    public void TryValidateWindow_WhenEndIsAfterStart_ReturnsTrueWithNullFailure()
    {
        // Arrange
        var start = DateTimeOffset.Parse("2026-04-15T09:00:00Z");
        var end = DateTimeOffset.Parse("2026-04-15T17:00:00Z");

        // Act
        var result = HostAdapterRequestValidation.TryValidateWindow<object>(
            start,
            end,
            "req-12",
            TestOptions(),
            null,
            out var failure
        );

        // Assert
        result.Should().BeTrue();
        failure.Should().BeNull();
    }

    // ─── TryGetBridgeId ───────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a null bridge ID is rejected and the normalized output is set to an
    /// empty string.
    /// </summary>
    [TestMethod]
    public void TryGetBridgeId_WhenBridgeIdIsNull_ReturnsFalseWithEmptyNormalizedId()
    {
        // Act
        var result = HostAdapterRequestValidation.TryGetBridgeId<object>(
            null,
            "req-13",
            TestOptions(),
            null,
            out var normalizedBridgeId,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        normalizedBridgeId.Should().BeEmpty();
        failure.Should().NotBeNull();
        failure!.Envelope.Error!.Message.Should().Contain("bridgeId");
    }

    /// <summary>
    /// Verifies that a whitespace-only bridge ID is treated the same as null and rejected.
    /// </summary>
    [TestMethod]
    public void TryGetBridgeId_WhenBridgeIdIsWhitespace_ReturnsFalseWithFailure()
    {
        // Act
        var result = HostAdapterRequestValidation.TryGetBridgeId<object>(
            "   ",
            "req-14",
            TestOptions(),
            null,
            out _,
            out var failure
        );

        // Assert
        result.Should().BeFalse();
        failure.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that a non-empty bridge ID is accepted and returned unchanged as the
    /// normalized value.
    /// </summary>
    [TestMethod]
    public void TryGetBridgeId_WhenBridgeIdIsValid_ReturnsTrueWithNormalizedId()
    {
        // Arrange
        const string bridgeId = "msg:ABCDEF1234";

        // Act
        var result = HostAdapterRequestValidation.TryGetBridgeId<object>(
            bridgeId,
            "req-15",
            TestOptions(),
            null,
            out var normalizedBridgeId,
            out var failure
        );

        // Assert
        result.Should().BeTrue();
        failure.Should().BeNull();
        normalizedBridgeId.Should().Be(bridgeId);
    }

    // ─── Bridge-status passthrough ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when a <see cref="BridgeStatusDto"/> is supplied to a failing
    /// validation method it is included in the response envelope's meta so the caller
    /// can surface bridge state alongside the error.
    /// </summary>
    [TestMethod]
    public void TryGetUtcTimestamp_WhenBridgeIsSuppliedAndValidationFails_BridgeIsIncludedInFailureEnvelope()
    {
        // Arrange
        var bridge = new BridgeStatusDto(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );
        StringValues empty = string.Empty;

        // Act
        HostAdapterRequestValidation.TryGetUtcTimestamp<object>(
            empty,
            "since",
            "req-16",
            TestOptions(),
            bridge,
            out _,
            out var failure
        );

        // Assert: the bridge status must be echoed in the response meta
        failure.Should().NotBeNull();
        failure!.Envelope.Meta.Bridge.Should().NotBeNull();
        failure.Envelope.Meta.Bridge!.State.Should().Be(BridgeState.ready.ToString());
    }
}
