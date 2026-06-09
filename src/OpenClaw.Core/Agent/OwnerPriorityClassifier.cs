using System.Text.RegularExpressions;

namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic owner-priority classifier (D3). <see cref="Classify"/> is a pure
/// function that maps a normalized context to an <see cref="OwnerPriority"/> per master
/// Section 10.1-10.2, always returning a defined member.
/// </summary>
public static partial class OwnerPriorityClassifier
{
    [GeneratedRegex(@"\burgent\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrgentRegex();

    /// <summary>
    /// Classifies the request priority per master Section 10.2 in order: owner-initiated
    /// requests are P1; VIP senders are P0; urgent requests from direct reports are P0;
    /// non-VIP senders on the emblem domain or in the explicit P1 list are P1; direct
    /// reports or the explicit P2 list are P2; internal-domain senders or the explicit
    /// P3 list are P3; unknown recruiters escalate to the owner; otherwise the request
    /// is digest-ignored.
    /// </summary>
    /// <param name="ctx">The normalized meeting context. Must not be null.</param>
    /// <param name="policy">The owner scheduling policy. Must not be null.</param>
    /// <returns>The owner priority.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ctx"/> or <paramref name="policy"/> is null.
    /// </exception>
    public static OwnerPriority Classify(NormalizedMeetingContext ctx, OwnerSchedulingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(policy);

        var sender = string.IsNullOrEmpty(ctx.MessageFrom) ? ctx.MessageSender : ctx.MessageFrom;
        var domain = TriageEmail.DomainOf(sender);
        var isUrgent = UrgentRegex().IsMatch($"{ctx.Subject} {ctx.BodyText}");

        // Owner-initiated requests are Priority 1 (the owner asked to set up a meeting).
        if (IsOwnerInitiated(ctx, sender))
        {
            return OwnerPriority.P1;
        }

        // Priority 0: VIPs, or urgent requests from direct reports.
        if (policy.VipEmails.Contains(sender))
        {
            return OwnerPriority.P0;
        }

        if (isUrgent && policy.DirectReports.Contains(sender))
        {
            return OwnerPriority.P0;
        }

        // Priority 1: non-VIP emblem-domain senders, or the explicit P1 list.
        if (
            !policy.VipEmails.Contains(sender)
            && policy.EmblemEmailDomain.Length > 0
            && string.Equals(domain, policy.EmblemEmailDomain, StringComparison.Ordinal)
        )
        {
            return OwnerPriority.P1;
        }

        if (policy.Priority1.Contains(sender))
        {
            return OwnerPriority.P1;
        }

        // Priority 2: direct reports or the explicit P2 list.
        if (policy.DirectReports.Contains(sender) || policy.Priority2.Contains(sender))
        {
            return OwnerPriority.P2;
        }

        // Priority 3: internal-domain senders or the explicit P3 list.
        if (
            (
                policy.InternalDomain.Length > 0
                && string.Equals(domain, policy.InternalDomain, StringComparison.Ordinal)
            ) || policy.Priority3.Contains(sender)
        )
        {
            return OwnerPriority.P3;
        }

        // Priority 4: unknown external senders. Unknown recruiters escalate; the rest
        // are likely spam and are added to the digest of ignored requests.
        if (IsUnknownRecruiter(ctx))
        {
            return OwnerPriority.ESCALATE_TO_OWNER;
        }

        return OwnerPriority.DIGEST_IGNORED;
    }

    /// <summary>
    /// Determines whether the request was initiated by the mailbox owner. The owner is
    /// the submitting party when the sender equals the mailbox UPN.
    /// </summary>
    private static bool IsOwnerInitiated(NormalizedMeetingContext ctx, string sender) =>
        sender.Length > 0
        && string.Equals(
            sender,
            MeetingContextNormalizer.NormalizeEmail(ctx.MailboxUpn),
            StringComparison.Ordinal
        );

    /// <summary>
    /// Deterministic recruiter heuristic over the subject and body. The master leaves
    /// <c>is_unknown_recruiter</c> as an external signal; this pure surface uses a
    /// conservative keyword match so the classifier remains total and reproducible.
    /// </summary>
    private static bool IsUnknownRecruiter(NormalizedMeetingContext ctx)
    {
        var text = $"{ctx.Subject} {ctx.BodyText}";
        return RecruiterRegex().IsMatch(text);
    }

    [GeneratedRegex(
        @"\b(recruit(er|ing)?|talent acquisition|staffing|headhunter|opportunity)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex RecruiterRegex();
}
