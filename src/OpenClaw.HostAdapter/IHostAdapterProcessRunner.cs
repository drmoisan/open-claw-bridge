using System.Diagnostics;
using System.Text.Json;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

internal interface IHostAdapterProcessRunner
{
    Task<AdapterCommandResult<T>> ExecuteAsync<T>(
        ProcessStartInfo startInfo,
        string requestId,
        BridgeStatusDto? bridge,
        Func<JsonElement, T> projector,
        CancellationToken cancellationToken
    );
}
