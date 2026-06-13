using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Adapter-contract tests for <see cref="ComMessageSource"/> (issue #73, locked decision D-D),
/// validating the pure (non-COM) mapping surface: conversation id pass-through, meeting-type
/// projection (raw <c>OlMeetingType</c> for meeting items, null for ordinary mail), and the To/Cc
/// recipient projection from a reflection-readable fake source. The SMTP resolution and recipient
/// COM-enumeration paths are validated end-to-end through the scanner in
/// <c>OutlookScannerMessageFieldsTests</c>; this class establishes the adapter-contract test seam.
/// No live COM; the fake <see cref="ComActiveObject"/> release is a no-op on non-COM objects.
/// </summary>
[TestClass]
public sealed class ComMessageSourceTests
{
    /// <summary>
    /// Reflection-readable analog of an ordinary <c>MailItem</c> exposing only the members the
    /// adapter reads for the pure-mapping cases.
    /// </summary>
    private sealed class FakeAdapterMailItem
    {
        public string? ConversationID { get; init; }
        public string? SenderEmailAddress { get; init; }
        public object? Recipients { get; init; }
        public object? Sender { get; init; }
    }

    /// <summary>
    /// Reflection-readable analog of a <c>MeetingItem</c> additionally exposing <c>MeetingType</c>.
    /// </summary>
    private sealed class FakeAdapterMeetingItem
    {
        public string? ConversationID { get; init; }
        public int MeetingType { get; init; }
        public string? SenderEmailAddress { get; init; }
        public object? Recipients { get; init; }
        public object? Sender { get; init; }
    }

    [TestMethod]
    public void Adapter_should_pass_conversation_id_through_unmodified()
    {
        // Arrange
        var item = new FakeAdapterMailItem { ConversationID = "conv-123" };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act
        var conversationId = source.ConversationId;

        // Assert
        conversationId.Should().Be("conv-123");
    }

    [TestMethod]
    public void Adapter_should_yield_null_meeting_type_for_ordinary_mail()
    {
        // Arrange
        var item = new FakeAdapterMailItem { ConversationID = "conv-1" };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source.MeetingMessageType.Should().BeNull("ordinary mail carries no OlMeetingType (D-B)");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void Adapter_should_expose_raw_meeting_type_for_meeting_items(int olMeetingType)
    {
        // Arrange
        var item = new FakeAdapterMeetingItem
        {
            ConversationID = "conv-mtg",
            MeetingType = olMeetingType,
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: true);

        // Act / Assert
        source.MeetingMessageType.Should().Be(olMeetingType);
    }

    [TestMethod]
    public void Adapter_should_project_to_and_cc_recipients_by_type()
    {
        // Arrange: To = type 1, Cc = type 2, Bcc = type 3 (ignored).
        var recipients = new FakeRecipients(
            new FakeRecipient
            {
                Type = 1,
                Name = "Alice",
                Address = "alice@test.com",
            },
            new FakeRecipient
            {
                Type = 2,
                Name = "Carol",
                Address = "carol@test.com",
            },
            new FakeRecipient
            {
                Type = 3,
                Name = "Bcc Person",
                Address = "bcc@test.com",
            }
        );
        var item = new FakeAdapterMailItem { Recipients = recipients };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act
        var to = source.ToRecipients;
        var cc = source.CcRecipients;

        // Assert
        to.Should().ContainSingle();
        to[0].Name.Should().Be("Alice");
        to[0].Email.Should().Be("alice@test.com");
        cc.Should().ContainSingle();
        cc[0].Name.Should().Be("Carol");
        cc[0].Email.Should().Be("carol@test.com");
    }

    [TestMethod]
    public void Adapter_should_yield_empty_recipient_lists_when_recipients_absent()
    {
        // Arrange
        var item = new FakeAdapterMailItem { Recipients = null };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source.ToRecipients.Should().BeEmpty();
        source.CcRecipients.Should().BeEmpty();
    }

    [TestMethod]
    public void Adapter_should_fall_back_to_raw_sender_address_when_no_sender_object()
    {
        // Arrange: no Sender object; the adapter degrades to the raw SenderEmailAddress.
        var item = new FakeAdapterMailItem
        {
            Sender = null,
            SenderEmailAddress = "raw.sender@test.com",
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source.SenderEmailResolved.Should().Be("raw.sender@test.com");
    }
}
