namespace OpenClaw.HostAdapter.Contracts;

/// <summary>
/// Graph-shaped request body for <c>POST /users/{assistantMailbox}/sendMail</c>.
/// Mirrors the Microsoft Graph <c>sendMail</c> action shape so the local adapter and a
/// future Graph backend share one contract.
/// </summary>
/// <param name="Message">The message to send.</param>
/// <param name="SaveToSentItems">
/// When <see langword="true"/> (the default) the sent message is saved to Sent Items;
/// when <see langword="false"/> it is not. Maps to Outlook
/// <c>DeleteAfterSubmit = !SaveToSentItems</c> on the bridge.
/// </param>
public sealed record SendMailRequest(SendMailMessageDto Message, bool SaveToSentItems = true);

/// <summary>
/// Graph-shaped message payload for an outbound send.
/// </summary>
/// <param name="Subject">The message subject. An empty subject is permitted (Graph-permissive).</param>
/// <param name="Body">The message body and its content type.</param>
/// <param name="ToRecipients">The primary (To) recipients.</param>
/// <param name="CcRecipients">The carbon-copy (CC) recipients, if any.</param>
/// <param name="BccRecipients">The blind-carbon-copy (BCC) recipients, if any.</param>
public sealed record SendMailMessageDto(
    string Subject,
    SendMailBodyDto Body,
    IReadOnlyList<SendMailRecipientDto> ToRecipients,
    IReadOnlyList<SendMailRecipientDto>? CcRecipients = null,
    IReadOnlyList<SendMailRecipientDto>? BccRecipients = null
);

/// <summary>
/// Graph-shaped message body.
/// </summary>
/// <param name="ContentType">The content type, either <c>Text</c> or <c>HTML</c> (case-insensitive).</param>
/// <param name="Content">The body content.</param>
public sealed record SendMailBodyDto(string ContentType, string Content);

/// <summary>
/// Graph-shaped recipient wrapper.
/// </summary>
/// <param name="EmailAddress">The recipient email address.</param>
public sealed record SendMailRecipientDto(SendMailEmailAddressDto EmailAddress);

/// <summary>
/// Graph-shaped email address.
/// </summary>
/// <param name="Address">The SMTP address (for example <c>a@b.c</c>).</param>
/// <param name="Name">An optional display name.</param>
public sealed record SendMailEmailAddressDto(string Address, string? Name = null);
