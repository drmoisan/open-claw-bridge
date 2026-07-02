using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// Background worker (D6) that orchestrates the deterministic agent pipeline:
/// poll candidate messages → hydrate via <see cref="ISchedulingService"/> → normalize
/// (D1) → triage (D2) → priority/recurrence/move (D3, for <c>AUTO_COORDINATE</c> and
/// <c>HUMAN_APPROVAL</c>) → propose slots (D4, when scheduling is required). Each
/// decision class and its reasons are logged. Per-message I/O failures are isolated so a
/// single failure does not halt the loop. The <c>SendEnabled</c> and
/// <c>CalendarWriteEnabled</c> kill switches gate all side effects; the deterministic
/// pipeline still computes and logs when they are off. Every Stage 0 outbound-action
/// decision point writes one structured record to the <see cref="IActionAuditLog"/>
/// audit sink (issue #107). This worker is part of the runtime seam (namespace
/// <c>OpenClaw.Core.Agent.Runtime</c>).
/// </summary>
public sealed partial class SchedulingWorker(
    ISchedulingService schedulingService,
    ISentActionStore sentActionStore,
    IActionAuditLog actionAuditLog,
    ISchedulingCandidateSource candidateSource,
    IOptions<AgentPolicyOptions> policyOptions,
    TimeProvider timeProvider,
    ILogger<SchedulingWorker> logger
) : BackgroundService
{
    private readonly AgentPolicyOptions options = policyOptions.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSchedulingCycleAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), timeProvider, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Runs a single scheduling cycle: fetches candidate message identifiers and
    /// processes each one, isolating per-message failures. Exposed for deterministic
    /// testing.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    internal async Task RunSchedulingCycleAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> messageIds;
        try
        {
            messageIds = await candidateSource
                .GetCandidateMessageIdsAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Scheduling cycle failed to fetch candidate messages; skipping this cycle."
            );
            return;
        }

        foreach (var messageId in messageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessMessageSafelyAsync(messageId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessMessageSafelyAsync(
        string messageId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await ProcessMessageAsync(messageId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Isolate the failure: log the message identifier and continue the loop so a
            // single bad item does not halt scheduling.
            logger.LogError(
                exception,
                "Scheduling pipeline failed for message {MessageId}; continuing with the next item.",
                messageId
            );
        }
    }
}
