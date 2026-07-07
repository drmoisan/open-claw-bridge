namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// Reduced, generic-free projection of one probe read's <c>ApiEnvelope&lt;T&gt;</c>
/// (spec D2). Carries only the four fields the scope-boundary classifier and evaluator
/// need: the success flag and the three F13 D5 error fields. <see cref="ErrorMessage"/>
/// is the mapped human-readable message and is never a raw Graph response body.
/// </summary>
/// <param name="Ok">Whether the probe read succeeded (a 2xx read; <c>ApiEnvelope.Ok</c>).</param>
/// <param name="ErrorCode">The adapter-level error code (<c>ApiError.Code</c>); null when <see cref="Ok"/>.</param>
/// <param name="BridgeErrorCode">The Graph <c>error.code</c> passthrough (<c>ApiError.BridgeErrorCode</c>).</param>
/// <param name="ErrorMessage">The mapped error message (<c>ApiError.Message</c>); never a response body.</param>
internal sealed record MailboxProbeOutcome(
    bool Ok,
    string? ErrorCode,
    string? BridgeErrorCode,
    string? ErrorMessage
);
