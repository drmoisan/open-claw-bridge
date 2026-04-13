using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Contracts;

/// <summary>
/// Carries request-scoped metadata attached to every HostAdapter response.
/// </summary>
/// <param name="RequestId">The correlation identifier for the current request.</param>
/// <param name="AdapterVersion">The HostAdapter version reported by the running service.</param>
/// <param name="Bridge">The current bridge status snapshot when available.</param>
public sealed record ApiMeta(string RequestId, string AdapterVersion, BridgeStatusDto? Bridge);
