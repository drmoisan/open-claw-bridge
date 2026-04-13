namespace OpenClaw.HostAdapter.Contracts;

/// <summary>
/// Describes a normalized HostAdapter failure shape and its retry guidance.
/// </summary>
/// <param name="Code">The adapter-level error code returned to the caller.</param>
/// <param name="Message">The human-readable error description.</param>
/// <param name="BridgeErrorCode">The underlying bridge error code when one exists.</param>
/// <param name="Retryable">Whether the caller may retry the request without changing inputs.</param>
public sealed record ApiError(
    string Code,
    string Message,
    string? BridgeErrorCode = null,
    bool Retryable = false
);
