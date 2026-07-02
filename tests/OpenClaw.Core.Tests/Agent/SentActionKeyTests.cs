using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the pure dedupe-key builder <see cref="SentActionKey"/> (issue #101,
/// AC-2): key format, the <see cref="SentActionKey.ProposalReply"/> constant, and
/// argument validation for each of the three components.
/// </summary>
[TestClass]
public sealed class SentActionKeyTests
{
    [TestMethod]
    public void Build_WithValidComponents_ReturnsColonJoinedKeyInFixedOrder()
    {
        // Arrange
        var mailbox = "owner@contoso.com";
        var messageId = "msg-1";

        // Act
        var key = SentActionKey.Build(mailbox, messageId, SentActionKey.ProposalReply);

        // Assert
        key.Should().Be("owner@contoso.com:msg-1:proposal-reply");
    }

    [TestMethod]
    public void ProposalReply_Constant_IsProposalReplyLiteral()
    {
        // Arrange / Act / Assert
        SentActionKey.ProposalReply.Should().Be("proposal-reply");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "null mailbox")]
    [DataRow("", DisplayName = "empty mailbox")]
    [DataRow("   ", DisplayName = "whitespace-only mailbox")]
    public void Build_WithInvalidMailbox_ThrowsArgumentExceptionNamingMailbox(string? mailbox)
    {
        // Arrange
        var act = () => SentActionKey.Build(mailbox!, "msg-1", SentActionKey.ProposalReply);

        // Act / Assert
        act.Should().Throw<ArgumentException>().WithParameterName("mailbox");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "null messageId")]
    [DataRow("", DisplayName = "empty messageId")]
    [DataRow("   ", DisplayName = "whitespace-only messageId")]
    public void Build_WithInvalidMessageId_ThrowsArgumentExceptionNamingMessageId(string? messageId)
    {
        // Arrange
        var act = () =>
            SentActionKey.Build("owner@contoso.com", messageId!, SentActionKey.ProposalReply);

        // Act / Assert
        act.Should().Throw<ArgumentException>().WithParameterName("messageId");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "null actionType")]
    [DataRow("", DisplayName = "empty actionType")]
    [DataRow("   ", DisplayName = "whitespace-only actionType")]
    public void Build_WithInvalidActionType_ThrowsArgumentExceptionNamingActionType(
        string? actionType
    )
    {
        // Arrange
        var act = () => SentActionKey.Build("owner@contoso.com", "msg-1", actionType!);

        // Act / Assert
        act.Should().Throw<ArgumentException>().WithParameterName("actionType");
    }
}
