using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Threading;

namespace OpenClaw.MailBridge;

/// <summary>
/// Executes Outlook COM work on a dedicated STA thread so background services can call into Outlook safely.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class OutlookStaExecutor : IDisposable
{
    private readonly BlockingCollection<(
        Func<object?> work,
        TaskCompletionSource<object?> tcs
    )> _queue = new();
    private readonly Thread _thread;

    /// <summary>
    /// Starts the dedicated STA worker thread used for COM-bound Outlook work.
    /// </summary>
    public OutlookStaExecutor()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Outlook-STA" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>
    /// Schedules work to run on the STA thread and returns the operation result asynchronously.
    /// </summary>
    /// <typeparam name="T">Type produced by the scheduled operation.</typeparam>
    /// <param name="operation">Synchronous COM-bound work to execute.</param>
    /// <returns>The value produced by <paramref name="operation"/>.</returns>
    public async Task<T> InvokeAsync<T>(Func<T> operation)
    {
        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _queue.Add((() => operation()!, tcs));
        var value = await tcs.Task.ConfigureAwait(false);
        return (T)value!;
    }

    /// <summary>
    /// Processes queued COM work items sequentially on the dedicated STA thread.
    /// </summary>
    private void Run()
    {
        // Serialize all COM calls through one apartment so Outlook sees a consistent threading model.
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            try
            {
                item.tcs.SetResult(item.work());
            }
            catch (Exception ex)
            {
                item.tcs.SetException(ex);
            }
        }
    }

    /// <summary>
    /// Stops accepting new work and allows the STA thread to drain outstanding operations.
    /// </summary>
    public void Dispose() => _queue.CompleteAdding();
}
