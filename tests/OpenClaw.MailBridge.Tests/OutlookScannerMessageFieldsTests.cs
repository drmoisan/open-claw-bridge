using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// End-to-end normalization tests for the issue-#73 resolved <see cref="MessageDto"/> fields
/// (AC-02..AC-08). Drives <see cref="OutlookScanner.ScanAsync"/> over reflection-readable fakes (no
/// live COM) and asserts the six populated fields for both the ordinary-mail path and the
/// meeting-message path. The fake <see cref="ComActiveObject"/> release is a no-op on non-COM
/// objects, so deterministic execution holds.
/// </summary>
[TestClass]
public sealed class OutlookScannerMessageFieldsTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    private static OutlookScanner BuildScanner(FakeComActiveObject com)
    {
        var settings = BridgeSettings.Default;
        return new OutlookScanner(
            settings,
            new BridgeStateStore(settings),
            NullLogger<OutlookScanner>.Instance,
            com,
            _ => 0,
            () => FixedNow
        );
    }

    private static FakeComActiveObject ComWithInboxItem(object item)
    {
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(item);
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[6] = inbox;
        outlook.Namespace.DefaultFolders[9] = new FakeOutlookFolder();
        return new FakeComActiveObject { RunningObject = outlook };
    }

    private static async Task<MessageDto> ScanSingleAsync(object item)
    {
        var com = ComWithInboxItem(item);
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(com);

        await scanner.ScanAsync(repo);

        repo.Messages.Should().HaveCount(1);
        return repo.Messages.Values.First();
    }

    private static IReadOnlyList<(string Name, string Email)> ParseAttendeeJson(string? json)
    {
        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        return doc
            .RootElement.EnumerateArray()
            .Select(e => (e.GetProperty("name").GetString()!, e.GetProperty("email").GetString()!))
            .ToArray();
    }

    // ── Ordinary-mail path (P6-T2) ──────────────────────────────────────

    [TestMethod]
    public async Task OrdinaryMail_should_populate_resolved_fields_and_recipient_json()
    {
        // Arrange: a mail item with To (type 1) and Cc (type 2) recipients, a conversation id, and
        // no on-behalf-of identity, so FromEmailAddress falls back to the resolved sender (AC-03).
        var item = new FakeMailItem
        {
            EntryID = "mail-1",
            Subject = "Hello",
            ReceivedTime = FixedNow.AddHours(-1),
            MessageClass = "IPM.Note",
            SenderName = "Alice",
            SenderEmailAddress = "alice@contoso.com",
            ConversationID = "conv-mail-1",
            Sender = new FakeSender { Address = "alice@contoso.com" },
            Recipients = new FakeRecipients(
                new FakeRecipient
                {
                    Type = 1,
                    Name = "To Person",
                    Address = "to@contoso.com",
                },
                new FakeRecipient
                {
                    Type = 2,
                    Name = "Cc Person",
                    Address = "cc@contoso.com",
                }
            ),
        };

        // Act
        var msg = await ScanSingleAsync(item);

        // Assert
        msg.ItemKind.Should().Be("mail");
        msg.MeetingMessageType.Should().BeNull("ordinary mail carries no OlMeetingType (AC-06)");
        msg.ConversationId.Should().Be("conv-mail-1");
        msg.SenderEmailResolved.Should().Be("alice@contoso.com");
        msg.FromEmailAddress.Should()
            .Be("alice@contoso.com", "no on-behalf-of identity falls back to the resolved sender");

        var to = ParseAttendeeJson(msg.ToJson);
        to.Should().ContainSingle();
        to[0].Should().Be(("To Person", "to@contoso.com"));

        var cc = ParseAttendeeJson(msg.CcJson);
        cc.Should().ContainSingle();
        cc[0].Should().Be(("Cc Person", "cc@contoso.com"));
    }

    [TestMethod]
    public async Task OrdinaryMail_with_no_recipients_should_yield_empty_json_arrays()
    {
        // Arrange
        var item = new FakeMailItem
        {
            EntryID = "mail-2",
            Subject = "No recipients",
            ReceivedTime = FixedNow.AddHours(-1),
            MessageClass = "IPM.Note",
            SenderName = "Alice",
            SenderEmailAddress = "alice@contoso.com",
            ConversationID = "conv-mail-2",
            Recipients = null,
        };

        // Act
        var msg = await ScanSingleAsync(item);

        // Assert: empty/absent recipients serialize to "[]" (never null) for both fields (AC-04).
        msg.ToJson.Should().Be("[]");
        msg.CcJson.Should().Be("[]");
    }

    // ── Meeting-message path (P6-T3) ────────────────────────────────────

    [TestMethod]
    public async Task MeetingRequest_should_satisfy_combined_acceptance_signal()
    {
        // Arrange: a meeting request (MeetingType 0) with a resolvable SMTP sender, To recipient, and
        // conversation id — the AC-07 combined signal.
        var item = new FakeMeetingItem
        {
            EntryID = "mtg-1",
            Subject = "Team Standup",
            ReceivedTime = FixedNow,
            MessageClass = "IPM.Schedule.Meeting.Request",
            SenderName = "Bob",
            SenderEmailAddress = "bob@contoso.com",
            MeetingType = 0,
            ConversationID = "conv-mtg-1",
            Sender = new FakeSender { Address = "bob@contoso.com" },
            Recipients = new FakeRecipients(
                new FakeRecipient
                {
                    Type = 1,
                    Name = "Invitee",
                    Address = "invitee@contoso.com",
                }
            ),
        };

        // Act
        var msg = await ScanSingleAsync(item);

        // Assert (AC-06 int, AC-07 combined)
        msg.ItemKind.Should().Be("meeting");
        msg.MeetingMessageType.Should().Be(0, "a meeting request carries raw OlMeetingType 0");
        msg.SenderEmailResolved.Should().Be("bob@contoso.com");
        msg.ConversationId.Should().Be("conv-mtg-1");

        var to = ParseAttendeeJson(msg.ToJson);
        to.Should().ContainSingle();
        to[0].Email.Should().Be("invitee@contoso.com");
    }

    [TestMethod]
    public async Task MeetingCancellation_should_carry_cancellation_meeting_type()
    {
        // Arrange
        var item = new FakeMeetingItem
        {
            EntryID = "mtg-2",
            Subject = "Cancelled",
            ReceivedTime = FixedNow,
            MessageClass = "IPM.Schedule.Meeting.Canceled",
            SenderName = "Bob",
            SenderEmailAddress = "bob@contoso.com",
            MeetingType = 1,
            ConversationID = "conv-mtg-2",
        };

        // Act
        var msg = await ScanSingleAsync(item);

        // Assert (AC-06)
        msg.MeetingMessageType.Should()
            .Be(1, "a meeting cancellation carries raw OlMeetingType 1");
    }

    [TestMethod]
    public async Task DelegateSentMeeting_should_reflect_on_behalf_of_in_from_address()
    {
        // Arrange: SentOnBehalfOfEmailAddress is an SMTP address, so FromEmailAddress reflects the
        // on-behalf-of identity (AC-03 present-branch), distinct from the resolved sender.
        var item = new FakeMeetingItem
        {
            EntryID = "mtg-3",
            Subject = "Delegate sent",
            ReceivedTime = FixedNow,
            MessageClass = "IPM.Schedule.Meeting.Request",
            SenderName = "Assistant",
            SenderEmailAddress = "assistant@contoso.com",
            SentOnBehalfOfEmailAddress = "boss@contoso.com",
            MeetingType = 0,
            ConversationID = "conv-mtg-3",
            Sender = new FakeSender { Address = "assistant@contoso.com" },
        };

        // Act
        var msg = await ScanSingleAsync(item);

        // Assert
        msg.FromEmailAddress.Should()
            .Be("boss@contoso.com", "delegate-sent reflects on-behalf-of");
        msg.SenderEmailResolved.Should().Be("assistant@contoso.com");
    }

    [TestMethod]
    public async Task ExchangeDnSender_should_resolve_to_true_smtp_not_the_dn()
    {
        // Arrange: an internal Exchange sender whose AddressEntry.Address is a legacy Exchange DN.
        // The GetExchangeUser().PrimarySmtpAddress path must resolve the true SMTP (AC-02 resolved).
        const string exchangeDn =
            "/o=Contoso/ou=Exchange Administrative Group/cn=Recipients/cn=bob";
        var item = new FakeMeetingItem
        {
            EntryID = "mtg-4",
            Subject = "Exchange DN sender",
            ReceivedTime = FixedNow,
            MessageClass = "IPM.Schedule.Meeting.Request",
            SenderName = "Bob",
            SenderEmailAddress = exchangeDn,
            MeetingType = 0,
            ConversationID = "conv-mtg-4",
            Sender = new FakeSender
            {
                Address = exchangeDn,
                AddressEntry = new FakeAddressEntry
                {
                    Address = exchangeDn,
                    ExchangeUser = new FakeExchangeUser { PrimarySmtpAddress = "bob@contoso.com" },
                },
            },
        };

        // Act
        var msg = await ScanSingleAsync(item);

        // Assert
        msg.SenderEmailResolved.Should()
            .Be("bob@contoso.com", "the Exchange DN resolves to the true SMTP address");
        msg.SenderEmailResolved.Should().NotBe(exchangeDn);
    }
}
