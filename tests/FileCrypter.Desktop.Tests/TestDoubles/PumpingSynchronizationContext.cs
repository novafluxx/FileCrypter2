namespace FileCrypter.Desktop.Tests.TestDoubles;

/// <summary>
/// A single-threaded <see cref="SynchronizationContext"/> that pumps posted callbacks on the
/// calling thread, mirroring how Avalonia marshals work (and <see cref="Progress{T}"/> callbacks)
/// onto the UI thread. Tests that exercise asynchronous progress reporting should run under this
/// context so callbacks are serialized deterministically instead of racing on the thread pool.
/// </summary>
internal sealed class PumpingSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> workItems = new();
    private bool completed;

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (workItems)
        {
            workItems.Enqueue((d, state));
            Monitor.PulseAll(workItems);
        }
    }

    public static void Run(Func<Task> asyncAction)
    {
        SynchronizationContext? originalContext = Current;
        var context = new PumpingSynchronizationContext();
        SetSynchronizationContext(context);

        try
        {
            Task task = asyncAction();
            task.ContinueWith(
                _ => context.Complete(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            context.RunOnCurrentThread();
            task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(originalContext);
        }
    }

    private void Complete()
    {
        lock (workItems)
        {
            completed = true;
            Monitor.PulseAll(workItems);
        }
    }

    private void RunOnCurrentThread()
    {
        while (true)
        {
            (SendOrPostCallback Callback, object? State)? workItem = null;

            lock (workItems)
            {
                while (workItems.Count == 0 && !completed)
                {
                    Monitor.Wait(workItems);
                }

                if (workItems.Count > 0)
                {
                    workItem = workItems.Dequeue();
                }
                else if (completed)
                {
                    return;
                }
            }

            workItem?.Callback(workItem.Value.State);
        }
    }
}
