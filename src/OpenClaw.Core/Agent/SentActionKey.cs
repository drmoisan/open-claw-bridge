namespace OpenClaw.Core.Agent;

/// <summary>
/// Pure, clock-free builder for send-idempotency dedupe keys. A dedupe key identifies
/// one outbound action for one message in one mailbox so the scheduling worker can
/// consult the sent-action store before sending and record after a successful send.
/// </summary>
/// <remarks>
/// Components are joined with <c>:</c> in the fixed order
/// <c>{mailbox}:{messageId}:{actionType}</c> and are <b>not</b> escaped; key
/// distinctness is therefore guaranteed only for colon-free components.
/// </remarks>
public static class SentActionKey
{
    /// <summary>The action type for an outbound proposal reply.</summary>
    public const string ProposalReply = "proposal-reply";

    /// <summary>The action type for an organizer-reschedule calendar write (issue #128, colon-free).</summary>
    public const string OrganizerReschedule = "organizer-reschedule";

    /// <summary>The action type for an attendee propose-new-time calendar write (issue #130, colon-free).</summary>
    public const string AttendeeProposeNewTime = "attendee-propose-new-time";

    /// <summary>
    /// Builds the dedupe key <c>{mailbox}:{messageId}:{actionType}</c> for the supplied
    /// components.
    /// </summary>
    /// <param name="mailbox">The mailbox UPN the action is performed for.</param>
    /// <param name="messageId">The identifier of the message the action responds to.</param>
    /// <param name="actionType">The action type (for example <see cref="ProposalReply"/>).</param>
    /// <returns>The colon-joined dedupe key.</returns>
    /// <exception cref="ArgumentException">
    /// A component is <see langword="null"/>, empty, or whitespace-only.
    /// </exception>
    public static string Build(string mailbox, string messageId, string actionType)
    {
        if (string.IsNullOrWhiteSpace(mailbox))
        {
            throw new ArgumentException(
                "Value must not be null, empty, or whitespace-only.",
                nameof(mailbox)
            );
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException(
                "Value must not be null, empty, or whitespace-only.",
                nameof(messageId)
            );
        }

        if (string.IsNullOrWhiteSpace(actionType))
        {
            throw new ArgumentException(
                "Value must not be null, empty, or whitespace-only.",
                nameof(actionType)
            );
        }

        return $"{mailbox}:{messageId}:{actionType}";
    }
}
