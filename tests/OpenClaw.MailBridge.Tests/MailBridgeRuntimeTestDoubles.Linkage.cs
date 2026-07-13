using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Message-to-event linkage behavior for the <see cref="FakeScanStateRepository"/> test double
/// (issue #146). Split into a partial-class file so the main test-doubles file stays under the
/// 500-line cap. Mirrors the real <c>CacheRepository.GetEventForMessageAsync</c> join: resolve the
/// message's stored <c>LinkedGlobalAppointmentId</c> to the newest matching event, or null.
/// </summary>
internal sealed partial class FakeScanStateRepository
{
    public Task<EventDto?> GetEventForMessageAsync(
        string messageBridgeId,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !Messages.TryGetValue(messageBridgeId, out var message)
            || string.IsNullOrWhiteSpace(message.LinkedGlobalAppointmentId)
        )
        {
            return Task.FromResult<EventDto?>(null);
        }

        var evt = Events
            .Values.Where(x => x.GlobalAppointmentId == message.LinkedGlobalAppointmentId)
            .OrderByDescending(x => x.StartUtc)
            .FirstOrDefault();
        return Task.FromResult(evt);
    }
}
