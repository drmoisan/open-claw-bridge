using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Fail-soft tests for <see cref="ComMessageSource.LinkedGlobalAppointmentId"/> (issue #146). A meeting
/// item resolves its associated appointment's <c>GlobalAppointmentID</c>; ordinary (non-meeting) mail,
/// a meeting item whose <c>GetAssociatedAppointment</c> returns nothing, and a meeting item whose COM
/// access throws each yield <see langword="null"/>. No live COM; hand-written reflection-readable
/// doubles are used and the fake <see cref="ComActiveObject"/> release is a no-op.
/// </summary>
[TestClass]
public sealed class ComMessageSourceLinkageTests
{
    /// <summary>Reflection-readable analog of an appointment exposing <c>GlobalAppointmentID</c>.</summary>
    private sealed class FakeAppointment
    {
        public string? GlobalAppointmentID { get; init; }
    }

    /// <summary>
    /// Meeting-item double exposing <c>GetAssociatedAppointment(bool)</c> returning a fake appointment.
    /// </summary>
    private sealed class FakeMeetingItemWithAppointment
    {
        public object? Appointment { get; init; }

        public object? GetAssociatedAppointment(bool addToCalendar)
        {
            _ = addToCalendar;
            return Appointment;
        }
    }

    /// <summary>Meeting-item double whose <c>GetAssociatedAppointment</c> throws (drives the catch path).</summary>
    private sealed class FakeMeetingItemWithThrowingAppointment
    {
        public object GetAssociatedAppointment(bool addToCalendar) =>
            throw new InvalidOperationException(
                "Simulated COM failure on GetAssociatedAppointment."
            );
    }

    /// <summary>Ordinary-mail double exposing no appointment accessor.</summary>
    private sealed class FakeMailItem
    {
        public string? ConversationID { get; init; }
    }

    [TestMethod]
    public void Adapter_should_resolve_global_appointment_id_for_a_meeting_item()
    {
        // Arrange
        var item = new FakeMeetingItemWithAppointment
        {
            Appointment = new FakeAppointment { GlobalAppointmentID = "clean-goid-1" },
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: true);

        // Act
        var linked = source.LinkedGlobalAppointmentId;

        // Assert
        linked.Should().Be("clean-goid-1");
    }

    [TestMethod]
    public void Adapter_should_yield_null_linked_key_for_ordinary_mail()
    {
        // Arrange
        var item = new FakeMailItem { ConversationID = "conv-1" };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source
            .LinkedGlobalAppointmentId.Should()
            .BeNull("ordinary mail has no associated appointment");
    }

    [TestMethod]
    public void Adapter_should_yield_null_when_no_associated_appointment_is_available()
    {
        // Arrange: a meeting item whose GetAssociatedAppointment returns null.
        var item = new FakeMeetingItemWithAppointment { Appointment = null };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: true);

        // Act / Assert
        source.LinkedGlobalAppointmentId.Should().BeNull();
    }

    [TestMethod]
    public void Adapter_should_fail_soft_to_null_when_appointment_read_throws()
    {
        // Arrange
        var item = new FakeMeetingItemWithThrowingAppointment();
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: true);

        // Act
        var linked = source.LinkedGlobalAppointmentId;

        // Assert: the COM fault is swallowed and yields a clean null; no exception escapes.
        linked.Should().BeNull();
    }
}
