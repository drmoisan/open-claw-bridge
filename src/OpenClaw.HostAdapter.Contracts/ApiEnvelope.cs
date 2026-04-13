using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Contracts;

/// <summary>
/// Wraps every HostAdapter response in a consistent success or failure envelope.
/// </summary>
/// <typeparam name="T">The response payload type when the request succeeds.</typeparam>
public sealed record ApiEnvelope<T>(bool Ok, T? Data, ApiMeta Meta, ApiError? Error);
