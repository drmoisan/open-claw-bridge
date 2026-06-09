namespace OpenClaw.Core.Agent;

/// <summary>
/// Shared pure email-domain helpers for the triage layer (D2), ported from the
/// <c>isInternal</c> helper in master Section 9.2.
/// </summary>
internal static class TriageEmail
{
    /// <summary>
    /// Returns the domain portion of <paramref name="email"/> (the text after the first
    /// <c>@</c>), or empty when no <c>@</c> is present.
    /// </summary>
    internal static string DomainOf(string email)
    {
        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        return atIndex >= 0 && atIndex < email.Length - 1 ? email[(atIndex + 1)..] : string.Empty;
    }

    /// <summary>
    /// Determines whether <paramref name="email"/> belongs to an internal domain per the
    /// supplied <paramref name="policy"/>.
    /// </summary>
    internal static bool IsInternal(string email, TriagePolicy policy) =>
        policy.InternalDomains.Contains(DomainOf(email));
}
