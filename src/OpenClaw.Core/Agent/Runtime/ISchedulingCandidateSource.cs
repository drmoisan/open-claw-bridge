namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// A narrow seam supplying the candidate message identifiers the
/// <see cref="SchedulingWorker"/> processes each cycle. Implemented over the local
/// cache repository at runtime and substituted with a test double in unit tests.
/// </summary>
public interface ISchedulingCandidateSource
{
    /// <summary>
    /// Returns the candidate meeting-message identifiers to process this cycle.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The candidate message identifiers.</returns>
    Task<IReadOnlyList<string>> GetCandidateMessageIdsAsync(CancellationToken cancellationToken);
}
