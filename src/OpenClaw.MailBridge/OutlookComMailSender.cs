using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace OpenClaw.MailBridge;

/// <summary>
/// Sends mail through Outlook COM on the dedicated STA thread. Obtains the live
/// <c>Application</c> from <see cref="IOutlookApplicationProvider"/> (set by
/// <see cref="OutlookScanner"/>), creates a <c>MailItem</c>, populates subject/body/recipients,
/// maps <c>DeleteAfterSubmit = !SaveToSentItems</c>, calls <c>Send()</c>, and releases every COM
/// object in a <c>finally</c>. COM interop is confined to this file. Send failures propagate
/// (fail-fast) to the caller.
/// </summary>
internal sealed class OutlookComMailSender : IOutlookMailSender
{
    private const int OlMailItem = 0;
    private const int OlTo = 1;
    private const int OlCc = 2;
    private const int OlBcc = 3;

    private readonly IOutlookStaExecutor _sta;
    private readonly IOutlookApplicationProvider _applicationProvider;
    private readonly ILogger<OutlookComMailSender> _logger;

    public OutlookComMailSender(
        IOutlookStaExecutor sta,
        IOutlookApplicationProvider applicationProvider,
        ILogger<OutlookComMailSender> logger
    )
    {
        _sta = sta;
        _applicationProvider = applicationProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendMailAsync(SendMailComRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var application =
            _applicationProvider.Application
            ?? throw new InvalidOperationException("Outlook is not connected; cannot send mail.");

        await _sta.InvokeAsync(() =>
        {
            SendOnSta(application, request);
            return true;
        });
    }

    /// <summary>
    /// Performs the live Outlook COM send on the STA thread. Excluded from coverage because every
    /// statement touches live COM; exercised by the Phase 9 integration test
    /// (<c>OutlookComMailSenderIntegrationTests</c>) on a live-Outlook host.
    /// </summary>
    [ExcludeFromCodeCoverage(
        Justification = "Live Outlook COM send; covered by [TestCategory(\"Integration\")] OutlookComMailSenderIntegrationTests on a live-Outlook host."
    )]
    private void SendOnSta(object application, SendMailComRequest request)
    {
        object? mailItem = null;
        try
        {
            mailItem =
                OutlookComHelpers.InvokeMember(application, "CreateItem", OlMailItem)
                ?? throw new InvalidOperationException("Outlook CreateItem returned null.");

            OutlookComHelpers.SetMemberValue(mailItem, "Subject", request.Subject);

            if (string.Equals(request.BodyContentType, "HTML", StringComparison.OrdinalIgnoreCase))
            {
                OutlookComHelpers.SetMemberValue(mailItem, "HTMLBody", request.BodyContent);
            }
            else
            {
                OutlookComHelpers.SetMemberValue(mailItem, "Body", request.BodyContent);
            }

            AddRecipients(mailItem, request.To, OlTo);
            AddRecipients(mailItem, request.Cc, OlCc);
            AddRecipients(mailItem, request.Bcc, OlBcc);

            OutlookComHelpers.SetMemberValue(
                mailItem,
                "DeleteAfterSubmit",
                !request.SaveToSentItems
            );

            OutlookComHelpers.InvokeMember(mailItem, "Send");
            _logger.LogInformation(
                "Outlook send completed (subject length {SubjectLength}, saveToSentItems {SaveToSentItems}).",
                request.Subject.Length,
                request.SaveToSentItems
            );
        }
        finally
        {
            ReleaseRecipients(mailItem);
            new ComActiveObject().ReleaseAll(mailItem);
        }
    }

    /// <summary>
    /// Adds each address to the mail item's <c>Recipients</c> collection with the given Outlook
    /// recipient type, releasing each transient recipient and the collection wrapper.
    /// </summary>
    [ExcludeFromCodeCoverage(
        Justification = "Live Outlook COM recipient resolution; covered by integration tests."
    )]
    private static void AddRecipients(object mailItem, IReadOnlyList<string> addresses, int type)
    {
        if (addresses.Count == 0)
        {
            return;
        }

        object? recipients = null;
        try
        {
            recipients = OutlookComHelpers.GetMemberValue(mailItem, "Recipients");
            if (recipients is null)
            {
                throw new InvalidOperationException("Outlook Recipients collection was null.");
            }

            foreach (var address in addresses)
            {
                object? recipient = null;
                try
                {
                    recipient = OutlookComHelpers.InvokeMember(recipients, "Add", address);
                    if (recipient is not null)
                    {
                        OutlookComHelpers.SetMemberValue(recipient, "Type", type);
                    }
                }
                finally
                {
                    new ComActiveObject().ReleaseAll(recipient);
                }
            }
        }
        finally
        {
            new ComActiveObject().ReleaseAll(recipients);
        }
    }

    [ExcludeFromCodeCoverage(
        Justification = "Live Outlook COM resolve-all on the Recipients collection; covered by integration tests."
    )]
    private static void ReleaseRecipients(object? mailItem)
    {
        if (mailItem is null)
        {
            return;
        }

        object? recipients = OutlookComHelpers.GetOptionalMemberValue(mailItem, "Recipients");
        new ComActiveObject().ReleaseAll(recipients);
    }
}
