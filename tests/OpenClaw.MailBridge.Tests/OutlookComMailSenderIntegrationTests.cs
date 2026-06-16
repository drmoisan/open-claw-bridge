using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Real-COM integration tests for <see cref="OutlookComMailSender"/>. These exercise the
/// <c>[ExcludeFromCodeCoverage]</c> live-COM-only members on a live-Outlook host. They are gated:
/// when Outlook is not available (non-Windows, or no running Outlook instance) the test reports
/// <see cref="Assert.Inconclusive(string)"/> so CI runners without Outlook do not fail. No temporary
/// files; no Thread.Sleep/Task.Delay.
/// </summary>
[TestClass]
public class OutlookComMailSenderIntegrationTests
{
    private static (IOutlookStaExecutor sta, IOutlookApplicationProvider provider)? TryConnect()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var sta = new OutlookStaExecutor();
        object? application;
        try
        {
            // Reuse a running Outlook instance; do not autostart from the test.
            application = sta.InvokeAsync(() => new ComActiveObject().TryGet("Outlook.Application"))
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            sta.Dispose();
            return null;
        }

        if (application is null)
        {
            sta.Dispose();
            return null;
        }

        var provider = new OutlookApplicationProvider();
        provider.Set(application);
        return (sta, provider);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SendMail_with_one_recipient_should_create_a_sent_items_entry()
    {
        var connection = TryConnect();
        if (connection is null)
        {
            Assert.Inconclusive(
                "Live Outlook is not available on this host; the COM send path is covered-by-design pending a live run."
            );
            return;
        }

        var (sta, provider) = connection.Value;
        try
        {
            var sender = new OutlookComMailSender(
                sta,
                provider,
                NullLogger<OutlookComMailSender>.Instance
            );
            var subject = $"OpenClaw integration test {Guid.NewGuid():N}";
            var selfAddress = ResolveCurrentUserSmtp(sta, provider);

            var request = new SendMailComRequest(
                subject,
                "Text",
                "OpenClaw issue #75 integration send.",
                To: [selfAddress],
                Cc: [],
                Bcc: [],
                SaveToSentItems: true
            );

            await sender.SendMailAsync(request, CancellationToken.None);

            var found = FindInSentItems(sta, provider, subject);
            found.Should().BeTrue("a saved-to-sent-items send should create a Sent Items entry");
        }
        finally
        {
            sta.Dispose();
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SendMail_end_to_end_through_seam_should_complete_and_release_com()
    {
        var connection = TryConnect();
        if (connection is null)
        {
            Assert.Inconclusive(
                "Live Outlook is not available on this host; the COM send path is covered-by-design pending a live run."
            );
            return;
        }

        var (sta, provider) = connection.Value;
        try
        {
            IOutlookMailSender sender = new OutlookComMailSender(
                sta,
                provider,
                NullLogger<OutlookComMailSender>.Instance
            );
            var selfAddress = ResolveCurrentUserSmtp(sta, provider);
            var request = new SendMailComRequest(
                $"OpenClaw e2e {Guid.NewGuid():N}",
                "HTML",
                "<p>OpenClaw issue #75 end-to-end send.</p>",
                To: [selfAddress],
                Cc: [],
                Bcc: [],
                SaveToSentItems: true
            );

            // Completing without throwing indicates the full create -> populate -> Send -> release
            // path succeeded; release happens in the sender's finally.
            await sender.SendMailAsync(request, CancellationToken.None);
        }
        finally
        {
            sta.Dispose();
        }
    }

    private static string ResolveCurrentUserSmtp(
        IOutlookStaExecutor sta,
        IOutlookApplicationProvider provider
    ) =>
        sta.InvokeAsync(() =>
            {
                var app = provider.Application!;
                var ns = OutlookComHelpers.InvokeMember(app, "GetNamespace", "MAPI")!;
                var currentUser = OutlookComHelpers.GetMemberValue(ns, "CurrentUser")!;
                var addressEntry = OutlookComHelpers.GetMemberValue(currentUser, "AddressEntry");
                var smtp = addressEntry is null
                    ? null
                    : OutlookComHelpers.GetOptionalString(addressEntry, "Address");
                return string.IsNullOrWhiteSpace(smtp) ? "test@example.com" : smtp;
            })
            .GetAwaiter()
            .GetResult();

    private static bool FindInSentItems(
        IOutlookStaExecutor sta,
        IOutlookApplicationProvider provider,
        string subject
    ) =>
        sta.InvokeAsync(() =>
            {
                var app = provider.Application!;
                var ns = OutlookComHelpers.InvokeMember(app, "GetNamespace", "MAPI")!;
                // 5 == olFolderSentMail
                var sent = OutlookComHelpers.InvokeMember(ns, "GetDefaultFolder", 5)!;
                var items = OutlookComHelpers.GetMemberValue(sent, "Items")!;
                var count = OutlookComHelpers.GetOptionalInt(items, "Count") ?? 0;
                for (var i = 1; i <= count; i++)
                {
                    var item = OutlookComHelpers.GetOptionalIndexedItem(items, i);
                    if (item is null)
                    {
                        continue;
                    }

                    var itemSubject = OutlookComHelpers.GetOptionalString(item, "Subject");
                    if (string.Equals(itemSubject, subject, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            })
            .GetAwaiter()
            .GetResult();
}
