using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Test double for <see cref="IOutlookMailSender"/>. Records the received
/// <see cref="SendMailComRequest"/> on the success path; throws
/// <see cref="InvalidOperationException"/> when <see cref="ThrowOnSend"/> is set, exercising the
/// dispatch failure -> <c>INTERNAL_ERROR</c> mapping. No live COM.
/// </summary>
/// <remarks>
/// Lives in its own file (rather than extending <c>MailBridgeRuntimeTestDoubles.cs</c>, which is at
/// the 500-line cap) to honor the repository file-size limit.
/// </remarks>
internal sealed class FakeOutlookMailSender : IOutlookMailSender
{
    public bool ThrowOnSend { get; set; }
    public SendMailComRequest? Received { get; private set; }
    public int SendCalls { get; private set; }

    public Task SendMailAsync(SendMailComRequest request, CancellationToken cancellationToken)
    {
        SendCalls++;
        Received = request;
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("Simulated COM send failure.");
        }

        return Task.CompletedTask;
    }
}
