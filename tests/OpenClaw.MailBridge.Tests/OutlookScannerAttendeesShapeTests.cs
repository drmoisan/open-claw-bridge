using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Pure JSON-shaping tests for issue #71 (US-AC2, SP-B1, US-AC5):
/// <see cref="OutlookScanner.ShapeAttendeeJson"/> serializes ordered attendee lists to JSON arrays of
/// <c>{"name","email"}</c> objects with lowercase keys, preserved order, <c>"[]"</c> for empty lists,
/// and both keys always present even when a value is missing. No COM, no temp files, deterministic.
/// </summary>
[TestClass]
public sealed class OutlookScannerAttendeesShapeTests
{
    private static OutlookScanner.Attendee Attendee(string name, string email) => new(name, email);

    [TestMethod]
    public void ShapeAttendeeJson_should_emit_lowercase_keys_and_preserve_order()
    {
        // Arrange: a two-element required list; the others empty to isolate the shape assertion.
        var required = new[]
        {
            Attendee("Alice Example", "alice@example.com"),
            Attendee("Bob Example", "bob@example.com"),
        };

        // Act
        var set = OutlookScanner.ShapeAttendeeJson(
            required,
            Array.Empty<OutlookScanner.Attendee>(),
            Array.Empty<OutlookScanner.Attendee>()
        );

        // Assert: exact JSON string, lowercase name/email keys, Graph emailAddress shape, order kept.
        set.RequiredJson.Should()
            .Be(
                "[{\"name\":\"Alice Example\",\"email\":\"alice@example.com\"},"
                    + "{\"name\":\"Bob Example\",\"email\":\"bob@example.com\"}]",
                "the required list must serialize in collection order with lowercase name/email keys"
            );
    }

    [TestMethod]
    public void ShapeAttendeeJson_should_emit_empty_array_for_each_empty_group()
    {
        // Arrange / Act: all three groups empty.
        var set = OutlookScanner.ShapeAttendeeJson(
            Array.Empty<OutlookScanner.Attendee>(),
            Array.Empty<OutlookScanner.Attendee>(),
            Array.Empty<OutlookScanner.Attendee>()
        );

        // Assert: each group is "[]" (not null), so "no attendees of this type" is distinguishable
        // from safe-mode redaction (which uses null).
        set.RequiredJson.Should().Be("[]");
        set.OptionalJson.Should().Be("[]");
        set.ResourcesJson.Should().Be("[]");
    }

    [TestMethod]
    public void ShapeAttendeeJson_should_keep_both_keys_when_a_value_is_missing()
    {
        // Arrange: one recipient missing a name, one missing an email (empty string for missing).
        var required = new[] { Attendee(string.Empty, "noname@example.com") };
        var optional = new[] { Attendee("No Email Person", string.Empty) };

        // Act
        var set = OutlookScanner.ShapeAttendeeJson(
            required,
            optional,
            Array.Empty<OutlookScanner.Attendee>()
        );

        // Assert: both name and email keys are always present; the missing value is "".
        set.RequiredJson.Should()
            .Be(
                "[{\"name\":\"\",\"email\":\"noname@example.com\"}]",
                "a missing name emits an empty string with the name key still present"
            );
        set.OptionalJson.Should()
            .Be(
                "[{\"name\":\"No Email Person\",\"email\":\"\"}]",
                "a missing email emits an empty string with the email key still present"
            );
    }
}
