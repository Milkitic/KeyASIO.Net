using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Common;

/// <summary>
/// Executes asynchronous work in submission order. Bounded workers apply real
/// backpressure through <see cref="ChannelWriter{T}.WriteAsync(T, CancellationToken)" />.
/// </summary>
public sealed class AsyncSequentialWorker : IDisposable, IAsyncDisposable
{
    private readonly Channel<WorkItem> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger? _logger;
    private readonly string _name;
    private readonly Task _workerLoop;
    private int _disposeState;

    public AsyncSequentialWorker(ILogger? logger = null, string name = "AsyncSequentialWorker", int capacity = 0)
    {
        _logger = logger;
        _name = name;
        _channel = capacity > 0
            ? Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            })
            : Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _workerLoop = Task.Run(ProcessQueueAsync);
    }

    public void Enqueue(Func<CancellationToken, Task> workItem)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        if (!_channel.Writer.TryWrite(new WorkItem(workItem, null)))
        {
            throw new InvalidOperationException(
                $"{_name} is bounded and currently full. Use EnqueueAsync to apply backpressure.");
        }
    }

    public void Enqueue(Func<Task> workItem) => Enqueue(_ => workItem());

    public Task EnqueueAsync(Func<CancellationToken, Task> workItem) =>
        EnqueueAsync<object?>(async cancellationToken =>
        {
            await workItem(cancellationToken).ConfigureAwait(false);
            return null;
        });

    public Task EnqueueAsync(Func<Task> workItem) => EnqueueAsync(_ => workItem());

    public async Task<T> EnqueueAsync<T>(
        Func<CancellationToken, Task<T>> workItem,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = new WorkItem(
            async workerToken =>
            {
                try
                {
                    completion.TrySetResult(await workItem(workerToken).ConfigureAwait(false));
                }
                catch (OperationCanceledException exception)
                {
                    completion.TrySetCanceled(exception.CancellationToken);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            },
            token => completion.TrySetCanceled(token));

        try
        {
            await _channel.Writer.WriteAsync(queued, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            throw new ObjectDisposedException(_name);
        }

        return await completion.Task.ConfigureAwait(false);
    }

    public Task<T> EnqueueAsync<T>(Func<Task<T>> workItem, CancellationToken cancellationToken = default) =>
        EnqueueAsync(_ => workItem(), cancellationToken);

    public void Dispose()
    {
        BeginDispose();
    }

    public async ValueTask DisposeAsync()
    {
        BeginDispose();
        try
        {
            await _workerLoop.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger?.LogWarning("Worker loop in {Name} did not stop within five seconds", _name);
        }
    }

    private void BeginDispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();
        _cts.Cancel();
        _ = _workerLoop.ContinueWith(
            static (_, state) => ((CancellationTokenSource)state!).Dispose(),
            _cts,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var workItem in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await workItem.Execute(_cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    workItem.Cancel?.Invoke(_cts.Token);
                    break;
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "Error processing work item in {Name}", _name);
                }

                if (_cts.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        finally
        {
            while (_channel.Reader.TryRead(out var pending))
            {
                pending.Cancel?.Invoke(_cts.Token);
            }
        }
    }

    private sealed record WorkItem(
        Func<CancellationToken, Task> Execute,
        Action<CancellationToken>? Cancel);
}
