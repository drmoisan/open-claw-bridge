namespace OpenClaw.MailBridge;

/// <summary>
/// Sends an outbound message through Outlook on the dedicated STA thread. COM interop is confined
/// to the implementation; this seam exposes only plain data so callers (the RPC dispatch) never
/// touch live COM. The seam reserves an optional <c>FromEmailAddress</c> for the PI-1
/// send-on-behalf feature so a future caller can supply it without breaking this signature (AC-09).
/// </summary>
internal interface IOutlookMailSender
{
    /// <summary>
    /// Creates, populates, and submits an Outlook mail item, releasing every COM object it obtains.
    /// COM/send failures propagate to the caller (fail-fast).
    /// </summary>
    /// <param name="request">The flattened, plain-data send request.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    Task SendMailAsync(SendMailComRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Plain-data send request passed across the <see cref="IOutlookMailSender"/> seam. Carries no COM
/// type. Recipient lists are flat SMTP-address strings; CC and BCC may be empty.
/// </summary>
/// <param name="Subject">The message subject. May be empty (D-F).</param>
/// <param name="BodyContentType">The body content type, <c>Text</c> or <c>HTML</c> (case-insensitive).</param>
/// <param name="BodyContent">The body content.</param>
/// <param name="To">The To recipient addresses.</param>
/// <param name="Cc">The CC recipient addresses (possibly empty).</param>
/// <param name="Bcc">The BCC recipient addresses (possibly empty).</param>
/// <param name="SaveToSentItems">When <see langword="true"/>, save to Sent Items (<c>DeleteAfterSubmit = false</c>).</param>
/// <param name="FromEmailAddress">
/// Reserved for PI-1 send-on-behalf (AC-09). Defaults to <see langword="null"/>; ignored by the MVP
/// implementation. Present so a future caller can supply a sending identity without breaking the seam.
/// </param>
internal sealed record SendMailComRequest(
    string Subject,
    string BodyContentType,
    string BodyContent,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    bool SaveToSentItems,
    string? FromEmailAddress = null
);
