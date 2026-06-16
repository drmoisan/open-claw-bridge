using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Round-trip persistence tests for issue #73 (AC-10): the bridge <see cref="CacheRepository"/> must
/// write and read back all four new <see cref="MessageDto"/> resolved fields
/// (<c>sender_email_resolved</c>, <c>from_email_address</c>, <c>conversation_id</c>,
/// <c>meeting_message_type</c>) identically, and its messages schema migration must be idempotent.
/// Uses in-memory shared-cache SQLite so no temp files are created.
/// </summary>
[TestClass]
public sealed class CacheRepositoryMessageFieldsTests
{
    private static MessageDto BuildMessage(
        string bridgeId,
        string itemKind = "meeting",
        string? senderEmailResolved = "resolved@contoso.com",
        string? fromEmailAddress = "delegate@contoso.com",
        string? conversationId = "conv-xyz",
        int? meetingMessageType = 0
    )
    {
        var received = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        return new MessageDto(
            BridgeId: bridgeId,
            ItemKind: itemKind,
            Subject: "Resolved fields round-trip",
            ReceivedUtc: received,
            SentUtc: received.AddMinutes(-5),
            Importance: 1,
            Sensitivity: 0,
            Unread: true,
            HasAttachments: false,
            MessageClass: "IPM.Schedule.Meeting.Request",
            SenderName: "Sender",
            SenderEmail: "sender@contoso.com",
            ToJson: """[{"name":"To","email":"to@contoso.com"}]""",
            CcJson: """[{"name":"Cc","email":"cc@contoso.com"}]""",
            BodyPreview: "preview",
            ProtectedFieldsAvailable: true,
            IsRedacted: false,
            SenderEmailResolved: senderEmailResolved,
            FromEmailAddress: fromEmailAddress,
            ConversationId: conversationId,
            MeetingMessageType: meetingMessageType
        );
    }

    [TestMethod]
    public async Task UpsertMessage_then_GetMessage_should_round_trip_all_four_resolved_fields()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=msg-pop-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var message = BuildMessage("bridge-msg-pop");

        // Act
        await repo.UpsertMessageAsync("entry-1", "store-1", message);
        var loaded = await repo.GetMessageAsync("bridge-msg-pop");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.SenderEmailResolved.Should().Be("resolved@contoso.com");
        loaded.FromEmailAddress.Should().Be("delegate@contoso.com");
        loaded.ConversationId.Should().Be("conv-xyz");
        loaded.MeetingMessageType.Should().Be(0);
    }

    [TestMethod]
    public async Task UpsertMessage_then_GetMessage_should_round_trip_null_resolved_fields()
    {
        // Arrange: ordinary mail with no meeting type and null resolved values.
        using var repo = new CacheRepository(
            $"Data Source=msg-null-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var message = BuildMessage(
            "bridge-msg-null",
            itemKind: "mail",
            senderEmailResolved: null,
            fromEmailAddress: null,
            conversationId: null,
            meetingMessageType: null
        );

        // Act
        await repo.UpsertMessageAsync("entry-2", "store-2", message);
        var loaded = await repo.GetMessageAsync("bridge-msg-null");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.SenderEmailResolved.Should().BeNull();
        loaded.FromEmailAddress.Should().BeNull();
        loaded.ConversationId.Should().BeNull();
        loaded.MeetingMessageType.Should().BeNull();
    }

    [TestMethod]
    public async Task InitializeAsync_should_be_idempotent_for_messages_schema()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=msg-idem-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );

        // Act: initializing twice must not raise a "duplicate column" error.
        await repo.InitializeAsync();
        var secondInit = async () => await repo.InitializeAsync();

        // Assert
        await secondInit.Should().NotThrowAsync("the messages schema migration must be idempotent");

        // And a round-trip still works after the second migration (stable schema).
        var message = BuildMessage("bridge-msg-idem");
        await repo.UpsertMessageAsync("entry-3", "store-3", message);
        var loaded = await repo.GetMessageAsync("bridge-msg-idem");
        loaded.Should().NotBeNull();
        loaded!.ConversationId.Should().Be("conv-xyz");
        loaded.MeetingMessageType.Should().Be(0);
    }
}
