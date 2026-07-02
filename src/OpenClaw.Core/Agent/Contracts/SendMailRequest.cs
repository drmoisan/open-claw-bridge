namespace OpenClaw.Core.Agent;

/// <summary>
/// Graph-shaped outbound mail request (D6). The send endpoint is gated by the
/// <see cref="AgentPolicyOptions.SendEnabled"/> kill switch.
/// </summary>
/// <param name="Subject">The reply subject.</param>
/// <param name="BodyContent">The reply body content.</param>
/// <param name="BodyContentType">The body content type (for example <c>text</c> or <c>html</c>).</param>
/// <param name="ToRecipients">The To recipients.</param>
/// <param name="CcRecipients">The Cc recipients.</param>
/// <param name="InReplyToMessageId">The message identifier being replied to, or null for a new mail.</param>
public sealed record SendMailRequest(
    string Subject,
    string BodyContent,
    string BodyContentType,
    IReadOnlyList<AttendeeDto> ToRecipients,
    IReadOnlyList<AttendeeDto> CcRecipients,
    string? InReplyToMessageId
);
