using System.Collections.Concurrent;
using System.Threading;

namespace OpenClaw.MailBridge;

internal interface IOutlookStaExecutor : IDisposable
{
    Task<T> InvokeAsync<T>(Func<T> operation);
}

/// <summary>
/// Executes Outlook COM work on a dedicated STA thread so background services can call into Outlook safely.
/// </summary>
internal sealed class OutlookStaExecutor : IOutlookStaExecutor
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

    public async Task<T> InvokeAsync<T>(Func<T> operation)
    {
        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _queue.Add((() => operation()!, tcs));
        var value = await tcs.Task.ConfigureAwait(false);
        return (T)value!;
    }

    private void Run()
    {
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

    public void Dispose() => _queue.CompleteAdding();
}
