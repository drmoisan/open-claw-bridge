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

    /// <summary>
    /// Mail-item fake whose <c>Sender</c> property getter throws, driving the catch path in
    /// <c>ResolveSenderSmtp</c> (P2-T1 scenario b).
    /// </summary>
    private sealed class FakeAdapterMailItemWithThrowingSender
    {
        public string? SenderEmailAddress { get; init; }

        public object Sender =>
            throw new InvalidOperationException("Simulated COM failure on Sender.");
    }

    /// <summary>
    /// Mail-item fake that also exposes <c>SentOnBehalfOfEmailAddress</c>, which
    /// <c>ComMessageSource</c> reads for the on-behalf-of / from resolution path (P2-T2).
    /// </summary>
    private sealed class FakeAdapterMailItemWithOnBehalfOf
    {
        public string? SenderEmailAddress { get; init; }
        public string? SentOnBehalfOfEmailAddress { get; init; }
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

    // ---- P2-T1: ResolveSenderSmtp tests ----

    [TestMethod]
    public void ResolveSenderSmtp_should_return_smtp_from_property_accessor()
    {
        // Arrange: sender with an address entry whose PropertyAccessor resolves a true SMTP address.
        var addressEntry = new FakeAddressEntry
        {
            PropertyAccessor = new FakePropertyAccessor { SmtpAddress = "true.smtp@test.com" },
        };
        var sender = new FakeSender { AddressEntry = addressEntry };
        var item = new FakeAdapterMailItem { Sender = sender, SenderEmailAddress = "raw@test.com" };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: PropertyAccessor path returns the true SMTP, not the raw value.
        source.SenderEmailResolved.Should().Be("true.smtp@test.com");
    }

    [TestMethod]
    public void ResolveSenderSmtp_should_fall_back_to_raw_sender_when_resolution_throws()
    {
        // Arrange: Sender property is a throwing fake, so ResolveAddressEntrySmtp catch path fires.
        var item = new FakeAdapterMailItemWithThrowingSender
        {
            SenderEmailAddress = "fallback@test.com",
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: catch path falls through to SenderEmailAddress.
        source.SenderEmailResolved.Should().Be("fallback@test.com");
    }

    [TestMethod]
    public void ResolveSenderSmtp_should_return_null_when_raw_sender_email_is_null_or_whitespace()
    {
        // Arrange: no Sender object, no SenderEmailAddress.
        var item = new FakeAdapterMailItem { Sender = null, SenderEmailAddress = "   " };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: NormalizeAddress("   ") yields null.
        source.SenderEmailResolved.Should().BeNull();
    }

    // ---- P2-T2: ResolveFromSmtp / ResolveOnBehalfOfSmtp tests ----

    [TestMethod]
    public void ResolveFromSmtp_should_delegate_to_sender_when_sent_on_behalf_of_is_empty()
    {
        // Arrange: SentOnBehalfOfEmailAddress is whitespace → ResolveFromSmtp calls ResolveSenderSmtp.
        var addressEntry = new FakeAddressEntry
        {
            PropertyAccessor = new FakePropertyAccessor { SmtpAddress = "sender.smtp@test.com" },
        };
        var sender = new FakeSender { AddressEntry = addressEntry };
        var item = new FakeAdapterMailItemWithOnBehalfOf
        {
            Sender = sender,
            SentOnBehalfOfEmailAddress = "",
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source.FromEmailAddress.Should().Be("sender.smtp@test.com");
    }

    [TestMethod]
    public void ResolveFromSmtp_should_return_on_behalf_of_value_directly_when_it_looks_like_smtp()
    {
        // Arrange: SentOnBehalfOfEmailAddress is already SMTP-shaped → returned directly.
        var sender = new FakeSender { Address = "sender@test.com" };
        var item = new FakeAdapterMailItemWithOnBehalfOf
        {
            Sender = sender,
            SentOnBehalfOfEmailAddress = "delegate@test.com",
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source.FromEmailAddress.Should().Be("delegate@test.com");
    }

    [TestMethod]
    public void ResolveFromSmtp_should_resolve_non_smtp_on_behalf_of_via_address_entry()
    {
        // Arrange: SentOnBehalfOfEmailAddress is a legacy DN; sender has a PropertyAccessor.
        var addressEntry = new FakeAddressEntry
        {
            PropertyAccessor = new FakePropertyAccessor { SmtpAddress = "resolved@test.com" },
        };
        var sender = new FakeSender { AddressEntry = addressEntry };
        var item = new FakeAdapterMailItemWithOnBehalfOf
        {
            Sender = sender,
            SentOnBehalfOfEmailAddress = "/o=ExchangeLabs/ou=delegate",
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: ResolveAddressEntrySmtp returns the true SMTP.
        source.FromEmailAddress.Should().Be("resolved@test.com");
    }

    [TestMethod]
    public void ResolveFromSmtp_should_return_normalized_raw_on_behalf_of_when_chain_returns_empty()
    {
        // Arrange: SentOnBehalfOfEmailAddress is a legacy DN; Sender is null so
        // ResolveAddressEntrySmtp returns null → ResolveOnBehalfOfSmtp returns NormalizeAddress(raw).
        var item = new FakeAdapterMailItemWithOnBehalfOf
        {
            Sender = null,
            SentOnBehalfOfEmailAddress = "  /o=ExchangeLabs/delegate  ",
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: chain empty → NormalizeAddress applied to the raw on-behalf-of value.
        source.FromEmailAddress.Should().Be("/o=ExchangeLabs/delegate");
    }

    // ---- P2-T3: ResolveAddressEntrySmtp tests ----

    [TestMethod]
    public void ResolveAddressEntrySmtp_should_return_null_when_sender_is_null()
    {
        // Arrange: Sender is null → ResolveAddressEntrySmtp returns null immediately.
        var item = new FakeAdapterMailItem { Sender = null, SenderEmailAddress = null };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source.SenderEmailResolved.Should().BeNull();
    }

    [TestMethod]
    public void ResolveAddressEntrySmtp_should_return_smtp_via_sender_address_when_smtp_shaped()
    {
        // Arrange: sender has an SMTP-shaped Address; no PropertyAccessor, no ExchangeUser.
        var sender = new FakeSender { Address = "smtp.sender@test.com" };
        var item = new FakeAdapterMailItem { Sender = sender };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source.SenderEmailResolved.Should().Be("smtp.sender@test.com");
    }

    [TestMethod]
    public void ResolveAddressEntrySmtp_should_return_entry_address_when_smtp_shaped()
    {
        // Arrange: sender.Address is not SMTP-shaped; address entry Address is SMTP-shaped.
        var addressEntry = new FakeAddressEntry { Address = "entry@test.com" };
        var sender = new FakeSender
        {
            Address = "/o=ExchangeLabs/not-smtp",
            AddressEntry = addressEntry,
        };
        var item = new FakeAdapterMailItem { Sender = sender };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert
        source.SenderEmailResolved.Should().Be("entry@test.com");
    }

    [TestMethod]
    public void ResolveAddressEntrySmtp_should_fall_back_to_entry_address_when_not_smtp_shaped()
    {
        // Arrange: neither sender.Address nor entry.Address is SMTP-shaped; returns entry address.
        var addressEntry = new FakeAddressEntry { Address = "/o=ExchangeLabs/entry-dn" };
        var sender = new FakeSender
        {
            Address = "/o=ExchangeLabs/sender-dn",
            AddressEntry = addressEntry,
        };
        var item = new FakeAdapterMailItem { Sender = sender };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: neither is SMTP-shaped; returns entry address (non-null preferred).
        source.SenderEmailResolved.Should().Be("/o=ExchangeLabs/entry-dn");
    }

    // ---- P2-T4: ResolveViaPropertyAccessor / ResolveViaExchangeUser reachable surface ----

    [TestMethod]
    public void ResolveViaPropertyAccessor_should_return_null_when_property_accessor_is_null()
    {
        // Arrange: address entry exists but has no PropertyAccessor.
        var addressEntry = new FakeAddressEntry { PropertyAccessor = null };
        var sender = new FakeSender { AddressEntry = addressEntry };
        var item = new FakeAdapterMailItem { Sender = sender };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: PropertyAccessor null path → null from ResolveViaPropertyAccessor.
        // Falls through to ExchangeUser (also null) and Address (null) → SenderEmailResolved = null.
        source.SenderEmailResolved.Should().BeNull();
    }

    [TestMethod]
    public void ResolveViaExchangeUser_should_return_null_when_exchange_user_is_null()
    {
        // Arrange: address entry returns null from GetExchangeUser().
        var addressEntry = new FakeAddressEntry { ExchangeUser = null };
        var sender = new FakeSender { AddressEntry = addressEntry };
        var item = new FakeAdapterMailItem { Sender = sender };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: ExchangeUser null path → null from ResolveViaExchangeUser.
        source.SenderEmailResolved.Should().BeNull();
    }

    [TestMethod]
    public void ResolveViaExchangeUser_should_return_primary_smtp_address()
    {
        // Arrange: address entry returns an ExchangeUser with a PrimarySmtpAddress.
        var addressEntry = new FakeAddressEntry
        {
            ExchangeUser = new FakeExchangeUser { PrimarySmtpAddress = "exchange@test.com" },
        };
        var sender = new FakeSender { AddressEntry = addressEntry };
        var item = new FakeAdapterMailItem { Sender = sender };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act / Assert: ExchangeUser path returns PrimarySmtpAddress.
        source.SenderEmailResolved.Should().Be("exchange@test.com");
    }

    // ---- P2-T5: Recipient enumeration (cache short-circuit) ----

    [TestMethod]
    public void EnsureRecipients_should_cache_and_short_circuit_on_second_access()
    {
        // Arrange: one To recipient; both ToRecipients and CcRecipients are accessed twice.
        var recipients = new FakeRecipients(
            new FakeRecipient
            {
                Type = 1,
                Name = "Bob",
                Address = "bob@test.com",
            }
        );
        var item = new FakeAdapterMailItem { Recipients = recipients };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act: first access populates the cache.
        var toFirst = source.ToRecipients;
        var ccFirst = source.CcRecipients;

        // Act: second access should return the same cached instances (short-circuit).
        var toSecond = source.ToRecipients;
        var ccSecond = source.CcRecipients;

        // Assert
        toFirst.Should().BeSameAs(toSecond, "cached instance returned on second call");
        ccFirst.Should().BeSameAs(ccSecond, "cached instance returned on second call");
        toFirst.Should().ContainSingle();
    }

    // ---- P2-T6: Pure helpers LooksLikeSmtp, NormalizeAddress, MeetingMessageType ----

    [DataTestMethod]
    [DataRow("user@example.com", true)]
    [DataRow("user@sub.domain.org", true)]
    [DataRow("/o=ExchangeLabs/ou=Exchange/recipient", false)]
    [DataRow("/O=DOMAIN/alias", false)]
    [DataRow("no-at-sign-value", false)]
    public void LooksLikeSmtp_should_classify_addresses_correctly(
        string? address,
        bool expectedSmtp
    )
    {
        // Drive LooksLikeSmtp via the on-behalf-of path: supply the address as
        // SentOnBehalfOfEmailAddress with Sender=null. When LooksLikeSmtp returns true the value is
        // returned directly; when false, ResolveAddressEntrySmtp(null) returns null and
        // NormalizeAddress(raw) is returned.
        var item = new FakeAdapterMailItemWithOnBehalfOf
        {
            Sender = null,
            SentOnBehalfOfEmailAddress = address,
        };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);
        var resolved = source.FromEmailAddress;

        if (expectedSmtp)
        {
            resolved.Should().Be(address);
        }
        else
        {
            // Non-SMTP value: ResolveOnBehalfOfSmtp → NormalizeAddress(address).
            // For non-null, non-whitespace non-SMTP values, NormalizeAddress trims and returns them.
            resolved.Should().Be(address?.Trim());
        }
    }

    [DataTestMethod]
    [DataRow(null, false)]
    [DataRow("   ", false)]
    public void LooksLikeSmtp_should_return_false_for_null_or_whitespace(
        string? address,
        bool expectedSmtp
    )
    {
        // Null/whitespace on-behalf-of → ResolveFromSmtp delegates to ResolveSenderSmtp (not
        // the LooksLikeSmtp path). Use SenderEmailResolved to verify null/whitespace handling.
        _ = expectedSmtp; // not used; these are always false
        var item = new FakeAdapterMailItem { Sender = null, SenderEmailAddress = address };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);
        source.SenderEmailResolved.Should().BeNull();
    }

    [DataTestMethod]
    [DataRow(null, null)]
    [DataRow("   ", null)]
    [DataRow("  addr@x.com  ", "addr@x.com")]
    [DataRow("addr@x.com", "addr@x.com")]
    public void NormalizeAddress_should_trim_or_return_null(string? input, string? expected)
    {
        // Drive NormalizeAddress via the sender-fallback path (sender=null, SenderEmailAddress=input).
        var item = new FakeAdapterMailItem { Sender = null, SenderEmailAddress = input };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);
        source.SenderEmailResolved.Should().Be(expected);
    }
}
